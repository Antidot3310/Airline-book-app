using AirlineTicketOffice.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Tests.Data;

/// <summary>
/// Модульные тесты для проверки инициализации и наполнения базы данных начальными данными (DbInitializer).
/// </summary>
public class DbInitializerTests
{
    [Fact]
    public void Initialize_WhenDatabaseIsEmpty_SeedsRequiredInitialDatasetAndIsIdempotent()
    {
        // Arrange
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AirlineDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new AirlineDbContext(options);
        context.Database.EnsureCreated();

        // Act
        DbInitializer.Initialize(context);

        // Assert
        Assert.Equal(55, context.Airports.Count());
        Assert.Equal(5, context.AircraftTypes.Count());
        Assert.Equal(7, context.Aircrafts.Count());
        Assert.Equal(15, context.Tariffs.Count());
        Assert.Equal(8, context.Flights.Count());
        Assert.True(context.FlightSeats.Any(s => s.Flight != null && s.Flight.FlightNumber == "SU-101"));

        var systemTime = context.SystemTimeStates.Single();
        Assert.Equal(new DateTime(2026, 11, 1, 8, 0, 0), systemTime.CurrentVirtualTime);

        // Act 2 (проверка идемпотентности повторного вызова)
        DbInitializer.Initialize(context);

        // Assert 2
        Assert.Equal(55, context.Airports.Count());
        Assert.Equal(8, context.Flights.Count());
    }
}
