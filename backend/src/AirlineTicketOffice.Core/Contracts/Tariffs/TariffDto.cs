namespace AirlineTicketOffice.Core.Contracts.Tariffs;

/// <summary>
/// Объект передачи данных тарифа авиаперелёта (M5).
/// </summary>
public class TariffDto
{
    /// <summary>
    /// Уникальный идентификатор тарифа.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор аэропорта вылета.
    /// </summary>
    public int OriginAirportId { get; set; }

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта вылета.
    /// </summary>
    public string OriginAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор аэропорта назначения.
    /// </summary>
    public int DestinationAirportId { get; set; }

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта назначения.
    /// </summary>
    public string DestinationAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Базовая ставка тарифа эконом-класса в рублях.
    /// </summary>
    public decimal BasePrice { get; set; }

    /// <summary>
    /// Фиксированная стоимость одного платного места багажа в рублях.
    /// </summary>
    public decimal BaggageFee { get; set; }
}
