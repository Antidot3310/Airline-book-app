using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Booking;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса бронирования и управления картой мест <see cref="BookingService"/> (M6).
/// </summary>
public class BookingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;

    public BookingServiceTests()
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
    public void GetSeatMap_WhenFlightDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);
        int nonExistentFlightId = 999999;

        // Act
        var result = bookingService.GetSeatMap(nonExistentFlightId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("не найден", result.ErrorMessage);
    }

    [Fact]
    public void GetSeatMap_WhenFlightExists_ReturnsFullSeatMapWithCalculatedPrices()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        // Act
        var result = bookingService.GetSeatMap(flight.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var seatMap = result.Value;
        Assert.NotNull(seatMap);
        Assert.Equal(flight.Id, seatMap.FlightId);
        Assert.Equal("SU-101", seatMap.FlightNumber);
        Assert.Equal("Sukhoi Superjet 100", seatMap.AircraftModel);
        Assert.Equal(87, seatMap.Seats.Count);

        var businessSeats = seatMap.Seats.Where(s => s.Class == SeatClass.Business).ToList();
        var economySeats = seatMap.Seats.Where(s => s.Class == SeatClass.Economy).ToList();

        Assert.Equal(12, businessSeats.Count);
        Assert.Equal(75, economySeats.Count);

        // SU-101 вылетает через 36 ч (K_days = 1.8), базовый тариф SVO->LED = 4500 руб.
        // Эконом: 4500 * 1.0 * 1.8 = 8100.00 руб.
        // Бизнес: 4500 * 2.0 * 1.8 = 16200.00 руб.
        Assert.All(businessSeats, s => Assert.Equal(16200.00m, s.Price));
        Assert.All(economySeats, s => Assert.Equal(8100.00m, s.Price));

        // Проверка корректности сортировки мест: по номеру ряда, затем букве (1A .. 18F)
        Assert.Equal("1A", seatMap.Seats.First().SeatNumber);
        Assert.Equal("18F", seatMap.Seats.Last().SeatNumber);
    }

    [Fact]
    public void GetSeatMap_WhenSeatHoldExpired_ReturnsSeatAsAvailable()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");
        var expiredSeat = flight.Seats.First(s => s.SeatNumber == "4A");
        var activeHeldSeat = flight.Seats.First(s => s.SeatNumber == "4C");

        expiredSeat.State = SeatState.Held;
        expiredSeat.HoldUntilTime = clockService.CurrentVirtualTime.AddMinutes(-5); // Срок удержания истёк

        activeHeldSeat.State = SeatState.Held;
        activeHeldSeat.HoldUntilTime = clockService.CurrentVirtualTime.AddMinutes(10); // Удержание всё ещё активно

        _dbContext.SaveChanges();

        // Act
        var result = bookingService.GetSeatMap(flight.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var expiredSeatDto = result.Value.Seats.First(s => s.SeatNumber == "4A");
        var activeHeldSeatDto = result.Value.Seats.First(s => s.SeatNumber == "4C");

        Assert.Equal(SeatState.Available, expiredSeatDto.State);
        Assert.Equal(SeatState.Held, activeHeldSeatDto.State);
    }

    [Fact]
    public void GetSeatMap_WhenSeatsAreSoldOrBlocked_PreservesTheirStatuses()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");
        var soldSeat = flight.Seats.First(s => s.SeatNumber == "1A");
        var blockedSeat = flight.Seats.First(s => s.SeatNumber == "1C");

        soldSeat.State = SeatState.Sold;
        blockedSeat.State = SeatState.Blocked;

        _dbContext.SaveChanges();

        // Act
        var result = bookingService.GetSeatMap(flight.Id);

        // Assert
        Assert.True(result.IsSuccess);
        var soldSeatDto = result.Value.Seats.First(s => s.SeatNumber == "1A");
        var blockedSeatDto = result.Value.Seats.First(s => s.SeatNumber == "1C");

        Assert.Equal(SeatState.Sold, soldSeatDto.State);
        Assert.Equal(SeatState.Blocked, blockedSeatDto.State);
    }

    [Fact]
    public void HoldSeats_WhenAllSeatsAvailable_HoldsSeatsFor15Minutes()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        var seatNumbers = new[] { "1A", "1C" };
        var expectedHoldUntil = clockService.CurrentVirtualTime.AddMinutes(15);

        // Act
        var result = bookingService.HoldSeats(flight.Id, seatNumbers);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(flight.Id, result.Value.FlightId);
        Assert.Equal(seatNumbers, result.Value.SeatNumbers);
        Assert.Equal(expectedHoldUntil, result.Value.HoldUntilTime);

        // Проверяем состояние в БД
        var seatsInDb = _dbContext.FlightSeats
            .Where(s => s.FlightId == flight.Id && seatNumbers.Contains(s.SeatNumber))
            .ToList();

        Assert.Equal(2, seatsInDb.Count);
        Assert.All(seatsInDb, s =>
        {
            Assert.Equal(SeatState.Held, s.State);
            Assert.Equal(expectedHoldUntil, s.HoldUntilTime);
        });
    }

    [Fact]
    public void HoldSeats_WhenAnySeatIsAlreadyHeldOrSold_FailsAndDoesNotHoldAnySeatMatchesTC03()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");
        var seat1A = flight.Seats.First(s => s.SeatNumber == "1A");
        var seat1C = flight.Seats.First(s => s.SeatNumber == "1C");

        // 1A свободно, 1C уже удерживается другим пользователем
        seat1A.State = SeatState.Available;
        seat1A.HoldUntilTime = null;
        seat1C.State = SeatState.Held;
        seat1C.HoldUntilTime = clockService.CurrentVirtualTime.AddMinutes(10);
        _dbContext.SaveChanges();

        // Act - попытка занять оба места (принцип "все или ни одного", TC-03)
        var result = bookingService.HoldSeats(flight.Id, new[] { "1A", "1C" });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Одно или несколько выбранных мест уже заняты.", result.ErrorMessage);

        // Место 1A обязано остаться Available
        var fresh1A = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == seat1A.Id);
        Assert.Equal(SeatState.Available, fresh1A.State);
        Assert.Null(fresh1A.HoldUntilTime);
    }

    [Fact]
    public void HoldSeats_WhenSeatHoldExpired_AllowsReHoldingSeat()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");
        var seat1A = flight.Seats.First(s => s.SeatNumber == "1A");

        // Место было удержано в прошлом, срок истек
        seat1A.State = SeatState.Held;
        seat1A.HoldUntilTime = clockService.CurrentVirtualTime.AddMinutes(-10);
        _dbContext.SaveChanges();

        // Act
        var result = bookingService.HoldSeats(flight.Id, new[] { "1A" });

        // Assert
        Assert.True(result.IsSuccess);
        var expectedHoldUntil = clockService.CurrentVirtualTime.AddMinutes(15);
        Assert.Equal(expectedHoldUntil, result.Value.HoldUntilTime);

        var fresh1A = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == seat1A.Id);
        Assert.Equal(SeatState.Held, fresh1A.State);
        Assert.Equal(expectedHoldUntil, fresh1A.HoldUntilTime);
    }

    [Fact]
    public void HoldSeats_WhenExceedsPassengerLimit_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        var sixSeats = new[] { "1A", "1C", "1D", "1F", "2A", "2C" };

        // Act
        var result = bookingService.HoldSeats(flight.Id, sixSeats);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("от 1 до 5 мест", result.ErrorMessage);
    }

    [Fact]
    public void HoldSeats_WhenFlightCancelledOrDeparted_ReturnsFailure()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        flight.Status = FlightStatus.Cancelled;
        _dbContext.SaveChanges();

        // Act
        var result = bookingService.HoldSeats(flight.Id, new[] { "1A" });

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("запланированных рейсов", result.ErrorMessage);
    }

    [Fact]
    public void ReleaseSeats_WhenSeatsAreHeld_ReleasesThemToAvailable()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);
        var bookingService = new BookingService(_dbContext, tariffCalculator, clockService);

        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");
        var seat1A = flight.Seats.First(s => s.SeatNumber == "1A");
        var seat1C = flight.Seats.First(s => s.SeatNumber == "1C");

        seat1A.State = SeatState.Held;
        seat1A.HoldUntilTime = clockService.CurrentVirtualTime.AddMinutes(15);
        seat1C.State = SeatState.Held;
        seat1C.HoldUntilTime = clockService.CurrentVirtualTime.AddMinutes(15);
        _dbContext.SaveChanges();

        // Act
        var result = bookingService.ReleaseSeats(flight.Id, new[] { "1A", "1C" });

        // Assert
        Assert.True(result.IsSuccess);

        var fresh1A = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == seat1A.Id);
        var fresh1C = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == seat1C.Id);

        Assert.Equal(SeatState.Available, fresh1A.State);
        Assert.Null(fresh1A.HoldUntilTime);
        Assert.Equal(SeatState.Available, fresh1C.State);
        Assert.Null(fresh1C.HoldUntilTime);
    }
}
