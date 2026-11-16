using System;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Реализация сервиса управления сессиями и ролями пользователей (M1).
/// Отслеживает активный сеанс, переключение ролей, проверку прав и регистрирует события в журнале аудита.
/// </summary>
public class SessionService : ISessionService
{
    private readonly IAuditLogService? _auditLogService;
    private readonly IServiceScopeFactory? _scopeFactory;
    private UserRole _currentRole = UserRole.Passenger;
    private bool _isSessionActive;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="SessionService"/> с прямым внедрением журнала аудита.
    /// </summary>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="auditLogService"/> равен null.</exception>
    public SessionService(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="SessionService"/> в качестве Singleton через фабрику скоупов.
    /// </summary>
    /// <param name="scopeFactory">Фабрика областей видимости DI.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="scopeFactory"/> равен null.</exception>
    [ActivatorUtilitiesConstructor]
    public SessionService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    public UserRole CurrentRole => _currentRole;

    /// <inheritdoc />
    public bool IsSessionActive => _isSessionActive;

    /// <inheritdoc />
    public void OpenSession(UserRole role)
    {
        OpenSession(role, null, null);
    }

    /// <inheritdoc />
    public Result OpenSession(UserRole role, string? userName, string? userEmail)
    {
        _currentRole = role;
        _isSessionActive = true;
        string userDesc = !string.IsNullOrWhiteSpace(userName) ? $" пользователя '{userName}' ({userEmail ?? string.Empty})" : string.Empty;
        Log(role, "OpenSession", "Session", null, $"Открыт сеанс роли '{role}'{userDesc}");
        return Result.Success();
    }

    /// <inheritdoc />
    public void CloseSession()
    {
        if (_isSessionActive)
        {
            Log(_currentRole, "CloseSession", "Session", null, $"Закрыт сеанс роли '{_currentRole}'");
        }

        _isSessionActive = false;
    }

    private void Log(UserRole role, string actionType, string entityName, int? entityId, string description)
    {
        if (_auditLogService != null)
        {
            _auditLogService.Log(role, actionType, entityName, entityId, description);
        }
        else if (_scopeFactory != null)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            IAuditLogService auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            auditService.Log(role, actionType, entityName, entityId, description);
        }
    }

    /// <inheritdoc />
    public bool HasPermission(string action)
    {
        if (!_isSessionActive || string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return _currentRole switch
        {
            UserRole.Administrator => action is SessionPermissions.ManageDirectories
                or SessionPermissions.ManageFlights
                or SessionPermissions.ManageTariffs
                or SessionPermissions.ViewReports
                or SessionPermissions.ViewAuditLogs,

            UserRole.Cashier => action is SessionPermissions.CreateBooking
                or SessionPermissions.AcceptCashPayment
                or SessionPermissions.AcceptCardPayment
                or SessionPermissions.RefundTicket
                or SessionPermissions.ManageClients,

            UserRole.Passenger => action is SessionPermissions.CreateBooking
                or SessionPermissions.AcceptCardPayment
                or SessionPermissions.RefundTicket,

            _ => false
        };
    }
}
