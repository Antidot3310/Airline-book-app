using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Session;

/// <summary>
/// Интерфейс сервиса управления сессиями и ролевой моделью пользователей (M1).
/// Обеспечивает открытие и закрытие сеансов, отслеживание текущей роли и проверку прав доступа.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Текущая роль активного пользователя в системе.
    /// </summary>
    UserRole CurrentRole { get; }

    /// <summary>
    /// Признак активности текущего пользовательского сеанса.
    /// </summary>
    bool IsSessionActive { get; }

    /// <summary>
    /// Открывает сеанс с заданной ролью пользователя и фиксирует действие в журнале аудита.
    /// </summary>
    /// <param name="role">Роль пользователя, для которой открывается сеанс.</param>
    void OpenSession(UserRole role);

    /// <summary>
    /// Закрывает текущий активный сеанс и фиксирует действие в журнале аудита.
    /// </summary>
    void CloseSession();

    /// <summary>
    /// Проверяет наличие у текущей активной роли разрешения на выполнение указанного действия.
    /// </summary>
    /// <param name="action">Идентификатор действия или права из <see cref="SessionPermissions"/>.</param>
    /// <returns><c>true</c>, если сеанс активен и действие разрешено роли; иначе <c>false</c>.</returns>
    bool HasPermission(string action);
}
