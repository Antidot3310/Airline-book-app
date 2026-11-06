using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса сессий и ролей пользователей <see cref="SessionService"/>.
/// </summary>
public class SessionServiceTests
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
    public void OpenSession_SwitchesRole_AndLogsAuditEntry()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var sessionService = new SessionService(auditService);

        Assert.False(sessionService.IsSessionActive);
        Assert.Equal(UserRole.Passenger, sessionService.CurrentRole);

        // Act
        sessionService.OpenSession(UserRole.Administrator);

        // Assert
        Assert.True(sessionService.IsSessionActive);
        Assert.Equal(UserRole.Administrator, sessionService.CurrentRole);

        var logs = auditService.GetLogs(null, null, UserRole.Administrator, "OpenSession");
        Assert.Single(logs);
        Assert.Equal("Session", logs[0].EntityName);
        Assert.Contains("Administrator", logs[0].Description);
    }

    [Fact]
    public void CloseSession_DeactivatesSession_AndHasPermissionReturnsFalse()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var sessionService = new SessionService(auditService);

        sessionService.OpenSession(UserRole.Cashier);
        Assert.True(sessionService.IsSessionActive);
        Assert.True(sessionService.HasPermission(SessionPermissions.CreateBooking));

        // Act
        sessionService.CloseSession();

        // Assert
        Assert.False(sessionService.IsSessionActive);
        Assert.False(sessionService.HasPermission(SessionPermissions.CreateBooking));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageClients));

        var logs = auditService.GetLogs(null, null, UserRole.Cashier, "CloseSession");
        Assert.Single(logs);
        Assert.Contains("Cashier", logs[0].Description);
    }

    [Fact]
    public void HasPermission_EnforcesRoleBoundaries_Correctly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var sessionService = new SessionService(auditService);

        // Act & Assert 1: Administrator
        sessionService.OpenSession(UserRole.Administrator);

        // Разрешено администратору:
        Assert.True(sessionService.HasPermission(SessionPermissions.ManageDirectories));
        Assert.True(sessionService.HasPermission(SessionPermissions.ManageFlights));
        Assert.True(sessionService.HasPermission(SessionPermissions.ManageTariffs));
        Assert.True(sessionService.HasPermission(SessionPermissions.ViewReports));
        Assert.True(sessionService.HasPermission(SessionPermissions.ViewAuditLogs));

        // Запрещено администратору:
        Assert.False(sessionService.HasPermission(SessionPermissions.CreateBooking));
        Assert.False(sessionService.HasPermission(SessionPermissions.AcceptCashPayment));
        Assert.False(sessionService.HasPermission(SessionPermissions.AcceptCardPayment));
        Assert.False(sessionService.HasPermission(SessionPermissions.RefundTicket));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageClients));

        // Act & Assert 2: Cashier
        sessionService.OpenSession(UserRole.Cashier);

        // Разрешено кассиру:
        Assert.True(sessionService.HasPermission(SessionPermissions.CreateBooking));
        Assert.True(sessionService.HasPermission(SessionPermissions.AcceptCashPayment));
        Assert.True(sessionService.HasPermission(SessionPermissions.AcceptCardPayment));
        Assert.True(sessionService.HasPermission(SessionPermissions.RefundTicket));
        Assert.True(sessionService.HasPermission(SessionPermissions.ManageClients));

        // Запрещено кассиру:
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageDirectories));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageFlights));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageTariffs));
        Assert.False(sessionService.HasPermission(SessionPermissions.ViewReports));
        Assert.False(sessionService.HasPermission(SessionPermissions.ViewAuditLogs));

        // Act & Assert 3: Passenger
        sessionService.OpenSession(UserRole.Passenger);

        // Разрешено пассажиру:
        Assert.True(sessionService.HasPermission(SessionPermissions.CreateBooking));
        Assert.True(sessionService.HasPermission(SessionPermissions.AcceptCardPayment));
        Assert.True(sessionService.HasPermission(SessionPermissions.RefundTicket));

        // Запрещено пассажиру:
        Assert.False(sessionService.HasPermission(SessionPermissions.AcceptCashPayment));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageClients));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageDirectories));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageFlights));
        Assert.False(sessionService.HasPermission(SessionPermissions.ManageTariffs));
        Assert.False(sessionService.HasPermission(SessionPermissions.ViewReports));
        Assert.False(sessionService.HasPermission(SessionPermissions.ViewAuditLogs));
    }
}
