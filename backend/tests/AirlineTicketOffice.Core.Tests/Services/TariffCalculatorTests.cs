using System;
using System.Linq;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для калькулятора динамических тарифов <see cref="TariffCalculator"/> (M5).
/// </summary>
public class TariffCalculatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;

    public TariffCalculatorTests()
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
    public void CalculateTicketPrice_MoreThan30DaysEconomyNoBaggage_MatchesTC12()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var calculator = new TariffCalculator(_dbContext, clockService, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var departureTime = clockService.CurrentVirtualTime.AddDays(35);

        // Act
        var price = calculator.CalculateTicketPrice(svo.Id, led.Id, SeatClass.Economy, departureTime, hasBaggage: false);

        // Assert
        Assert.Equal(4500.00m, price);
    }

    [Fact]
    public void CalculateTicketPrice_LessThan7DaysBusinessWithBaggage_MatchesTC13()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var calculator = new TariffCalculator(_dbContext, clockService, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var departureTime = clockService.CurrentVirtualTime.AddDays(3);

        // Act
        var price = calculator.CalculateTicketPrice(svo.Id, led.Id, SeatClass.Business, departureTime, hasBaggage: true);

        // Assert: 4500 * 2.0 (Business) * 1.8 (< 7 days) + 1500 (Baggage) = 16200 + 1500 = 17700.00
        Assert.Equal(17700.00m, price);
    }

    [Fact]
    public void GetAllTariffs_ReturnsSeededTariffs()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var calculator = new TariffCalculator(_dbContext, clockService, auditService);

        // Act
        var tariffs = calculator.GetAllTariffs();

        // Assert
        Assert.Equal(15, tariffs.Count);
        Assert.Contains(tariffs, t => t.OriginAirportCode == "SVO" && t.DestinationAirportCode == "LED" && t.BasePrice == 4500.00m && t.BaggageFee == 1500.00m);
    }

    [Fact]
    public void SetBaseTariff_UpdatesExistingTariff_AndLogsAudit()
    {
        // Arrange
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var calculator = new TariffCalculator(_dbContext, clockService, auditService);

        var svo = _dbContext.Airports.First(a => a.Code == "SVO");
        var led = _dbContext.Airports.First(a => a.Code == "LED");
        var newBasePrice = 5200.00m;
        var newBaggageFee = 1800.00m;

        // Act
        var result = calculator.SetBaseTariff(svo.Id, led.Id, newBasePrice, newBaggageFee);

        // Assert
        Assert.True(result.IsSuccess);

        var updated = _dbContext.Tariffs.First(t => t.OriginAirportId == svo.Id && t.DestinationAirportId == led.Id);
        Assert.Equal(newBasePrice, updated.BasePrice);
        Assert.Equal(newBaggageFee, updated.BaggageFee);

        var auditLogs = _dbContext.AuditLogs
            .Where(l => l.ActionType == "SetBaseTariff" && l.EntityName == "Tariff" && l.EntityId == updated.Id)
            .ToList();
        Assert.NotEmpty(auditLogs);
        Assert.Contains("SVO -> LED", auditLogs.Last().Description);
    }
}
