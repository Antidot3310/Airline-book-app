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

    private (Order Order, List<Ticket> Tickets) CreateTestOrderWithTickets(
        DateTime departureTime,
        int ticketCount,
        decimal pricePerTicket,
        TicketStatus ticketStatus = TicketStatus.Paid,
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

        DateTime baseTime = new DateTime(2026, 11, 1, 8, 0, 0);
        Order order = new Order
        {
            OrderNumber = $"ORD-{suffix}",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            TotalAmount = pricePerTicket * ticketCount,
            Status = ticketStatus == TicketStatus.Paid || ticketStatus == TicketStatus.Used
                ? OrderStatus.Paid
                : OrderStatus.Created,
            CreatedAt = baseTime,
            HoldExpiresAt = baseTime.AddMinutes(15)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        List<Ticket> tickets = new List<Ticket>();
        for (int i = 0; i < ticketCount; i++)
        {
            FlightSeat seat = new FlightSeat
            {
                FlightId = flight.Id,
                SeatNumber = $"{i + 1}A",
                SeatClass = SeatClass.Economy,
                State = ticketStatus == TicketStatus.Paid || ticketStatus == TicketStatus.Used
                    ? SeatState.Sold
                    : SeatState.Available
            };
            _dbContext.FlightSeats.Add(seat);

            Client client = new Client
            {
                FullName = $"Пассажир {i + 1}",
                DocumentNumber = $"4500{suffix}{i}"
            };
            _dbContext.Clients.Add(client);
            _dbContext.SaveChanges();

            Ticket ticket = new Ticket
            {
                TicketNumber = $"TKT-{suffix}-{i + 1}",
                OrderId = order.Id,
                ClientId = client.Id,
                FlightId = flight.Id,
                FlightSeatId = seat.Id,
                Price = pricePerTicket,
                HasBaggage = false,
                Status = ticketStatus
            };
            _dbContext.Tickets.Add(ticket);
            tickets.Add(ticket);
        }
        _dbContext.SaveChanges();

        return (order, tickets);
    }

    private Ticket CreateTestTicket(
        DateTime departureTime,
        decimal price,
        TicketStatus ticketStatus,
        FlightStatus flightStatus = FlightStatus.Scheduled)
    {
        return CreateTestOrderWithTickets(departureTime, 1, price, ticketStatus, flightStatus).Tickets[0];
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

    [Fact]
    public void ProcessRefund_WhenSingleTicketInOrder_SetsTicketRefundedSeatAvailableAndOrderRefunded()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 3, 10, 0, 0);
        Ticket ticket = CreateTestTicket(departureTime, 5000.00m, TicketStatus.Paid);

        DateTime targetTime = departureTime.AddHours(-30);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundResultDto> result = service.ProcessRefund(ticket.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(90, result.Value.RefundPercent);
        Assert.Equal(4500.00m, result.Value.RefundAmount);
        Assert.Equal(500.00m, result.Value.PenaltyAmount);
        Assert.Equal(OrderStatus.Refunded, result.Value.UpdatedOrderStatus);
        Assert.Equal(RefundReason.Voluntary, result.Value.Reason);

        Ticket updatedTicket = _dbContext.Tickets.AsNoTracking().First(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.Refunded, updatedTicket.Status);

        FlightSeat updatedSeat = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == ticket.FlightSeatId);
        Assert.Equal(SeatState.Available, updatedSeat.State);
        Assert.Null(updatedSeat.HoldUntilTime);

        Order updatedOrder = _dbContext.Orders.AsNoTracking().First(o => o.Id == ticket.OrderId);
        Assert.Equal(OrderStatus.Refunded, updatedOrder.Status);

        Refund refundRecord = _dbContext.Refunds.AsNoTracking().First(r => r.TicketId == ticket.Id);
        Assert.Equal(4500.00m, refundRecord.Amount);
        Assert.Equal(90, refundRecord.RefundPercent);
        Assert.Equal(RefundReason.Voluntary, refundRecord.Reason);

        bool auditExists = _dbContext.AuditLogs.AsNoTracking().Any(a => a.ActionType == "ProcessRefund" && a.EntityId == ticket.Id);
        Assert.True(auditExists);
    }

    [Fact]
    public void ProcessRefund_WhenOneOfMultipleTicketsRefunded_SetsOrderPartiallyRefunded()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 3, 10, 0, 0);
        (Order order, List<Ticket> tickets) = CreateTestOrderWithTickets(departureTime, 2, 5000.00m, TicketStatus.Paid);

        DateTime targetTime = departureTime.AddHours(-30);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundResultDto> result = service.ProcessRefund(tickets[0].Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.PartiallyRefunded, result.Value.UpdatedOrderStatus);

        Ticket refundedTicket = _dbContext.Tickets.AsNoTracking().First(t => t.Id == tickets[0].Id);
        Assert.Equal(TicketStatus.Refunded, refundedTicket.Status);
        FlightSeat refundedSeat = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == tickets[0].FlightSeatId);
        Assert.Equal(SeatState.Available, refundedSeat.State);

        Ticket remainingTicket = _dbContext.Tickets.AsNoTracking().First(t => t.Id == tickets[1].Id);
        Assert.Equal(TicketStatus.Paid, remainingTicket.Status);
        FlightSeat remainingSeat = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == tickets[1].FlightSeatId);
        Assert.Equal(SeatState.Sold, remainingSeat.State);

        Order updatedOrder = _dbContext.Orders.AsNoTracking().First(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.PartiallyRefunded, updatedOrder.Status);
    }

    [Fact]
    public void ProcessRefund_WhenSecondTicketRefundedAfterPartial_SetsOrderFullyRefunded()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 3, 10, 0, 0);
        (Order order, List<Ticket> tickets) = CreateTestOrderWithTickets(departureTime, 2, 5000.00m, TicketStatus.Paid);

        DateTime targetTime = departureTime.AddHours(-30);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        Result<RefundResultDto> firstResult = service.ProcessRefund(tickets[0].Id);
        Assert.True(firstResult.IsSuccess);

        // Act
        Result<RefundResultDto> secondResult = service.ProcessRefund(tickets[1].Id);

        // Assert
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(OrderStatus.Refunded, secondResult.Value.UpdatedOrderStatus);

        Ticket secondTicket = _dbContext.Tickets.AsNoTracking().First(t => t.Id == tickets[1].Id);
        Assert.Equal(TicketStatus.Refunded, secondTicket.Status);
        FlightSeat secondSeat = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == tickets[1].FlightSeatId);
        Assert.Equal(SeatState.Available, secondSeat.State);

        Order updatedOrder = _dbContext.Orders.AsNoTracking().First(o => o.Id == order.Id);
        Assert.Equal(OrderStatus.Refunded, updatedOrder.Status);
    }

    [Fact]
    public void ProcessRefund_WhenFlightAlreadyDeparted_ReturnsFailure()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 1, 10, 0, 0);
        Ticket ticket = CreateTestTicket(departureTime, 5000.00m, TicketStatus.Paid, FlightStatus.Departed);

        DateTime targetTime = new DateTime(2026, 11, 1, 10, 5, 0);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundResultDto> result = service.ProcessRefund(ticket.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Возврат билета невозможен: рейс уже вылетел", result.ErrorMessage);

        Ticket unchangedTicket = _dbContext.Tickets.AsNoTracking().First(t => t.Id == ticket.Id);
        Assert.Equal(TicketStatus.Paid, unchangedTicket.Status);
        FlightSeat unchangedSeat = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == ticket.FlightSeatId);
        Assert.Equal(SeatState.Sold, unchangedSeat.State);

        bool refundRecordExists = _dbContext.Refunds.AsNoTracking().Any(r => r.TicketId == ticket.Id);
        Assert.False(refundRecordExists);
    }

    [Fact]
    public void ProcessRefund_Within24Hours_Applies50PercentRefund()
    {
        // Arrange
        (RefundService service, VirtualClockService clockService) = CreateServiceWithClock();
        DateTime departureTime = new DateTime(2026, 11, 2, 20, 0, 0);
        Ticket ticket = CreateTestTicket(departureTime, 5000.00m, TicketStatus.Paid);

        DateTime targetTime = departureTime.AddHours(-12);
        clockService.AdvanceTime(targetTime - clockService.CurrentVirtualTime);

        // Act
        Result<RefundResultDto> result = service.ProcessRefund(ticket.Id);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(50, result.Value.RefundPercent);
        Assert.Equal(2500.00m, result.Value.RefundAmount);
        Assert.Equal(2500.00m, result.Value.PenaltyAmount);
        Assert.Equal(OrderStatus.Refunded, result.Value.UpdatedOrderStatus);
    }
}
