namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Статус электронного авиабилета.
/// </summary>
public enum TicketStatus
{
    /// <summary>
    /// Забронирован в составе созданного заказа.
    /// </summary>
    Booked,

    /// <summary>
    /// Оплачен и действителен для перелёта.
    /// </summary>
    Paid,

    /// <summary>
    /// Возвращён пассажиром или авиакомпанией.
    /// </summary>
    Refunded,

    /// <summary>
    /// Использован (пассажир совершил перелёт).
    /// </summary>
    Used,

    /// <summary>
    /// Аннулирован (например, при отмене неоплаченного заказа).
    /// </summary>
    Cancelled
}
