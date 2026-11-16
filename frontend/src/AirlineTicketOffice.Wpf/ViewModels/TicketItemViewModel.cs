using System;
using CommunityToolkit.Mvvm.ComponentModel;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления строки авиабилета в разделе «Мои билеты» W-07.
/// </summary>
public partial class TicketItemViewModel : ObservableObject
{
    /// <summary>
    /// Идентификатор билета в базе данных.
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
    /// Пользовательский номер родительского заказа (например, "ORD-A1B2C3D4").
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Фамилия, имя и отчество пассажира.
    /// </summary>
    public string PassengerName { get; set; } = string.Empty;

    /// <summary>
    /// Серия и номер паспорта пассажира.
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
    /// Код аэропорта вылета.
    /// </summary>
    public string OriginAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Код аэропорта назначения.
    /// </summary>
    public string DestinationAirportCode { get; set; } = string.Empty;

    /// <summary>
    /// Строковое обозначение маршрута перелёта (например, "SVO ➔ LED").
    /// </summary>
    public string RouteText => $"{OriginAirportCode} ➔ {DestinationAirportCode}";

    /// <summary>
    /// Дата и время запланированного вылета рейса.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Отформатированная дата и время вылета.
    /// </summary>
    public string DepartureTimeText => DepartureTime.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Номер посадочного места в салоне самолёта (например, "1A", "12C").
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Класс обслуживания (Бизнес или Эконом).
    /// </summary>
    public SeatClass SeatClass { get; set; }

    /// <summary>
    /// Текстовое наименование класса обслуживания.
    /// </summary>
    public string SeatClassText => SeatClass == SeatClass.Business ? "Бизнес" : "Эконом";

    /// <summary>
    /// Признак включения платного багажного места.
    /// </summary>
    public bool HasBaggage { get; set; }

    /// <summary>
    /// Текстовое описание опции багажа.
    /// </summary>
    public string HasBaggageText => HasBaggage ? "Багаж 23 кг" : "Без багажа";

    /// <summary>
    /// Стоимость билета в рублях.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Текущий эксплуатационный статус билета.
    /// </summary>
    public TicketStatus Status { get; set; }

    /// <summary>
    /// Актуальное виртуальное время системы для вычисления доступности рейса и возвратов.
    /// </summary>
    public DateTime CurrentVirtualTime { get; set; }

    /// <summary>
    /// Текстовое описание статуса билета на русском языке.
    /// </summary>
    public string StatusText => Status switch
    {
        TicketStatus.Paid => "Оплачен",
        TicketStatus.Booked => "Забронирован",
        TicketStatus.Used => "Использован",
        TicketStatus.Refunded => "Возвращён",
        TicketStatus.Cancelled => "Аннулирован",
        _ => Status.ToString()
    };

    /// <summary>
    /// Цвет фона бейджа статуса билета.
    /// </summary>
    public string StatusBadgeColor => Status switch
    {
        TicketStatus.Paid => "#4CAF50",      // Зелёный
        TicketStatus.Booked => "#FF9800",    // Оранжевый
        TicketStatus.Used => "#2196F3",      // Синий
        TicketStatus.Refunded => "#9E9E9E",  // Серый
        TicketStatus.Cancelled => "#E53935", // Красный
        _ => "#757575"
    };

    /// <summary>
    /// Признак предстоящего рейса (оплачен и время вылета позже текущего виртуального времени).
    /// </summary>
    public bool IsUpcoming => Status == TicketStatus.Paid && DepartureTime > CurrentVirtualTime;

    /// <summary>
    /// Признак прошедшего рейса (использован или оплачен, но самолет уже вылетел).
    /// </summary>
    public bool IsPast => Status == TicketStatus.Used || (Status == TicketStatus.Paid && DepartureTime <= CurrentVirtualTime);

    /// <summary>
    /// Признак неоплаченной активной брони места.
    /// </summary>
    public bool IsBooked => Status == TicketStatus.Booked;

    /// <summary>
    /// Признак возвращённого билета.
    /// </summary>
    public bool IsRefunded => Status == TicketStatus.Refunded;

    /// <summary>
    /// Возможность добровольного возврата билета (оплачен и рейс ещё не вылетел).
    /// </summary>
    public bool CanBeRefunded => Status == TicketStatus.Paid && DepartureTime > CurrentVirtualTime;

    /// <summary>
    /// Возможность перехода к оплате брони.
    /// </summary>
    public bool CanBePaid => Status == TicketStatus.Booked;
}
