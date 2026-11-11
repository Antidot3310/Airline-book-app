using System;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Orders;

/// <summary>
/// DTO электронного авиабилета пассажира (M7).
/// </summary>
public class TicketDto
{
    /// <summary>
    /// Уникальный идентификатор билета.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Пользовательский уникальный номер билета (например, "TKT-A1B2C3D4E5").
    /// </summary>
    public string TicketNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительского заказа.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Номер родительского заказа (например, "ORD-A1B2C3D4").
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор пассажира / клиента системы.
    /// </summary>
    public int ClientId { get; set; }

    /// <summary>
    /// Фамилия, имя и отчество пассажира.
    /// </summary>
    public string PassengerName { get; set; } = string.Empty;

    /// <summary>
    /// Номер документа пассажира, удостоверяющего личность.
    /// </summary>
    public string PassengerDocument { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Номер рейса (например, "SU-101").
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор посадочного места рейса.
    /// </summary>
    public int FlightSeatId { get; set; }

    /// <summary>
    /// Буквенно-цифровое обозначение места в салоне (например, "1A").
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Класс обслуживания кресла (эконом или бизнес).
    /// </summary>
    public SeatClass SeatClass { get; set; }

    /// <summary>
    /// Зафиксированная стоимость билета в рублях на момент оформления заказа.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Признак включения платного багажного места.
    /// </summary>
    public bool HasBaggage { get; set; }

    /// <summary>
    /// Текущий статус билета (Booked, Paid, Used, Refunded, Cancelled).
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Дата и время запланированного вылета рейса.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Код IATA аэропорта вылета (например, "SVO").
    /// </summary>
    public string OriginAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Код IATA аэропорта прилёта (например, "LED").
    /// </summary>
    public string DestinationAirportCode { get; set; } = string.Empty;
}
