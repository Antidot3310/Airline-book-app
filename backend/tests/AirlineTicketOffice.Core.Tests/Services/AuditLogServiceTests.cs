using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса журнала аудита операций <see cref="AuditLogService"/>.
/// </summary>
public class AuditLogServiceTests
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
    public void Log_CreatesAuditLogEntry_WithCorrectVirtualAndRealTime()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new AuditLogService(context);
        var beforeTime = DateTime.Now.AddSeconds(-2);

        // Act
        service.Log("CreateOrder", "Order", 100, "Создан заказ #ORD-100");
        service.Log(UserRole.Cashier, "PayOrder", "Order", 100, "Оплачен заказ #ORD-100");

        // Assert
        var afterTime = DateTime.Now.AddSeconds(2);
        var logs = service.GetLogs(null, null, null, null);

        Assert.Equal(2, logs.Count);

        // Проверяем вторую запись (сохранена последней, сортировка по убыванию Id/времени)
        var payLog = logs[0];
        Assert.Equal(UserRole.Cashier, payLog.Role);
        Assert.Equal("PayOrder", payLog.ActionType);
        Assert.Equal("Order", payLog.EntityName);
        Assert.Equal(100, payLog.EntityId);
        Assert.Equal("Оплачен заказ #ORD-100", payLog.Description);
        Assert.Equal(new DateTime(2026, 11, 1, 8, 0, 0), payLog.VirtualTime);
        Assert.InRange(payLog.RealTime, beforeTime, afterTime);

        // Проверяем первую запись (по умолчанию системная роль)
        var createLog = logs[1];
        Assert.Equal(UserRole.System, createLog.Role);
        Assert.Equal("CreateOrder", createLog.ActionType);
        Assert.Equal("Order", createLog.EntityName);
        Assert.Equal(100, createLog.EntityId);
        Assert.Equal("Создан заказ #ORD-100", createLog.Description);
        Assert.Equal(new DateTime(2026, 11, 1, 8, 0, 0), createLog.VirtualTime);
        Assert.InRange(createLog.RealTime, beforeTime, afterTime);
    }

    [Fact]
    public void GetLogs_WithRoleAndDateFilters_ReturnsFilteredLogs()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new AuditLogService(context);

        var testLogs = new List<AuditLog>
        {
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 9, 0, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Passenger,
                ActionType = "SearchFlights",
                Description = "Поиск рейсов"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 10, 0, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Cashier,
                ActionType = "BookTicket",
                Description = "Бронирование билета"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 11, 0, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Cashier,
                ActionType = "PayTicket",
                Description = "Оплата билета"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 12, 0, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Administrator,
                ActionType = "CancelFlight",
                Description = "Отмена рейса"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 2, 8, 0, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Cashier,
                ActionType = "RefundTicket",
                Description = "Возврат билета"
            }
        };

        context.AuditLogs.AddRange(testLogs);
        context.SaveChanges();

        var fromDate = new DateTime(2026, 11, 1, 9, 30, 0);
        var toDate = new DateTime(2026, 11, 1, 11, 30, 0);
        var roleFilter = UserRole.Cashier;

        // Act
        var results = service.GetLogs(fromDate, toDate, roleFilter, null);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("PayTicket", results[0].ActionType);
        Assert.Equal(new DateTime(2026, 11, 1, 11, 0, 0), results[0].VirtualTime);
        Assert.Equal(UserRole.Cashier, results[0].Role);

        Assert.Equal("BookTicket", results[1].ActionType);
        Assert.Equal(new DateTime(2026, 11, 1, 10, 0, 0), results[1].VirtualTime);
        Assert.Equal(UserRole.Cashier, results[1].Role);
    }

    [Fact]
    public void GetLogs_WithActionFilter_ReturnsMatchingEntries()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new AuditLogService(context);

        var testLogs = new List<AuditLog>
        {
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 9, 0, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Cashier,
                ActionType = "OrderCreated",
                Description = "Заказ создан"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 9, 5, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Cashier,
                ActionType = "OrderPaid",
                Description = "Заказ оплачен"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 9, 10, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Administrator,
                ActionType = "FlightCancelled",
                Description = "Рейс отменен"
            },
            new()
            {
                VirtualTime = new DateTime(2026, 11, 1, 9, 15, 0),
                RealTime = DateTime.Now,
                Role = UserRole.Cashier,
                ActionType = "OrderRefunded",
                Description = "Заказ возвращен"
            }
        };

        context.AuditLogs.AddRange(testLogs);
        context.SaveChanges();

        // Act
        var results = service.GetLogs(null, null, null, "Order");

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, log => Assert.Contains("Order", log.ActionType));
    }
}
