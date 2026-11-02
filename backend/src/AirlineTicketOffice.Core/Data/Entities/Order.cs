using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность заказа на покупку авиабилетов (M7).
/// </summary>
public class Order
{
    /// <summary>
    /// Уникальный идентификатор заказа.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Пользовательский номер заказа (например, ORD-1001).
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Канал оформления заказа (самообслуживание или касса).
    /// </summary>
    public OrderChannel Channel { get; set; } = OrderChannel.SelfService;

    /// <summary>
    /// Роль пользователя, создавшего заказ (пассажир или кассир).
    /// </summary>
    public UserRole CreatedRole { get; set; } = UserRole.Passenger;

    /// <summary>
    /// Итоговая стоимость заказа в рублях.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Текущий статус заказа.
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Created;

    /// <summary>
    /// Дата и время создания заказа.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата и время истечения срока бронирования (удержания мест) при ожидании оплаты.
    /// </summary>
    public DateTime HoldExpiresAt { get; set; }

    /// <summary>
    /// Навигационная коллекция билетов, включённых в данный заказ.
    /// </summary>
    public ICollection<Ticket> Tickets { get; set; } = [];
}
