using System.Collections.Generic;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Orders;

/// <summary>
/// Запрос на создание заказа с одновременным бронированием от 1 до 5 пассажиров (M7).
/// </summary>
public class CreateOrderRequest
{
    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Идентификатор клиента / плательщика заказа (опционально).
    /// </summary>
    public int? ClientId { get; set; }

    /// <summary>
    /// Канал оформления бронирования (опционально).
    /// </summary>
    public BookingChannel? BookingChannel { get; set; }

    /// <summary>
    /// Список пассажиров с закреплёнными посадочными местами и опциями багажа (от 1 до 5 человек).
    /// </summary>
    public IReadOnlyList<PassengerOrderItemDto> Passengers { get; set; } = [];
}
