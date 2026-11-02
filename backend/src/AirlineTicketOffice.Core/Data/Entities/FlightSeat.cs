using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность пассажирского места конкретного рейса (M6).
/// </summary>
public class FlightSeat
{
    /// <summary>
    /// Уникальный идентификатор места на рейсе.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор связанного рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Навигационное свойство связанного рейса.
    /// </summary>
    public Flight? Flight { get; set; }

    /// <summary>
    /// Буквенно-цифровое обозначение места в салоне (например, 1A, 12F).
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Класс обслуживания места (Эконом или Бизнес).
    /// </summary>
    public SeatClass SeatClass { get; set; } = SeatClass.Economy;

    /// <summary>
    /// Текущее состояние места (Available, Held, Sold, Blocked).
    /// </summary>
    public SeatState State { get; set; } = SeatState.Available;

    /// <summary>
    /// Время истечения удержания места при временной брони (Held).
    /// </summary>
    public DateTime? HoldUntilTime { get; set; }

    /// <summary>
    /// Токен оптимистичной блокировки для контроля конкурентного доступа при бронировании.
    /// </summary>
    public byte[] RowVersion { get; set; } = [];
}
