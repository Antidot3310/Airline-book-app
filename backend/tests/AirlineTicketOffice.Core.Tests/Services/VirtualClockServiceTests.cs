using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса виртуального системного времени <see cref="VirtualClockService"/>.
/// </summary>
public class VirtualClockServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;

    public VirtualClockServiceTests()
    {
        var services = new ServiceCollection();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        using (var scope = _serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
            db.Database.EnsureCreated();
            DbInitializer.Initialize(db);
        }

        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void AdvanceTime_By15Minutes_IncreasesCurrentVirtualTimeAndSavesToDb()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var initialTime = new DateTime(2026, 11, 1, 8, 0, 0);
        Assert.Equal(initialTime, clockService.CurrentVirtualTime);

        // Act
        clockService.AdvanceTime(TimeSpan.FromMinutes(15));

        // Assert
        var expectedTime = new DateTime(2026, 11, 1, 8, 15, 0);
        Assert.Equal(expectedTime, clockService.CurrentVirtualTime);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
        var state = db.SystemTimeStates.Single();
        Assert.Equal(expectedTime, state.CurrentVirtualTime);
    }

    [Fact]
    public void AdvanceTime_By1Day_IncreasesCurrentVirtualTimeAndFiresEvent()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        VirtualTimeChangedEventArgs? capturedArgs = null;
        clockService.TimeChanged += (sender, args) => capturedArgs = args;

        // Act
        clockService.AdvanceTime(TimeSpan.FromDays(1));

        // Assert
        var expectedTime = new DateTime(2026, 11, 2, 8, 0, 0);
        Assert.Equal(expectedTime, clockService.CurrentVirtualTime);

        Assert.NotNull(capturedArgs);
        Assert.Equal(new DateTime(2026, 11, 1, 8, 0, 0), capturedArgs.OldVirtualTime);
        Assert.Equal(expectedTime, capturedArgs.NewVirtualTime);
    }

    [Fact]
    public void SetMode_AndResetToRealTime_UpdatesStateAndPersistsToDb()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);

        // Act 1: Установка ускоренного режима
        clockService.SetMode(ClockMode.Accelerated, 10.0);

        // Assert 1
        Assert.Equal(ClockMode.Accelerated, clockService.Mode);
        Assert.Equal(10.0, clockService.SpeedMultiplier);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
            var state = db.SystemTimeStates.Single();
            Assert.Equal(ClockMode.Accelerated, state.Mode);
            Assert.Equal(10.0, state.SpeedMultiplier);
        }

        // Act 2: Сброс на реальное время
        VirtualTimeChangedEventArgs? resetArgs = null;
        clockService.TimeChanged += (sender, args) => resetArgs = args;
        var beforeReset = DateTime.Now.AddSeconds(-2);

        clockService.ResetToRealTime();

        var afterReset = DateTime.Now.AddSeconds(2);

        // Assert 2
        Assert.Equal(ClockMode.RealTime, clockService.Mode);
        Assert.Equal(1.0, clockService.SpeedMultiplier);
        Assert.InRange(clockService.CurrentVirtualTime, beforeReset, afterReset);

        Assert.NotNull(resetArgs);
        Assert.InRange(resetArgs.NewVirtualTime, beforeReset, afterReset);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
            var state = db.SystemTimeStates.Single();
            Assert.Equal(ClockMode.RealTime, state.Mode);
            Assert.Equal(1.0, state.SpeedMultiplier);
            Assert.InRange(state.CurrentVirtualTime, beforeReset, afterReset);
        }
    }

    [Fact]
    public void TriggerScheduledJobs_WhenHoldExpired_ReleasesSeatsAndMarksOrderExpired()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var t0 = clockService.CurrentVirtualTime;

        int seatId;
        int orderId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
            var flight = db.Flights.First();
            var seat = db.FlightSeats.First(s => s.FlightId == flight.Id);
            seat.State = SeatState.Held;
            seat.HoldUntilTime = t0.AddMinutes(15);
            seatId = seat.Id;

            var client = new Client
            {
                FullName = "Тестовый Пассажир",
                DocumentNumber = "1111 222333"
            };
            db.Clients.Add(client);

            var order = new Order
            {
                OrderNumber = "ORD-EXPIRE-01",
                Channel = OrderChannel.SelfService,
                CreatedRole = UserRole.Passenger,
                TotalAmount = 4500m,
                Status = OrderStatus.Created,
                CreatedAt = t0,
                HoldExpiresAt = t0.AddMinutes(15)
            };
            db.Orders.Add(order);

            var ticket = new Ticket
            {
                TicketNumber = "TKT-EXPIRE-01",
                Order = order,
                Client = client,
                FlightId = flight.Id,
                FlightSeatId = seat.Id,
                Price = 4500m,
                HasBaggage = false,
                Status = TicketStatus.Booked
            };
            db.Tickets.Add(ticket);

            db.SaveChanges();
            orderId = order.Id;
        }

        HoldsExpiredEventArgs? capturedHoldsEvent = null;
        clockService.HoldsExpired += (s, e) => capturedHoldsEvent = e;

        // Act
        clockService.AdvanceTime(TimeSpan.FromMinutes(16));

        // Assert
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            var seat = db.FlightSeats.Find(seatId);
            Assert.NotNull(seat);
            Assert.Equal(SeatState.Available, seat.State);
            Assert.Null(seat.HoldUntilTime);

            var order = db.Orders.Include(o => o.Tickets).Single(o => o.Id == orderId);
            Assert.Equal(OrderStatus.Expired, order.Status);
            Assert.Single(order.Tickets);
            Assert.Equal(TicketStatus.Cancelled, order.Tickets.First().Status);

            var audit = db.AuditLogs.FirstOrDefault(a => a.ActionType == "ExpireHold");
            Assert.NotNull(audit);
            Assert.Contains("Заказов: 1", audit.Description);
            Assert.Contains("освобождено мест: 1", audit.Description);
        }

        Assert.NotNull(capturedHoldsEvent);
        Assert.Equal(1, capturedHoldsEvent.ExpiredOrdersCount);
        Assert.Equal(1, capturedHoldsEvent.ReleasedSeatsCount);
    }

    [Fact]
    public void TriggerScheduledJobs_WhenFlightDepartureTimeReached_MarksFlightDepartedAndTicketsUsed()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var t0 = clockService.CurrentVirtualTime;

        int flightId;
        int ticketId;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            var flight = new Flight
            {
                FlightNumber = "SU-DEPART-01",
                OriginAirportId = 1,
                DestinationAirportId = 2,
                AircraftId = 1,
                DepartureTime = t0.AddHours(2),
                ArrivalTime = t0.AddHours(4),
                Status = FlightStatus.Scheduled
            };
            db.Flights.Add(flight);
            db.SaveChanges();
            flightId = flight.Id;

            var seat = new FlightSeat
            {
                FlightId = flight.Id,
                SeatNumber = "1A",
                SeatClass = SeatClass.Business,
                State = SeatState.Sold
            };
            db.FlightSeats.Add(seat);

            var client = new Client
            {
                FullName = "Вылетающий Пассажир",
                DocumentNumber = "9999 888777"
            };
            db.Clients.Add(client);

            var order = new Order
            {
                OrderNumber = "ORD-DEPART-01",
                Channel = OrderChannel.Cashier,
                CreatedRole = UserRole.Cashier,
                TotalAmount = 7000m,
                Status = OrderStatus.Paid,
                CreatedAt = t0,
                HoldExpiresAt = t0.AddMinutes(15)
            };
            db.Orders.Add(order);

            var ticket = new Ticket
            {
                TicketNumber = "TKT-DEPART-01",
                Order = order,
                Client = client,
                FlightId = flight.Id,
                FlightSeat = seat,
                Price = 7000m,
                HasBaggage = true,
                Status = TicketStatus.Paid
            };
            db.Tickets.Add(ticket);

            db.SaveChanges();
            ticketId = ticket.Id;
        }

        var capturedFlightStatusEvents = new List<FlightStatusChangedEventArgs>();
        clockService.FlightStatusChanged += (s, e) => capturedFlightStatusEvents.Add(e);

        // Act
        clockService.AdvanceTime(TimeSpan.FromHours(3));

        // Assert
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            var flight = db.Flights.Find(flightId);
            Assert.NotNull(flight);
            Assert.Equal(FlightStatus.Departed, flight.Status);

            var ticket = db.Tickets.Find(ticketId);
            Assert.NotNull(ticket);
            Assert.Equal(TicketStatus.Used, ticket.Status);

            var audit = db.AuditLogs.FirstOrDefault(a => a.ActionType == "FlightDeparted" && a.EntityId == flightId);
            Assert.NotNull(audit);
            Assert.Contains("SU-DEPART-01", audit.Description);
            Assert.Contains("Использовано билетов: 1", audit.Description);
        }

        Assert.Contains(capturedFlightStatusEvents, e => e.FlightId == flightId && e.NewStatus == FlightStatus.Departed);
    }
}
