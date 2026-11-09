using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Data;
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
}
