using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность рейса расписания (M4).
/// </summary>
public class Flight
{
    /// <summary>
    /// Уникальный идентификатор рейса.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Номер рейса (например, SU-101, DP-204).
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор аэропорта вылета.
    /// </summary>
    public int OriginAirportId { get; set; }

    /// <summary>
    /// Навигационное свойство аэропорта вылета.
    /// </summary>
    public Airport? OriginAirport { get; set; }

    /// <summary>
    /// Идентификатор аэропорта прилёта.
    /// </summary>
    public int DestinationAirportId { get; set; }

    /// <summary>
    /// Навигационное свойство аэропорта прилёта.
    /// </summary>
    public Airport? DestinationAirport { get; set; }

    /// <summary>
    /// Идентификатор назначенного воздушного судна (борта).
    /// </summary>
    public int AircraftId { get; set; }

    /// <summary>
    /// Навигационное свойство назначенного воздушного судна.
    /// </summary>
    public Aircraft? Aircraft { get; set; }

    /// <summary>
    /// Плановое дата и время вылета рейса.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Плановое дата и время прилёта рейса.
    /// </summary>
    public DateTime ArrivalTime { get; set; }

    /// <summary>
    /// Текущий статус рейса.
    /// </summary>
    public FlightStatus Status { get; set; } = FlightStatus.Scheduled;

    /// <summary>
    /// Навигационная коллекция пассажирских мест рейса.
    /// </summary>
    public ICollection<FlightSeat> Seats { get; set; } = [];

    /// <summary>
    /// Навигационная коллекция билетов, оформленных на данный рейс.
    /// </summary>
    public ICollection<Ticket> Tickets { get; set; } = [];
}
