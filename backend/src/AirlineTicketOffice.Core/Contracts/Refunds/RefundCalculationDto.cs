namespace AirlineTicketOffice.Core.Contracts.Refunds;

/// <summary>
/// Результат предварительного расчёта суммы возврата и удержаний по билету.
/// </summary>
public class RefundCalculationDto
{
    /// <summary>
    /// Идентификатор рассчитываемого билета.
    /// </summary>
    public int TicketId { get; set; }

    /// <summary>
    /// Номер билета.
    /// </summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// Первоначальная стоимость билета в рублях.
    /// </summary>
    public decimal OriginalPrice { get; set; }

    /// <summary>
    /// Процент возврата стоимости билета (90, 50, 100 или 0).
    /// </summary>
    public int RefundPercent { get; set; }

    /// <summary>
    /// Расчётная сумма к возврату клиенту в рублях.
    /// </summary>
    public decimal RefundAmount { get; set; }

    /// <summary>
    /// Сумма удерживаемого штрафа в рублях.
    /// </summary>
    public decimal PenaltyAmount { get; set; }

    /// <summary>
    /// Текстовое описание основания и условий возврата.
    /// </summary>
    public string ReasonDescription { get; set; } = string.Empty;

    /// <summary>
    /// Признак допустимости оформления возврата по билету.
    /// </summary>
    public bool IsRefundAllowed { get; set; }
}
