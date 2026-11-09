using System;

namespace AirlineTicketOffice.Core.Contracts.Flights;

/// <summary>
/// Запрос на создание нового рейса в расписании (M4).
/// </summary>
public class CreateFlightRequest
{
    /// <summary>
    /// Номер рейса (например, SU-105).
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор аэропорта вылета.
    /// </summary>
    public int OriginAirportId { get; set; }

    /// <summary>
    /// Идентификатор аэропорта назначения.
    /// </summary>
    public int DestinationAirportId { get; set; }

    /// <summary>
    /// Идентификатор назначенного воздушного судна (борта).
    /// </summary>
    public int AircraftId { get; set; }

    /// <summary>
    /// Дата и время вылета рейса.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Дата и время прилёта рейса.
    /// </summary>
    public DateTime ArrivalTime { get; set; }
}
