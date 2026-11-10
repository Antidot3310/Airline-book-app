using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса управления расписанием и местами <see cref="FlightService"/> (M4).
/// </summary>
public class FlightServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;

    public FlightServiceTests()
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

    [Fact]
    public void CreateFlight_WithValidData_CreatesFlightAndGeneratesSeats()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var aircraft = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89014"); // Sukhoi Superjet 100 (87 seats)

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-999",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = aircraft.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(5),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(5).AddHours(1).AddMinutes(30)
        };

        // Act
        var result = flightService.CreateFlight(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);

        var createdFlight = _dbContext.Flights
            .Include(f => f.Seats)
            .FirstOrDefault(f => f.Id == result.Value);

        Assert.NotNull(createdFlight);
        Assert.Equal("SU-999", createdFlight.FlightNumber);
        Assert.Equal(FlightStatus.Scheduled, createdFlight.Status);
        Assert.Equal(87, createdFlight.Seats.Count);
        Assert.All(createdFlight.Seats, seat => Assert.Equal(SeatState.Available, seat.State));

        var businessSeats = createdFlight.Seats.Count(s => s.SeatClass == SeatClass.Business);
        var economySeats = createdFlight.Seats.Count(s => s.SeatClass == SeatClass.Economy);
        Assert.Equal(12, businessSeats);
        Assert.Equal(75, economySeats);
    }

    [Fact]
    public void CreateFlight_WithSameOriginAndDestination_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var aircraft = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89014");

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-001",
            OriginAirportId = svo.Id,
            DestinationAirportId = svo.Id,
            AircraftId = aircraft.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(2),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(2).AddHours(1)
        };

        // Act
        var result = flightService.CreateFlight(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("различаться", result.ErrorMessage);
    }

    [Fact]
    public void CreateFlight_WithArrivalBeforeDeparture_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var aircraft = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89014");

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-002",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = aircraft.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(2),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(1)
        };

        // Act
        var result = flightService.CreateFlight(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("позже времени вылета", result.ErrorMessage);
    }

    [Fact]
    public void CreateFlight_WithAircraftInMaintenance_ReturnsFailure_MatchesTC11()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var maintenanceAircraft = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89088"); // Status = Maintenance

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-003",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = maintenanceAircraft.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(2),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(2).AddHours(2)
        };

        // Act
        var result = flightService.CreateFlight(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Самолёт находится на техническом обслуживании и не может быть назначен на рейс.", result.ErrorMessage);
    }

    [Fact]
    public void GetFlightById_ExistingFlight_ReturnsDetailsWithAvailableSeats()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var existingFlight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        // Act
        var result = flightService.GetFlightById(existingFlight.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var details = result.Value;
        Assert.Equal("SU-101", details.FlightNumber);
        Assert.Equal("SVO", details.OriginAirportCode);
        Assert.Equal("LED", details.DestinationAirportCode);
        Assert.Equal("RA-89014", details.AircraftTailNumber);
        Assert.Equal(87, details.TotalSeats);
        Assert.Equal(87, details.AvailableSeats);
        Assert.Equal(FlightStatus.Scheduled, details.Status);
    }

    [Fact]
    public void SearchFlights_MatchingRouteAndDate_ReturnsAvailableFlightsWithMinPrice()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var flightDate = new DateTime(2026, 11, 2);

        // Act
        var results = flightService.SearchFlights(svo.Id, led.Id, flightDate, passengersCount: 1);

        // Assert
        Assert.Single(results);
        var flight = results[0];
        Assert.Equal("SU-101", flight.FlightNumber);
        Assert.Equal("SVO -> LED", flight.Route);
        Assert.True(flight.EconomySeatsAvailable > 0);
        Assert.True(flight.BusinessSeatsAvailable > 0);
        Assert.True(flight.MinPrice == 8100.00m || flight.MinPrice == 4500.00m);
    }

    [Fact]
    public void SearchFlights_WhenFlightIsCancelledOrDeparted_ExcludesFromResults()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var flightDate = new DateTime(2026, 11, 2);

        // Создаём второй рейс на ту же дату, но в статусе Cancelled
        var aircraft = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-73015");
        var cancelledFlight = new Flight
        {
            FlightNumber = "SU-199",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = aircraft.Id,
            DepartureTime = new DateTime(2026, 11, 2, 22, 0, 0),
            ArrivalTime = new DateTime(2026, 11, 2, 23, 30, 0),
            Status = FlightStatus.Cancelled
        };
        _dbContext.Flights.Add(cancelledFlight);
        _dbContext.SaveChanges();

        // Act
        var results = flightService.SearchFlights(svo.Id, led.Id, flightDate, passengersCount: 1);

        // Assert
        Assert.Single(results);
        Assert.Equal("SU-101", results[0].FlightNumber);
        Assert.Equal(FlightStatus.Scheduled, results[0].Status);
    }

    [Fact]
    public void SearchFlights_WhenNotEnoughSeatsForPassengersCount_ExcludesFromResults()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var flightDate = new DateTime(2026, 11, 2);

        // Act (у рейса SU-101 всего 87 мест, запрашиваем 100)
        var results = flightService.SearchFlights(svo.Id, led.Id, flightDate, passengersCount: 100);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void SearchFlights_OnDifferentDate_ReturnsEmptyList()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var differentDate = new DateTime(2026, 12, 1);

        // Act
        var results = flightService.SearchFlights(svo.Id, led.Id, differentDate, passengersCount: 1);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ChangeAircraft_WhenNoTicketsSold_ReplacesSeatsGrid_MatchesTC09()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var ssj = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89014"); // SSJ-100 (87 seats)
        var a320 = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-73015"); // A320 (150 seats)

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-701",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = ssj.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(5),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(5).AddHours(2)
        };
        var createResult = flightService.CreateFlight(request);
        Assert.True(createResult.IsSuccess);
        int flightId = createResult.Value;

        // Act
        var result = flightService.ChangeAircraft(flightId, a320.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedFlight = _dbContext.Flights.Include(f => f.Seats).First(f => f.Id == flightId);
        Assert.Equal(a320.Id, updatedFlight.AircraftId);
        Assert.Equal(150, updatedFlight.Seats.Count);
        Assert.All(updatedFlight.Seats, s => Assert.Equal(SeatState.Available, s.State));
    }

    [Fact]
    public void ChangeAircraft_WhenTicketsExist_ReturnsFailure_MatchesTC10()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var ssj = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89014");
        var a320 = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-73015");

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-702",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = ssj.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(5),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(5).AddHours(2)
        };
        var createResult = flightService.CreateFlight(request);
        Assert.True(createResult.IsSuccess);
        int flightId = createResult.Value;

        // Добавляем оплаченный билет на рейс (кейс TC-10)
        var client = new Client { FullName = "Иван Иванов", DocumentNumber = "1234 567890" };
        _dbContext.Clients.Add(client);
        var order = new Order
        {
            OrderNumber = "ORD-TC10",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            CreatedAt = clockService.CurrentVirtualTime,
            HoldExpiresAt = clockService.CurrentVirtualTime.AddMinutes(15),
            Status = OrderStatus.Paid,
            TotalAmount = 4500.00m
        };
        _dbContext.Orders.Add(order);

        var seat = _dbContext.FlightSeats.First(s => s.FlightId == flightId);
        seat.State = SeatState.Sold;

        var ticket = new Ticket
        {
            TicketNumber = "TKT-TC10",
            OrderId = order.Id,
            Order = order,
            ClientId = client.Id,
            Client = client,
            FlightId = flightId,
            FlightSeatId = seat.Id,
            FlightSeat = seat,
            Price = 4500.00m,
            HasBaggage = false,
            Status = TicketStatus.Paid
        };
        _dbContext.Tickets.Add(ticket);
        _dbContext.SaveChanges();

        // Act
        var result = flightService.ChangeAircraft(flightId, a320.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Смена воздушного судна запрещена: на рейс уже проданы билеты.", result.ErrorMessage);
    }

    [Fact]
    public void ChangeAircraft_WhenNewAircraftInMaintenance_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var ssj = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89014");
        var maintenanceAircraft = _dbContext.Aircrafts.First(a => a.TailNumber == "RA-89088"); // Maintenance

        var request = new CreateFlightRequest
        {
            FlightNumber = "SU-703",
            OriginAirportId = svo.Id,
            DestinationAirportId = led.Id,
            AircraftId = ssj.Id,
            DepartureTime = clockService.CurrentVirtualTime.AddDays(5),
            ArrivalTime = clockService.CurrentVirtualTime.AddDays(5).AddHours(2)
        };
        var createResult = flightService.CreateFlight(request);
        int flightId = createResult.Value;

        // Act
        var result = flightService.ChangeAircraft(flightId, maintenanceAircraft.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Самолёт находится на техническом обслуживании и не может быть назначен на рейс.", result.ErrorMessage);
    }

    [Fact]
    public void CancelFlight_ScheduledFlight_CancelsAndBlocksAllSeats()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");
        Assert.Equal(FlightStatus.Scheduled, flight.Status);

        // Act
        var result = flightService.CancelFlight(flight.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var updatedFlight = _dbContext.Flights.Include(f => f.Seats).First(f => f.Id == flight.Id);
        Assert.Equal(FlightStatus.Cancelled, updatedFlight.Status);
        Assert.All(updatedFlight.Seats, s => Assert.Equal(SeatState.Blocked, s.State));
    }

    [Fact]
    public void CancelFlight_DepartedFlight_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var flightService = new FlightService(_dbContext, clockService, tariffCalculator, auditService);

        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-102");
        flight.Status = FlightStatus.Departed;
        _dbContext.SaveChanges();

        // Act
        var result = flightService.CancelFlight(flight.Id);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Невозможно отменить рейс: рейс уже вылетел.", result.ErrorMessage);
    }
}
