using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Audit;

/// <summary>
/// DTO записи журнала аудита операций.
/// </summary>
public class AuditLogDto
{
    /// <summary>
    /// Уникальный идентификатор записи журнала аудита.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Виртуальное системное время на момент совершения операции.
    /// </summary>
    public DateTime VirtualTime { get; set; }

    /// <summary>
    /// Реальное физическое астрономическое время хост-системы.
    /// </summary>
    public DateTime RealTime { get; set; }

    /// <summary>
    /// Роль пользователя или подсистемы, инициировавшей операцию.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Тип совершённого действия (например, CreateBooking, PayOrder, CancelFlight).
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Наименование затронутой сущности (например, Order, Flight, Ticket).
    /// </summary>
    public string? EntityName { get; set; }

    /// <summary>
    /// Идентификатор затронутой сущности.
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// Подробное текстовое описание события аудита.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
