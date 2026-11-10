using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Booking;

/// <summary>
/// DTO элемента посадочного места на интерактивной схеме салона воздушного судна (W-04).
/// </summary>
public class SeatItemDto
{
    /// <summary>
    /// Уникальный идентификатор посадочного места на рейсе.
    /// </summary>
    public int SeatId { get; set; }

    /// <summary>
    /// Буквенно-цифровое обозначение места в салоне (например, "1A", "12C").
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Класс обслуживания посадочного места (эконом или бизнес).
    /// </summary>
    public SeatClass Class { get; set; }

    /// <summary>
    /// Текущее динамическое состояние места (Available, Held, Sold, Blocked).
    /// </summary>
    public SeatState State { get; set; }

    /// <summary>
    /// Рассчитанная динамическая стоимость билета на данное место без учёта багажа.
    /// </summary>
    public decimal Price { get; set; }
}
