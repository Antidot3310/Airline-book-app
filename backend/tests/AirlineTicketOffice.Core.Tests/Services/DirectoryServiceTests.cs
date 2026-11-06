using System.Linq;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса справочников <see cref="DirectoryService"/>.
/// </summary>
public class DirectoryServiceTests
{
    private static AirlineDbContext CreateInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AirlineDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AirlineDbContext(options);
        context.Database.EnsureCreated();
        DbInitializer.Initialize(context);

        return context;
    }

    [Fact]
    public void GetAllAirports_ReturnsSeededAirports()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        // Act
        var airports = directoryService.GetAllAirports();

        // Assert
        Assert.Equal(55, airports.Count);
        Assert.Contains(airports, a => a.Code == "SVO");
        Assert.Contains(airports, a => a.Code == "LED");
    }

    [Fact]
    public void AddAirport_WithUniqueCode_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        var newAirport = new AirportDto
        {
            Code = "tst",
            City = "Тестоград",
            Name = "Тестовый Аэропорт",
            IsActive = true
        };

        // Act
        var result = directoryService.AddAirport(newAirport);

        // Assert
        Assert.True(result.IsSuccess);

        var airports = directoryService.GetAllAirports();
        var added = airports.FirstOrDefault(a => a.Code == "TST");
        Assert.NotNull(added);
        Assert.Equal("Тестоград", added.City);
        Assert.Equal("Тестовый Аэропорт", added.Name);

        var logs = auditService.GetLogs(null, null, null, "AddAirport");
        Assert.Single(logs);
        Assert.Equal("Airport", logs[0].EntityName);
    }

    [Fact]
    public void AddAirport_WithDuplicateCode_ReturnsFailure()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        var duplicateAirport = new AirportDto
        {
            Code = "svo", // В нижнем регистре, должен быть распознан как дубликат SVO
            City = "Москва",
            Name = "Второй Шереметьево",
            IsActive = true
        };

        // Act
        var result = directoryService.AddAirport(duplicateAirport);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("уже зарегистрирован", result.ErrorMessage);
    }

    [Fact]
    public void DeactivateAirport_WhenUsedInScheduledFlight_ReturnsFailure()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        // SVO задействован в запланированном рейсе SU-101
        var svo = context.Airports.First(a => a.Code == "SVO");

        // Act
        var result = directoryService.DeactivateAirport(svo.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("задействован в запланированных рейсах", result.ErrorMessage);

        // Проверяем, что аэропорт остался активным
        var airportAfter = context.Airports.First(a => a.Id == svo.Id);
        Assert.True(airportAfter.IsActive);
    }

    [Fact]
    public void DeactivateAirport_WhenNotUsedInScheduledFlights_Succeeds()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        // Добавим отдельный тестовый аэропорт, не привязанный к рейсам
        var testAirport = new AirportDto
        {
            Code = "ZZZ",
            City = "Удалённый",
            Name = "Аэропорт Без Рейсов"
        };
        var addResult = directoryService.AddAirport(testAirport);
        Assert.True(addResult.IsSuccess);

        var addedAirport = context.Airports.First(a => a.Code == "ZZZ");

        // Act
        var result = directoryService.DeactivateAirport(addedAirport.Id);

        // Assert
        Assert.True(result.IsSuccess);

        var airportAfter = context.Airports.First(a => a.Id == addedAirport.Id);
        Assert.False(airportAfter.IsActive);

        var logs = auditService.GetLogs(null, null, null, "DeactivateAirport");
        Assert.Single(logs);
    }

    [Fact]
    public void AddAircraft_WithDuplicateTailNumber_ReturnsFailure()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        var existingAircraft = context.Aircrafts.First();

        var duplicateAircraft = new AircraftDto
        {
            TailNumber = existingAircraft.TailNumber.ToLowerInvariant(),
            AircraftTypeId = existingAircraft.AircraftTypeId
        };

        // Act
        var result = directoryService.AddAircraft(duplicateAircraft);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("уже существует", result.ErrorMessage);
    }

    [Fact]
    public void SetAircraftStatus_ChangesStatusAndLogsAudit()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var directoryService = new DirectoryService(context, auditService);

        var aircraft = context.Aircrafts.First(a => a.Status == AircraftStatus.InService);

        // Act
        var result = directoryService.SetAircraftStatus(aircraft.Id, AircraftStatus.Maintenance);

        // Assert
        Assert.True(result.IsSuccess);

        var aircraftAfter = context.Aircrafts.First(a => a.Id == aircraft.Id);
        Assert.Equal(AircraftStatus.Maintenance, aircraftAfter.Status);

        var logs = auditService.GetLogs(null, null, null, "SetAircraftStatus");
        Assert.Single(logs);
        Assert.Contains("Maintenance", logs[0].Description);
    }
}
