using System;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Flights;

/// <summary>
/// Элемент списка рейсов для результатов поиска в расписании (M4).
/// </summary>
public class FlightListItemDto
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
    /// Строковое обозначение маршрута перелёта (например, "SVO -> LED").
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// Дата и время вылета рейса.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Дата и время прилёта рейса.
    /// </summary>
    public DateTime ArrivalTime { get; set; }

    /// <summary>
    /// Эксплуатационный статус рейса.
    /// </summary>
    public FlightStatus Status { get; set; }

    /// <summary>
    /// Количество доступных мест в эконом-классе.
    /// </summary>
    public int EconomySeatsAvailable { get; set; }

    /// <summary>
    /// Количество доступных мест в бизнес-классе.
    /// </summary>
    public int BusinessSeatsAvailable { get; set; }

    /// <summary>
    /// Минимальная стоимость билета на рейс в рублях (эконом без багажа).
    /// </summary>
    public decimal MinPrice { get; set; }
}
