using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Refunds;

/// <summary>
/// Результат успешно оформленного возврата одного билета.
/// </summary>
public class RefundResultDto
{
    /// <summary>
    /// Идентификатор созданной записи о возврате.
    /// </summary>
    public int RefundId { get; set; }

    /// <summary>
    /// Идентификатор возвращённого билета.
    /// </summary>
    public int TicketId { get; set; }

    /// <summary>
    /// Номер возвращённого билета.
    /// </summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// Первоначальная стоимость билета в рублях.
    /// </summary>
    public decimal OriginalPrice { get; set; }

    /// <summary>
    /// Применённый процент возврата стоимости билета.
    /// </summary>
    public int RefundPercent { get; set; }

    /// <summary>
    /// Фактическая сумма возврата клиенту в рублях.
    /// </summary>
    public decimal RefundAmount { get; set; }

    /// <summary>
    /// Удержанная сумма штрафа в рублях.
    /// </summary>
    public decimal PenaltyAmount { get; set; }

    /// <summary>
    /// Причина оформления возврата.
    /// </summary>
    public RefundReason Reason { get; set; }

    /// <summary>
    /// Обновлённый статус родительского заказа после возврата билета.
    /// </summary>
    public OrderStatus UpdatedOrderStatus { get; set; }

    /// <summary>
    /// Дата и время проведения операции возврата.
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
