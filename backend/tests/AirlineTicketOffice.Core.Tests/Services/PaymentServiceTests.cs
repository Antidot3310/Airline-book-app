using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса проведения платежей <see cref="PaymentService"/> (M8).
/// </summary>
public class PaymentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;
    private readonly List<IDisposable> _extraDisposables = [];

    public PaymentServiceTests()
    {
        var services = new ServiceCollection();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<AirlineDbContext>();
        _dbContext.Database.EnsureCreated();
        DbInitializer.Initialize(_dbContext);

        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        foreach (IDisposable item in _extraDisposables)
        {
            item.Dispose();
        }

        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private (
        PaymentService PaymentService,
        PaymentEmulator Emulator,
        OrderTicketService OrderService,
        VirtualClockService ClockService,
        AuditLogService AuditService)
        CreateServices(bool isolateClockScheduler = false)
    {
        IServiceScopeFactory clockScopeFactory = _scopeFactory;

        if (isolateClockScheduler)
        {
            var isolatedServices = new ServiceCollection();
            var clockConn = new SqliteConnection("DataSource=:memory:");
            clockConn.Open();
            isolatedServices.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(clockConn));
            var clockProvider = isolatedServices.BuildServiceProvider();
            AirlineDbContext clockDb = clockProvider.GetRequiredService<AirlineDbContext>();
            clockDb.Database.EnsureCreated();
            DbInitializer.Initialize(clockDb);

            _extraDisposables.Add(clockDb);
            _extraDisposables.Add(clockProvider);
            _extraDisposables.Add(clockConn);

            clockScopeFactory = clockProvider.GetRequiredService<IServiceScopeFactory>();
        }

        var clockService = new VirtualClockService(clockScopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var sessionService = new SessionService(auditService);
        sessionService.OpenSession(UserRole.Passenger);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var orderService = new OrderTicketService(_dbContext, tariffCalculator, clockService, sessionService, auditService);
        var emulator = new PaymentEmulator(_dbContext, clockService, auditService);
        var paymentService = new PaymentService(_dbContext, emulator, clockService, auditService);

        return (paymentService, emulator, orderService, clockService, auditService);
    }

    private OrderDto CreateSampleOrder(OrderTicketService orderService, params string[] seatNumbers)
    {
        Flight flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        string[] seatsToBook = seatNumbers.Length > 0 ? seatNumbers : ["4A"];

        List<PassengerOrderItemDto> passengers = seatsToBook
            .Select((seatNumber, index) => new PassengerOrderItemDto
            {
                FullName = $"Пассажир Тест {index + 1}",
                DocumentNumber = $"DOC-PAY-{index + 1:D3}",
                SeatNumber = seatNumber,
                HasBaggage = false
            })
            .ToList();

        Result<OrderDto> result = orderService.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers = passengers
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static CardDetailsDto CreateValidCard()
    {
        return new CardDetailsDto
        {
            CardNumber = "4276 1234 5678 9012",
            CardHolder = "IVAN IVANOV",
            ExpiryMonth = "12",
            ExpiryYear = "29",
            Cvv = "123"
        };
    }

    [Fact]
    public void PayWithCard_WhenOrderActiveAndEmulatorSucceeds_IssuesTicketsAndMarksSeatsSold()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, AuditLogService auditService) =
            CreateServices();
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A", "4C");
        CardDetailsDto card = CreateValidCard();

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCard(orderDto.Id, card);

        // Assert
        Assert.True(result.IsSuccess);
        PaymentResultDto paymentDto = result.Value;
        Assert.Equal(orderDto.Id, paymentDto.OrderId);
        Assert.Equal(orderDto.OrderNumber, paymentDto.OrderNumber);
        Assert.Equal(orderDto.TotalAmount, paymentDto.Amount);
        Assert.Equal(PaymentMethod.Card, paymentDto.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, paymentDto.Status);
        Assert.False(string.IsNullOrWhiteSpace(paymentDto.ExternalTransactionId));

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
        Assert.Equal(2, persistedOrder.Tickets.Count);
        Assert.All(persistedOrder.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Paid, ticket.Status);
            Assert.NotNull(ticket.FlightSeat);
            Assert.Equal(SeatState.Sold, ticket.FlightSeat.State);
            Assert.Null(ticket.FlightSeat.HoldUntilTime);
        });

        Payment persistedPayment = Assert.Single(persistedOrder.Payments);
        Assert.Equal(PaymentStatus.Success, persistedPayment.Status);
        Assert.Equal(PaymentMethod.Card, persistedPayment.PaymentMethod);
        Assert.Equal(orderDto.TotalAmount, persistedPayment.Amount);

        IReadOnlyList<AuditLogDto> logs = auditService.GetLogs(null, null, null, "PaymentSuccess");
        Assert.Single(logs);
        Assert.Equal("Payment", logs[0].EntityName);
        Assert.Equal(persistedPayment.Id, logs[0].EntityId);
    }

    [Fact]
    public void PayWithCard_At14Min59SecBeforeExpiry_SucceedsMatchesTC04A()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, VirtualClockService clockService, _) =
            CreateServices(isolateClockScheduler: true);
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");
        CardDetailsDto card = CreateValidCard();

        clockService.AdvanceTime(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCard(orderDto.Id, card);

        // Assert (TC-04А)
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Success, result.Value.Status);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
        Assert.All(persistedOrder.Tickets, t =>
        {
            Assert.Equal(TicketStatus.Paid, t.Status);
            Assert.Equal(SeatState.Sold, t.FlightSeat!.State);
        });
    }

    [Fact]
    public void PayWithCard_At15Min01SecAfterExpiry_FailsExpiresOrderAndFreesSeatsMatchesTC04B()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, VirtualClockService clockService, AuditLogService auditService) =
            CreateServices(isolateClockScheduler: true);
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A", "4C");
        CardDetailsDto card = CreateValidCard();

        clockService.AdvanceTime(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCard(orderDto.Id, card);

        // Assert (TC-04Б)
        Assert.True(result.IsFailure);
        Assert.Equal("Срок действия бронирования истёк.", result.ErrorMessage);
        Assert.Empty(emulator.GetIncomingRequestsLog());

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Expired, persistedOrder.Status);
        Assert.Empty(persistedOrder.Payments);
        Assert.All(persistedOrder.Tickets, t =>
        {
            Assert.Equal(TicketStatus.Cancelled, t.Status);
            Assert.NotNull(t.FlightSeat);
            Assert.Equal(SeatState.Available, t.FlightSeat.State);
            Assert.Null(t.FlightSeat.HoldUntilTime);
        });

        IReadOnlyList<AuditLogDto> logs = auditService.GetLogs(null, null, null, "ExpireHold");
        Assert.Single(logs);
        Assert.Equal("Order", logs[0].EntityName);
        Assert.Equal(orderDto.Id, logs[0].EntityId);
    }

    [Fact]
    public void PayWithCard_WhenEmulatorRejects_SavesFailedPaymentAndAllowsRetry()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, _) =
            CreateServices();
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");
        CardDetailsDto card = CreateValidCard();

        // Act 1 (отказ шлюза)
        emulator.Mode = PaymentEmulatorMode.AlwaysReject;
        Result<PaymentResultDto> failedResult = paymentService.PayWithCard(orderDto.Id, card);

        // Assert 1 (заказ и бронь сохраняются для повторной попытки)
        Assert.True(failedResult.IsFailure);
        Assert.Contains("отклонён", failedResult.ErrorMessage);

        _dbContext.ChangeTracker.Clear();

        Order orderAfterReject = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Created, orderAfterReject.Status);
        Assert.All(orderAfterReject.Tickets, t =>
        {
            Assert.Equal(TicketStatus.Booked, t.Status);
            Assert.Equal(SeatState.Held, t.FlightSeat!.State);
        });
        Payment failedPayment = Assert.Single(orderAfterReject.Payments);
        Assert.Equal(PaymentStatus.Failed, failedPayment.Status);

        // Act 2 (повторная попытка в режиме успеха)
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        Result<PaymentResultDto> retryResult = paymentService.PayWithCard(orderDto.Id, card);

        // Assert 2
        Assert.True(retryResult.IsSuccess);
        Assert.Equal(PaymentStatus.Success, retryResult.Value.Status);

        _dbContext.ChangeTracker.Clear();

        Order orderAfterRetry = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Paid, orderAfterRetry.Status);
        Assert.All(orderAfterRetry.Tickets, t =>
        {
            Assert.Equal(TicketStatus.Paid, t.Status);
            Assert.Equal(SeatState.Sold, t.FlightSeat!.State);
        });
        Assert.Equal(2, orderAfterRetry.Payments.Count);
        Assert.Single(orderAfterRetry.Payments, p => p.Status == PaymentStatus.Failed);
        Assert.Single(orderAfterRetry.Payments, p => p.Status == PaymentStatus.Success);
    }

    [Fact]
    public void PayWithCard_WhenOrderAlreadyPaid_FailsForIdempotency()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, _) =
            CreateServices();
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");
        CardDetailsDto card = CreateValidCard();

        Result<PaymentResultDto> firstResult = paymentService.PayWithCard(orderDto.Id, card);
        Assert.True(firstResult.IsSuccess);

        // Act
        Result<PaymentResultDto> secondResult = paymentService.PayWithCard(orderDto.Id, card);

        // Assert
        Assert.True(secondResult.IsFailure);
        Assert.Equal("Заказ уже оплачен.", secondResult.ErrorMessage);
        Assert.Single(emulator.GetIncomingRequestsLog());

        int paymentsCount = _dbContext.Payments.Count(p => p.OrderId == orderDto.Id);
        Assert.Equal(1, paymentsCount);
    }

    [Fact]
    public void PayWithCard_WhenCardInvalid_ReturnsFailure()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, _) =
            CreateServices();
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");

        var shortCard = new CardDetailsDto
        {
            CardNumber = "1234-56",
            CardHolder = "IVAN IVANOV",
            ExpiryMonth = "12",
            ExpiryYear = "29",
            Cvv = "123"
        };

        var emptyCvvCard = new CardDetailsDto
        {
            CardNumber = "4276 1234 5678 9012",
            CardHolder = "IVAN IVANOV",
            ExpiryMonth = "12",
            ExpiryYear = "29",
            Cvv = "   "
        };

        // Act
        Result<PaymentResultDto> shortCardResult = paymentService.PayWithCard(orderDto.Id, shortCard);
        Result<PaymentResultDto> emptyCvvResult = paymentService.PayWithCard(orderDto.Id, emptyCvvCard);
        Result<PaymentResultDto> nullCardResult = paymentService.PayWithCard(orderDto.Id, null!);

        // Assert
        Assert.True(shortCardResult.IsFailure);
        Assert.Equal("Указаны некорректные реквизиты банковской карты.", shortCardResult.ErrorMessage);

        Assert.True(emptyCvvResult.IsFailure);
        Assert.Equal("Указаны некорректные реквизиты банковской карты.", emptyCvvResult.ErrorMessage);

        Assert.True(nullCardResult.IsFailure);
        Assert.Equal("Указаны некорректные реквизиты банковской карты.", nullCardResult.ErrorMessage);

        Assert.Empty(emulator.GetIncomingRequestsLog());
        Assert.Empty(_dbContext.Payments);
    }

    [Fact]
    public void PayWithCash_WhenCashLessThanOrderTotal_ReturnsFailureMatchesTC14A()
    {
        // Arrange
        (PaymentService paymentService, _, OrderTicketService orderService, _, _) =
            CreateServices();
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");
        decimal insufficientCash = orderDto.TotalAmount - 500m;

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCash(orderDto.Id, insufficientCash);

        // Assert (TC-14А)
        Assert.True(result.IsFailure);
        Assert.Contains("Внесённая сумма меньше стоимости заказа", result.ErrorMessage);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Created, persistedOrder.Status);
        Assert.Empty(persistedOrder.Payments);
        Assert.All(persistedOrder.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Booked, ticket.Status);
            Assert.Equal(SeatState.Held, ticket.FlightSeat!.State);
        });
    }

    [Fact]
    public void PayWithCash_WhenCashGreaterThanOrderTotal_SucceedsAndCalculatesChangeMatchesTC14B()
    {
        // Arrange
        (PaymentService paymentService, _, OrderTicketService orderService, _, AuditLogService auditService) =
            CreateServices();
        OrderDto orderDto = CreateSampleOrder(orderService, "4A", "4C");
        decimal cashReceived = orderDto.TotalAmount + 2000m;

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCash(orderDto.Id, cashReceived);

        // Assert (TC-14Б)
        Assert.True(result.IsSuccess);
        PaymentResultDto paymentDto = result.Value;
        Assert.Equal(orderDto.Id, paymentDto.OrderId);
        Assert.Equal(orderDto.OrderNumber, paymentDto.OrderNumber);
        Assert.Equal(orderDto.TotalAmount, paymentDto.Amount);
        Assert.Equal(PaymentMethod.Cash, paymentDto.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, paymentDto.Status);
        Assert.Null(paymentDto.ExternalTransactionId);
        Assert.Equal(cashReceived, paymentDto.CashReceived);
        Assert.Equal(2000.00m, paymentDto.ChangeAmount);
        Assert.Contains("Оплата наличными принята", paymentDto.Message);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
        Assert.All(persistedOrder.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Paid, ticket.Status);
            Assert.NotNull(ticket.FlightSeat);
            Assert.Equal(SeatState.Sold, ticket.FlightSeat.State);
            Assert.Null(ticket.FlightSeat.HoldUntilTime);
        });

        Payment persistedPayment = Assert.Single(persistedOrder.Payments);
        Assert.Equal(PaymentStatus.Success, persistedPayment.Status);
        Assert.Equal(PaymentMethod.Cash, persistedPayment.PaymentMethod);
        Assert.Null(persistedPayment.ExternalTransactionId);

        IReadOnlyList<AuditLogDto> logs = auditService.GetLogs(null, null, null, "PaymentSuccess");
        Assert.Single(logs);
        Assert.Equal("Payment", logs[0].EntityName);
        Assert.Equal(persistedPayment.Id, logs[0].EntityId);
        Assert.Contains("наличными", logs[0].Description);
    }

    [Fact]
    public void PayWithCash_WhenExactAmountProvided_SucceedsWithZeroChange()
    {
        // Arrange
        (PaymentService paymentService, _, OrderTicketService orderService, _, _) =
            CreateServices();
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCash(orderDto.Id, orderDto.TotalAmount);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentMethod.Cash, result.Value.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, result.Value.Status);
        Assert.Equal(orderDto.TotalAmount, result.Value.CashReceived);
        Assert.Equal(0m, result.Value.ChangeAmount);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders.First(o => o.Id == orderDto.Id);
        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
    }

    [Fact]
    public void PayWithCash_WhenOrderHoldExpired_FailsAndExpiresOrder()
    {
        // Arrange
        (PaymentService paymentService, _, OrderTicketService orderService, VirtualClockService clockService, _) =
            CreateServices(isolateClockScheduler: true);
        OrderDto orderDto = CreateSampleOrder(orderService, "4A", "4C");

        clockService.AdvanceTime(TimeSpan.FromMinutes(16));

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCash(orderDto.Id, orderDto.TotalAmount);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Срок действия бронирования истёк.", result.ErrorMessage);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Expired, persistedOrder.Status);
        Assert.Empty(persistedOrder.Payments);
        Assert.All(persistedOrder.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Cancelled, ticket.Status);
            Assert.NotNull(ticket.FlightSeat);
            Assert.Equal(SeatState.Available, ticket.FlightSeat.State);
            Assert.Null(ticket.FlightSeat.HoldUntilTime);
        });
    }

    [Fact]
    public void PayWithCash_WhenOrderAlreadyPaid_FailsForIdempotency()
    {
        // Arrange
        (PaymentService paymentService, _, OrderTicketService orderService, _, _) =
            CreateServices();
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");

        Result<PaymentResultDto> firstResult = paymentService.PayWithCash(orderDto.Id, orderDto.TotalAmount);
        Assert.True(firstResult.IsSuccess);

        // Act
        Result<PaymentResultDto> secondResult = paymentService.PayWithCash(orderDto.Id, orderDto.TotalAmount);

        // Assert
        Assert.True(secondResult.IsFailure);
        Assert.Equal("Заказ уже оплачен.", secondResult.ErrorMessage);

        int paymentsCount = _dbContext.Payments.Count(p => p.OrderId == orderDto.Id);
        Assert.Equal(1, paymentsCount);
    }

    [Fact]
    public void PayWithCard_WhenEmulatorTimesOut_CreatesIndeterminatePaymentAndKeepsSeatsHeldMatchesTC15()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, _) =
            CreateServices();
        emulator.Mode = PaymentEmulatorMode.Timeout;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A", "4C");
        CardDetailsDto card = CreateValidCard();

        // Act
        Result<PaymentResultDto> result = paymentService.PayWithCard(orderDto.Id, card);

        // Assert (TC-15)
        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Indeterminate, result.Value.Status);
        Assert.Null(result.Value.ExternalTransactionId);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Created, persistedOrder.Status);
        Assert.All(persistedOrder.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Booked, ticket.Status);
            Assert.NotNull(ticket.FlightSeat);
            Assert.Equal(SeatState.Held, ticket.FlightSeat.State);
            Assert.NotNull(ticket.FlightSeat.HoldUntilTime);
        });

        Payment persistedPayment = Assert.Single(persistedOrder.Payments);
        Assert.Equal(PaymentStatus.Indeterminate, persistedPayment.Status);

        Result<PaymentResultDto> duplicateAttempt = paymentService.PayWithCard(orderDto.Id, card);
        Assert.True(duplicateAttempt.IsFailure);
        Assert.Contains("Indeterminate", duplicateAttempt.ErrorMessage);
        Assert.Single(emulator.GetIncomingRequestsLog());
    }

    [Fact]
    public void CheckPaymentStatus_And_ManualResolveSuccess_FullFlowMatchesTC15()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, _) =
            CreateServices();
        emulator.Mode = PaymentEmulatorMode.Timeout;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");
        CardDetailsDto card = CreateValidCard();

        // Act 1: попытка оплаты при таймауте шлюза
        Result<PaymentResultDto> payResult = paymentService.PayWithCard(orderDto.Id, card);
        Assert.True(payResult.IsSuccess);
        int paymentId = payResult.Value.PaymentId;
        Assert.Equal(PaymentStatus.Indeterminate, payResult.Value.Status);

        // Act 2: проверка статуса до вмешательства оператора
        Result<PaymentStatus> pendingStatus = paymentService.CheckPaymentStatus(paymentId);
        Assert.True(pendingStatus.IsSuccess);
        Assert.Equal(PaymentStatus.Indeterminate, pendingStatus.Value);

        // Act 3: ручное подтверждение оператором в эмуляторе
        Result resolveResult = emulator.ManuallyResolvePayment(paymentId, markSuccess: true);
        Assert.True(resolveResult.IsSuccess);

        // Act 4: повторная проверка статуса после резолва
        Result<PaymentStatus> resolvedStatus = paymentService.CheckPaymentStatus(paymentId);

        // Assert (TC-15)
        Assert.True(resolvedStatus.IsSuccess);
        Assert.Equal(PaymentStatus.Success, resolvedStatus.Value);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
        Assert.All(persistedOrder.Tickets, ticket =>
        {
            Assert.Equal(TicketStatus.Paid, ticket.Status);
            Assert.NotNull(ticket.FlightSeat);
            Assert.Equal(SeatState.Sold, ticket.FlightSeat.State);
            Assert.Null(ticket.FlightSeat.HoldUntilTime);
        });
    }

    [Fact]
    public void CheckPaymentStatus_WhenManualResolveRejected_ReturnsFailedAndAllowsRetry()
    {
        // Arrange
        (PaymentService paymentService, PaymentEmulator emulator, OrderTicketService orderService, _, _) =
            CreateServices();
        emulator.Mode = PaymentEmulatorMode.Timeout;
        OrderDto orderDto = CreateSampleOrder(orderService, "4A");
        CardDetailsDto card = CreateValidCard();

        Result<PaymentResultDto> timeoutResult = paymentService.PayWithCard(orderDto.Id, card);
        Assert.True(timeoutResult.IsSuccess);
        int paymentId = timeoutResult.Value.PaymentId;

        // Act 1: оператор отклоняет зависший платёж
        Result rejectResult = emulator.ManuallyResolvePayment(paymentId, markSuccess: false);
        Assert.True(rejectResult.IsSuccess);

        Result<PaymentStatus> statusAfterReject = paymentService.CheckPaymentStatus(paymentId);

        // Assert 1
        Assert.True(statusAfterReject.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, statusAfterReject.Value);

        _dbContext.ChangeTracker.Clear();

        Order orderAfterReject = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Created, orderAfterReject.Status);
        Assert.All(orderAfterReject.Tickets, t => Assert.Equal(SeatState.Held, t.FlightSeat!.State));

        // Act 2: повторная оплата при исправном шлюзе
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        Result<PaymentResultDto> retryResult = paymentService.PayWithCard(orderDto.Id, card);

        // Assert 2
        Assert.True(retryResult.IsSuccess);
        Assert.Equal(PaymentStatus.Success, retryResult.Value.Status);

        _dbContext.ChangeTracker.Clear();

        Order paidOrder = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .First(o => o.Id == orderDto.Id);

        Assert.Equal(OrderStatus.Paid, paidOrder.Status);
        Assert.All(paidOrder.Tickets, t => Assert.Equal(SeatState.Sold, t.FlightSeat!.State));
    }

    [Fact]
    public void CheckPaymentStatus_WhenPaymentNotFound_ReturnsFailure()
    {
        // Arrange
        (PaymentService paymentService, _, _, _, _) = CreateServices();
        int nonExistentPaymentId = 999999;

        // Act
        Result<PaymentStatus> result = paymentService.CheckPaymentStatus(nonExistentPaymentId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("не найден", result.ErrorMessage);
    }
}
