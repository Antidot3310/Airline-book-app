using System.Collections.Generic;

namespace AirlineTicketOffice.Core.Contracts.Booking;

/// <summary>
/// DTO интерактивной карты мест рейса для отображения салона воздушного судна (W-04).
/// </summary>
public class SeatMapDto
{
    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Номер рейса (например, "SU-101").
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Наименование модели воздушного судна (например, "Sukhoi Superjet 100", "Airbus A320").
    /// </summary>
    public string AircraftModel { get; set; } = string.Empty;

    /// <summary>
    /// Упорядоченный список всех кресел салона воздушного судна.
    /// </summary>
    public IReadOnlyList<SeatItemDto> Seats { get; set; } = [];

    /// <summary>
    /// Общее количество мест в салоне воздушного судна.
    /// </summary>
    public int TotalSeats => Seats.Count;

    /// <summary>
    /// Количество доступных для бронирования мест в салоне.
    /// </summary>
    public int AvailableSeats => System.Linq.Enumerable.Count(Seats, s => s.State == Domain.Enums.SeatState.Available);
}
