using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Data;
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
}
