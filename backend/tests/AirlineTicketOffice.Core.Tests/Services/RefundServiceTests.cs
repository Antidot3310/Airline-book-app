using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса расчёта и проведения возвратов билетов <see cref="RefundService"/> (M9).
/// </summary>
public class RefundServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;

    public RefundServiceTests()
    {
        ServiceCollection services = new ServiceCollection();

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

    private (RefundService Service, VirtualClockService ClockService) CreateServiceWithClock()
    {
        VirtualClockService clockService = new VirtualClockService(_scopeFactory);
        AuditLogService auditService = new AuditLogService(_dbContext);
        RefundService refundService = new RefundService(_dbContext, clockService, auditService);

        return (refundService, clockService);
    }

    private Ticket CreateTestTicket(
        DateTime departureTime,
        decimal price,
        TicketStatus ticketStatus,
        FlightStatus flightStatus = FlightStatus.Scheduled)
    {
        Airport origin = _dbContext.Airports.First();
        Airport destination = _dbContext.Airports.Skip(1).First();
        Aircraft aircraft = _dbContext.Aircrafts.First();

        string suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        Flight flight = new Flight
        {
            FlightNumber = $"RF-{suffix}",
            OriginAirportId = origin.Id,
            DestinationAirportId = destination.Id,
            AircraftId = aircraft.Id,
            DepartureTime = departureTime,
            ArrivalTime = departureTime.AddHours(2),
            Status = flightStatus
        };
        _dbContext.Flights.Add(flight);
        _dbContext.SaveChanges();

        FlightSeat seat = new FlightSeat
        {
            FlightId = flight.Id,
            SeatNumber = "12A",
            SeatClass = SeatClass.Economy,
            State = ticketStatus == TicketStatus.Paid || ticketStatus == TicketStatus.Used
                ? SeatState.Sold
                : SeatState.Available
        };
        _dbContext.FlightSeats.Add(seat);

        Client client = new Client
        {
            FullName = "Иванов Иван Иванович",
            DocumentNumber = $"4500{suffix}"
        };
        _dbContext.Clients.Add(client);

        DateTime baseTime = new DateTime(2026, 11, 1, 8, 0, 0);
        Order order = new Order
        {
            OrderNumber = $"ORD-{suffix}",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            TotalAmount = price,
            Status = ticketStatus == TicketStatus.Paid || ticketStatus == TicketStatus.Used
                ? OrderStatus.Paid
                : OrderStatus.Created,
            CreatedAt = baseTime,
            HoldExpiresAt = baseTime.AddMinutes(15)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        Ticket ticket = new Ticket
        {
            TicketNumber = $"TKT-{suffix}",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = seat.Id,
            Price = price,
            HasBaggage = false,
            Status = ticketStatus
        };
        _dbContext.Tickets.Add(ticket);
        _dbContext.SaveChanges();

        return ticket;
    }

    [Fact]
    public void CalculateRefund_WhenMoreThan24HoursBeforeDeparture_Returns90PercentMatchesTC05()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 2, 20, 0, 0);
        Ticket ticket = CreateTestTicket(departureTime, 5000.00m, TicketStatus.Paid);

        DateTime targetTime = new DateTime(2026, 11, 1, 19, 59, 59);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(ticket.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(ticket.Id, result.Value.TicketId);
        Assert.Equal(ticket.TicketNumber, result.Value.TicketNumber);
        Assert.Equal(5000.00m, result.Value.OriginalPrice);
        Assert.Equal(90, result.Value.RefundPercent);
        Assert.Equal(4500.00m, result.Value.RefundAmount);
        Assert.Equal(500.00m, result.Value.PenaltyAmount);
        Assert.True(result.Value.IsRefundAllowed);
    }

    [Fact]
    public void CalculateRefund_WhenLessThan24HoursBeforeDeparture_Returns50PercentMatchesTC06()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 2, 20, 0, 0);
        Ticket ticket = CreateTestTicket(departureTime, 5000.00m, TicketStatus.Paid);

        DateTime targetTime = new DateTime(2026, 11, 1, 20, 0, 1);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(ticket.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(5000.00m, result.Value.OriginalPrice);
        Assert.Equal(50, result.Value.RefundPercent);
        Assert.Equal(2500.00m, result.Value.RefundAmount);
        Assert.Equal(2500.00m, result.Value.PenaltyAmount);
        Assert.True(result.Value.IsRefundAllowed);
    }

    [Fact]
    public void CalculateRefund_WhenFlightAlreadyDepartedOrTicketUsed_ReturnsFailureMatchesTC07()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 1, 10, 0, 0);
        Ticket ticket = CreateTestTicket(
            departureTime,
            5000.00m,
            TicketStatus.Used,
            FlightStatus.Departed);

        DateTime targetTime = new DateTime(2026, 11, 1, 10, 5, 0);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(ticket.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Возврат билета невозможен: рейс уже вылетел", result.ErrorMessage);
    }

    [Fact]
    public void CalculateRefund_WhenPaidTicketAndDepartureTimePassed_ReturnsFailure()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime targetTime = new DateTime(2026, 11, 1, 10, 5, 0);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        DateTime departureTime = new DateTime(2026, 11, 1, 10, 0, 0);
        Ticket ticket = CreateTestTicket(
            departureTime,
            5000.00m,
            TicketStatus.Paid,
            FlightStatus.Scheduled);

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(ticket.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Возврат билета невозможен: рейс уже вылетел", result.ErrorMessage);
    }

    [Theory]
    [InlineData(TicketStatus.Booked)]
    [InlineData(TicketStatus.Cancelled)]
    [InlineData(TicketStatus.Refunded)]
    public void CalculateRefund_WhenTicketNotPaid_ReturnsFailure(TicketStatus status)
    {
        // Arrange
        (RefundService service, _) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 5, 12, 0, 0);
        Ticket ticket = CreateTestTicket(departureTime, 4000.00m, status);

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(ticket.Id);

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CalculateRefund_WhenFlightCancelled_Returns100PercentForcedRefund()
    {
        // Arrange
        (RefundService service, _) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 2, 20, 0, 0);
        Ticket ticket = CreateTestTicket(
            departureTime,
            6500.00m,
            TicketStatus.Paid,
            FlightStatus.Cancelled);

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(ticket.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(100, result.Value.RefundPercent);
        Assert.Equal(6500.00m, result.Value.RefundAmount);
        Assert.Equal(0m, result.Value.PenaltyAmount);
        Assert.True(result.Value.IsRefundAllowed);
    }

    [Fact]
    public void CalculateRefund_WhenTicketNotFound_ReturnsFailure()
    {
        // Arrange
        (RefundService service, _) = CreateServiceWithClock();

        // Act
        Result<RefundCalculationDto> result = service.CalculateRefund(999999);

        // Assert
        Assert.True(result.IsFailure);
    }
}
