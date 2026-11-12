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
}
