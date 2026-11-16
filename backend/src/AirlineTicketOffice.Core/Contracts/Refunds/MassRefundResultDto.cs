namespace AirlineTicketOffice.Core.Contracts.Refunds;

/// <summary>
/// Результат массового вынужденного возврата билетов при отмене рейса.
/// </summary>
public class MassRefundResultDto
{
    /// <summary>
    /// Идентификатор отменённого рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Номер отменённого рейса.
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Количество билетов, по которым выполнен вынужденный возврат.
    /// </summary>
    public int RefundedTicketsCount { get; set; }

    /// <summary>
    /// Общая сумма возвращённых средств по всем билетам рейса в рублях.
    /// </summary>
    public decimal TotalRefundAmount { get; set; }

    /// <summary>
    /// Процент возврата стоимости билетов при отмене рейса (всегда 100%).
    /// </summary>
    public int RefundPercent { get; set; } = 100;

    /// <summary>
    /// Список созданных записей о возвратах по билетам рейса.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<RefundResultDto> Refunds { get; set; } = [];

    /// <summary>
    /// Дата и время завершения массового возврата.
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
