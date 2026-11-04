using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность электронного авиабилета (M7).
/// </summary>
public class Ticket
{
    /// <summary>
    /// Уникальный идентификатор билета.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Пользовательский уникальный номер билета (например, TKT-10001).
    /// </summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительского заказа.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Навигационное свойство родительского заказа.
    /// </summary>
    public Order? Order { get; set; }

    /// <summary>
    /// Идентификатор пассажира, на которого оформлен билет.
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Навигационное свойство пассажира.
    /// </summary>
    public Client? Client { get; set; }

    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Навигационное свойство рейса.
    /// </summary>
    public Flight? Flight { get; set; }

    /// <summary>
    /// Идентификатор закреплённого за билетом пассажирского места.
    /// </summary>
    public int FlightSeatId { get; set; }

    /// <summary>
    /// Навигационное свойство пассажирского места.
    /// </summary>
    public FlightSeat? FlightSeat { get; set; }

    /// <summary>
    /// Зафиксированная стоимость билета в рублях на момент оформления заказа.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Признак включения платного места багажа в стоимость билета.
    /// </summary>
    public bool HasBaggage { get; set; } = false;

    /// <summary>
    /// Текущий статус билета.
    /// </summary>
    public TicketStatus Status { get; set; } = TicketStatus.Booked;

    /// <summary>
    /// Навигационное свойство записи возврата билета (при наличии).
    /// </summary>
    public Refund? Refund { get; set; }
}
