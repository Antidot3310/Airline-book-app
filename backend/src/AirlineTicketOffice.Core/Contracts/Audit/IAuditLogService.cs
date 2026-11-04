using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Audit;

/// <summary>
/// Интерфейс сервиса журнала аудита операций (M11).
/// Обеспечивает сквозную фиксацию системных событий и действий пользователей,
/// а также получение истории аудита с фильтрацией.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Регистрирует событие аудита от имени системной роли (UserRole.System).
    /// </summary>
    /// <param name="actionType">Тип действия (например, CreateBooking, CancelFlight, ExpireHold).</param>
    /// <param name="entityName">Имя затронутой сущности (например, Order, Flight, Ticket).</param>
    /// <param name="entityId">Идентификатор затронутой сущности (если применимо).</param>
    /// <param name="description">Подробное текстовое описание события.</param>
    void Log(string actionType, string entityName, int? entityId, string description);

    /// <summary>
    /// Регистрирует событие аудита с явным указанием роли пользователя или подсистемы.
    /// </summary>
    /// <param name="role">Роль пользователя или инициатора операции.</param>
    /// <param name="actionType">Тип действия (например, CreateBooking, PayOrder, CancelFlight).</param>
    /// <param name="entityName">Имя затронутой сущности (например, Order, Flight, Ticket).</param>
    /// <param name="entityId">Идентификатор затронутой сущности (если применимо).</param>
    /// <param name="description">Подробное текстовое описание события.</param>
    void Log(UserRole role, string actionType, string entityName, int? entityId, string description);

    /// <summary>
    /// Получает список записей журнала аудита, отсортированный по убыванию виртуального времени, с возможностью фильтрации.
    /// </summary>
    /// <param name="fromDate">Начальная граница диапазона виртуального времени (включительно), если задана.</param>
    /// <param name="toDate">Конечная граница диапазона виртуального времени (включительно), если задана.</param>
    /// <param name="roleFilter">Фильтр по роли пользователя, если задан.</param>
    /// <param name="actionFilter">Подстрока для фильтрации по типу действия, если задана.</param>
    /// <returns>Список DTO записей аудита, удовлетворяющих заданным критериям.</returns>
    IReadOnlyList<AuditLogDto> GetLogs(DateTime? fromDate, DateTime? toDate, UserRole? roleFilter, string? actionFilter);
}
