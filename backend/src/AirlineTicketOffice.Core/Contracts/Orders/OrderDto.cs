using System;
using System.Collections.Generic;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Orders;

/// <summary>
/// DTO заказа на покупку авиабилетов (M7).
/// </summary>
public class OrderDto
{
    /// <summary>
    /// Уникальный идентификатор заказа.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Пользовательский уникальный номер заказа (например, "ORD-A1B2C3D4").
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Канал оформления заказа (самообслуживание или касса).
    /// </summary>
    public OrderChannel Channel { get; set; }

    /// <summary>
    /// Роль пользователя, создавшего данный заказ (пассажир или кассир).
    /// </summary>
    public UserRole CreatedRole { get; set; }

    /// <summary>
    /// Итоговая стоимость заказа в рублях.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Текущий статус заказа (Created, Paid, Expired, Cancelled, PartiallyRefunded, Refunded).
    /// </summary>
    public OrderStatus Status { get; set; }

    /// <summary>
    /// Дата и время создания заказа по виртуальным часам системы.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата и время истечения удержания мест при ожидании оплаты (15 минут виртуального времени).
    /// </summary>
    public DateTime HoldExpiresAt { get; set; }

    /// <summary>
    /// Список билетов, оформленных в рамках данного заказа.
    /// </summary>
    public IReadOnlyList<TicketDto> Tickets { get; set; } = [];
}
