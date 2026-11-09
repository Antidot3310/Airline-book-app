using System;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Реализация сервиса управления сессиями и ролями пользователей (M1).
/// Отслеживает активный сеанс, переключение ролей, проверку прав и регистрирует события в журнале аудита.
/// </summary>
public class SessionService : ISessionService
{
    private readonly IAuditLogService _auditLogService;
    private UserRole _currentRole = UserRole.Passenger;
    private bool _isSessionActive;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="SessionService"/>.
    /// </summary>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="auditLogService"/> равен null.</exception>
    public SessionService(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public UserRole CurrentRole => _currentRole;

    /// <inheritdoc />
    public bool IsSessionActive => _isSessionActive;

    /// <inheritdoc />
    public void OpenSession(UserRole role)
    {
        _currentRole = role;
        _isSessionActive = true;
        _auditLogService.Log(role, "OpenSession", "Session", null, $"Открыт сеанс роли '{role}'");
    }

    /// <inheritdoc />
    public void CloseSession()
    {
        if (_isSessionActive)
        {
            _auditLogService.Log(_currentRole, "CloseSession", "Session", null, $"Закрыт сеанс роли '{_currentRole}'");
        }

        _isSessionActive = false;
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
