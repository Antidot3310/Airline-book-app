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
    /// Дата и время завершения массового возврата.
    /// </summary>
    public DateTime ProcessedAt { get; set; }
}
