using System;
using System.Collections.Generic;

namespace AirlineTicketOffice.Core.Contracts.Booking;

/// <summary>
/// DTO результата успешного временного удержания (Hold) мест на рейсе.
/// </summary>
public class HoldSeatsResultDto
{
    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Список номеров успешно удержанных мест (например, "1A", "1B").
    /// </summary>
    public IReadOnlyList<string> SeatNumbers { get; set; } = [];

    /// <summary>
    /// Время истечения срока удержания мест по виртуальным часам системы.
    /// </summary>
    public DateTime HoldUntilTime { get; set; }
}
