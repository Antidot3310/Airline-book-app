using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Common;
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
/// Модульные тесты для эмулятора платёжного шлюза <see cref="PaymentEmulator"/> (M14).
/// </summary>
public class PaymentEmulatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;

    public PaymentEmulatorTests()
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
        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private (PaymentEmulator Emulator, VirtualClockService ClockService, AuditLogService AuditService) CreateEmulator()
    {
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var emulator = new PaymentEmulator(_dbContext, clockService, auditService);
        return (emulator, clockService, auditService);
    }

    private static CardDetailsDto CreateSampleCard(string cardNumber = "4276 1234 5678 1234")
    {
        return new CardDetailsDto
        {
            CardNumber = cardNumber,
            CardHolder = "IVAN IVANOV",
            ExpiryMonth = "12",
            ExpiryYear = "29",
            Cvv = "123"
        };
    }

    private (Order Order, Ticket Ticket, FlightSeat Seat, Payment Payment) SeedOrderWithPayment(
        PaymentStatus paymentStatus,
        DateTime currentVirtualTime)
    {
        Flight flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        FlightSeat seat = _dbContext.FlightSeats.First(s => s.FlightId == flight.Id && s.SeatNumber == "4A");

        var client = new Client
        {
            FullName = "Иванов Иван Иванович",
            DocumentNumber = "4510 123456",
            ContactPhone = "+79991112233"
        };
        _dbContext.Clients.Add(client);

        seat.State = SeatState.Held;
        seat.HoldUntilTime = currentVirtualTime.AddMinutes(15);

        var order = new Order
        {
            OrderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            TotalAmount = 8100.00m,
            Status = OrderStatus.Created,
            CreatedAt = currentVirtualTime,
            HoldExpiresAt = currentVirtualTime.AddMinutes(15)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        var ticket = new Ticket
        {
            TicketNumber = $"TKT-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
            OrderId = order.Id,
            Order = order,
            ClientId = client.Id,
            Client = client,
            FlightId = flight.Id,
            Flight = flight,
            FlightSeatId = seat.Id,
            FlightSeat = seat,
            Price = 8100.00m,
            HasBaggage = false,
            Status = TicketStatus.Booked
        };
        _dbContext.Tickets.Add(ticket);

        var payment = new Payment
        {
            OrderId = order.Id,
            Order = order,
            Amount = order.TotalAmount,
            PaymentMethod = PaymentMethod.Card,
            Status = paymentStatus,
            ExternalTransactionId = paymentStatus == PaymentStatus.Success ? "TXN-EXISTING" : null,
            CreatedAt = currentVirtualTime
        };
        _dbContext.Payments.Add(payment);
        _dbContext.SaveChanges();

        return (order, ticket, seat, payment);
    }

    [Fact]
    public void ProcessPayment_WhenAlwaysSuccessMode_ReturnsSuccessAndGeneratesTransactionId()
    {
        // Arrange
        (PaymentEmulator emulator, VirtualClockService clockService, _) = CreateEmulator();
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        CardDetailsDto card = CreateSampleCard("4276 8800 9911 1234");

        // Act
        EmulatorTransactionResult result = emulator.ProcessPayment(orderId: 101, amount: 8100.00m, card);

        // Assert
        Assert.Equal(PaymentStatus.Success, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ExternalTransactionId));
        Assert.StartsWith("TXN-", result.ExternalTransactionId);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));

        IReadOnlyList<EmulatorLogEntryDto> log = emulator.GetIncomingRequestsLog();
        Assert.Single(log);
        EmulatorLogEntryDto entry = log[0];
        Assert.Equal(101, entry.OrderId);
        Assert.Equal(8100.00m, entry.Amount);
        Assert.Equal("**** **** **** 1234", entry.MaskedCardNumber);
        Assert.Equal(PaymentStatus.Success, entry.Status);
        Assert.Equal(result.ExternalTransactionId, entry.ExternalTransactionId);
        Assert.Equal(clockService.CurrentVirtualTime, entry.Timestamp);
    }

    [Fact]
    public void ProcessPayment_WhenAlwaysRejectMode_ReturnsFailedAndNullTransactionId()
    {
        // Arrange
        (PaymentEmulator emulator, _, _) = CreateEmulator();
        emulator.Mode = PaymentEmulatorMode.AlwaysReject;
        CardDetailsDto card = CreateSampleCard();

        // Act
        EmulatorTransactionResult result = emulator.ProcessPayment(orderId: 202, amount: 16200.00m, card);

        // Assert
        Assert.Equal(PaymentStatus.Failed, result.Status);
        Assert.Null(result.ExternalTransactionId);
        Assert.Contains("отклонён", result.Message);

        IReadOnlyList<EmulatorLogEntryDto> log = emulator.GetIncomingRequestsLog();
        Assert.Single(log);
        Assert.Equal(PaymentStatus.Failed, log[0].Status);
        Assert.Null(log[0].ExternalTransactionId);
    }

    [Fact]
    public void ProcessPayment_WhenTimeoutMode_ReturnsIndeterminate()
    {
        // Arrange
        (PaymentEmulator emulator, _, _) = CreateEmulator();
        emulator.Mode = PaymentEmulatorMode.Timeout;
        CardDetailsDto card = CreateSampleCard();

        // Act
        EmulatorTransactionResult result = emulator.ProcessPayment(orderId: 303, amount: 9600.00m, card);

        // Assert
        Assert.Equal(PaymentStatus.Indeterminate, result.Status);
        Assert.Null(result.ExternalTransactionId);
        Assert.Contains("Таймаут", result.Message);

        IReadOnlyList<EmulatorLogEntryDto> log = emulator.GetIncomingRequestsLog();
        Assert.Single(log);
        Assert.Equal(PaymentStatus.Indeterminate, log[0].Status);
    }

    [Fact]
    public void ClearLog_ClearsIncomingRequests()
    {
        // Arrange
        (PaymentEmulator emulator, _, _) = CreateEmulator();
        emulator.Mode = PaymentEmulatorMode.AlwaysSuccess;
        CardDetailsDto card = CreateSampleCard();
        emulator.ProcessPayment(orderId: 1, amount: 5000.00m, card);
        emulator.ProcessPayment(orderId: 2, amount: 7500.00m, card);
        Assert.Equal(2, emulator.GetIncomingRequestsLog().Count);

        // Act
        emulator.ClearLog();

        // Assert
        IReadOnlyList<EmulatorLogEntryDto> logAfterClear = emulator.GetIncomingRequestsLog();
        Assert.Empty(logAfterClear);
    }

    [Fact]
    public void ManuallyResolvePayment_WhenIndeterminateAndMarkSuccess_UpdatesOrderTicketsAndSeatsMatchesTC15()
    {
        // Arrange
        (PaymentEmulator emulator, VirtualClockService clockService, AuditLogService auditService) = CreateEmulator();
        (Order order, Ticket ticket, FlightSeat seat, Payment payment) =
            SeedOrderWithPayment(PaymentStatus.Indeterminate, clockService.CurrentVirtualTime);

        // Act
        Result result = emulator.ManuallyResolvePayment(payment.Id, markSuccess: true);

        // Assert (TC-15)
        Assert.True(result.IsSuccess);

        _dbContext.ChangeTracker.Clear();

        Payment persistedPayment = _dbContext.Payments.First(p => p.Id == payment.Id);
        Order persistedOrder = _dbContext.Orders.First(o => o.Id == order.Id);
        Ticket persistedTicket = _dbContext.Tickets.First(t => t.Id == ticket.Id);
        FlightSeat persistedSeat = _dbContext.FlightSeats.First(s => s.Id == seat.Id);

        Assert.Equal(PaymentStatus.Success, persistedPayment.Status);
        Assert.False(string.IsNullOrWhiteSpace(persistedPayment.ExternalTransactionId));
        Assert.Equal(OrderStatus.Paid, persistedOrder.Status);
        Assert.Equal(TicketStatus.Paid, persistedTicket.Status);
        Assert.Equal(SeatState.Sold, persistedSeat.State);
        Assert.Null(persistedSeat.HoldUntilTime);

        IReadOnlyList<AuditLogDto> logs = auditService.GetLogs(null, null, null, "ManualPaymentResolve");
        Assert.Single(logs);
        Assert.Equal("Payment", logs[0].EntityName);
        Assert.Equal(payment.Id, logs[0].EntityId);
        Assert.Contains("подтверждён", logs[0].Description);
    }

    [Fact]
    public void ManuallyResolvePayment_WhenPaymentNotIndeterminate_ReturnsFailure()
    {
        // Arrange
        (PaymentEmulator emulator, VirtualClockService clockService, _) = CreateEmulator();
        (Order _, Ticket _, FlightSeat _, Payment payment) =
            SeedOrderWithPayment(PaymentStatus.Success, clockService.CurrentVirtualTime);

        // Act
        Result result = emulator.ManuallyResolvePayment(payment.Id, markSuccess: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Indeterminate", result.ErrorMessage);

        Payment persistedPayment = _dbContext.Payments.First(p => p.Id == payment.Id);
        Assert.Equal(PaymentStatus.Success, persistedPayment.Status);
    }

    [Fact]
    public void ManuallyResolvePayment_WhenOrderAlreadyExpired_ReturnsFailure()
    {
        // Arrange
        (PaymentEmulator emulator, VirtualClockService clockService, _) = CreateEmulator();
        (Order order, Ticket ticket, FlightSeat seat, Payment payment) =
            SeedOrderWithPayment(PaymentStatus.Indeterminate, clockService.CurrentVirtualTime);

        order.Status = OrderStatus.Expired;
        ticket.Status = TicketStatus.Cancelled;
        seat.State = SeatState.Available;
        seat.HoldUntilTime = null;
        _dbContext.SaveChanges();

        // Act
        Result result = emulator.ManuallyResolvePayment(payment.Id, markSuccess: true);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Expired", result.ErrorMessage);

        _dbContext.ChangeTracker.Clear();

        Order persistedOrder = _dbContext.Orders.First(o => o.Id == order.Id);
        Payment persistedPayment = _dbContext.Payments.First(p => p.Id == payment.Id);
        Assert.Equal(OrderStatus.Expired, persistedOrder.Status);
        Assert.Equal(PaymentStatus.Indeterminate, persistedPayment.Status);
    }
}
