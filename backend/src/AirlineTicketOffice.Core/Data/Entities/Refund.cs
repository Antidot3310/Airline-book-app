using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность возврата билета (M9).
/// </summary>
public class Refund
{
    /// <summary>
    /// Уникальный идентификатор возврата.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор возвращаемого билета.
    /// </summary>
    public int TicketId { get; set; }

    /// <summary>
    /// Навигационное свойство возвращаемого билета.
    /// </summary>
    public Ticket? Ticket { get; set; }

    /// <summary>
    /// Процент возврата стоимости билета (90, 50 или 100).
    /// </summary>
    public int RefundPercent { get; set; }

    /// <summary>
    /// Фактическая сумма возврата в рублях.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Причина оформления возврата (добровольный или вынужденный из-за отмены рейса).
    /// </summary>
    public RefundReason Reason { get; set; } = RefundReason.Voluntary;

    /// <summary>
    /// Дата и время проведения возврата.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
