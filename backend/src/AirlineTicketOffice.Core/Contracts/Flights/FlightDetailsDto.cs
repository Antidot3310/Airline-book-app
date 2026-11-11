using System;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Flights;

/// <summary>
/// Детальные сведения о рейсе расписания (M4).
/// </summary>
public class FlightDetailsDto
{
    /// <summary>
    /// Уникальный идентификатор рейса.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Номер рейса (например, SU-101).
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор аэропорта вылета.
    /// </summary>
    public int OriginAirportId { get; set; }

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта вылета.
    /// </summary>
    public string OriginAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Город вылета.
    /// </summary>
    public string OriginCity { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор аэропорта назначения.
    /// </summary>
    public int DestinationAirportId { get; set; }

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта назначения.
    /// </summary>
    public string DestinationAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Город назначения.
    /// </summary>
    public string DestinationCity { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор назначенного воздушного судна.
    /// </summary>
    public int AircraftId { get; set; }

    /// <summary>
    /// Бортовой номер назначенного воздушного судна.
    /// </summary>
    public string AircraftTailNumber { get; set; } = string.Empty;

    /// <summary>
    /// Модель назначенного воздушного судна.
    /// </summary>
    public string AircraftModel { get; set; } = string.Empty;

    /// <summary>
    /// Дата и время вылета.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Дата и время прилёта.
    /// </summary>
    public DateTime ArrivalTime { get; set; }

    /// <summary>
    /// Эксплуатационный статус рейса.
    /// </summary>
    public FlightStatus Status { get; set; }

    /// <summary>
    /// Общее количество мест на борту.
    /// </summary>
    public int TotalSeats { get; set; }

    /// <summary>
    /// Количество доступных для бронирования мест.
    /// </summary>
    public int AvailableSeats { get; set; }
}
