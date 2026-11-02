namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность тарифа на авиаперелёт по маршруту (M5).
/// </summary>
public class Tariff
{
    /// <summary>
    /// Уникальный идентификатор тарифа.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор аэропорта вылета маршрута.
    /// </summary>
    public int OriginAirportId { get; set; }

    /// <summary>
    /// Навигационное свойство аэропорта вылета маршрута.
    /// </summary>
    public Airport? OriginAirport { get; set; }

    /// <summary>
    /// Идентификатор аэропорта прилёта маршрута.
    /// </summary>
    public int DestinationAirportId { get; set; }

    /// <summary>
    /// Навигационное свойство аэропорта прилёта маршрута.
    /// </summary>
    public Airport? DestinationAirport { get; set; }

    /// <summary>
    /// Базовая ставка тарифа эконом-класса в рублях.
    /// </summary>
    public decimal BasePrice { get; set; }

    /// <summary>
    /// Фиксированная стоимость одного платного места багажа в рублях.
    /// </summary>
    public decimal BaggageFee { get; set; } = 1500.00m;
}
