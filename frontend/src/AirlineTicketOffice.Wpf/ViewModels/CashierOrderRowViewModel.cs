using System;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Строка списка заказов текущей смены кассира (W-08).
/// Выполняет исключительно презентационное форматирование данных DTO ядра:
/// человекочитаемый статус, канал продажи и остаток срока удержания мест.
/// Контракты <c>AirlineTicketOffice.Core.Contracts.*</c> при этом не изменяются.
/// </summary>
public sealed class CashierOrderRowViewModel
{
    private readonly OrderDto _order;
    private readonly DateTime _virtualNow;

    /// <summary>
    /// Инициализирует новый экземпляр строки заказа.
    /// </summary>
    /// <param name="order">DTO заказа, полученный из <c>IOrderTicketService</c>.</param>
    /// <param name="virtualNow">Текущее виртуальное время системы для расчёта срока брони.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="order"/> равен null.</exception>
    public CashierOrderRowViewModel(OrderDto order, DateTime virtualNow)
    {
        _order = order ?? throw new ArgumentNullException(nameof(order));
        _virtualNow = virtualNow;
    }

    /// <summary>
    /// Номер заказа (например, "ORD-A1B2C3D4").
    /// </summary>
    public string OrderNumber => _order.OrderNumber;

    /// <summary>
    /// Виртуальное время оформления заказа.
    /// </summary>
    public DateTime CreatedAt => _order.CreatedAt;

    /// <summary>
    /// Количество билетов в заказе.
    /// </summary>
    public int TicketsCount => _order.Tickets.Count;

    /// <summary>
    /// Итоговая стоимость заказа в рублях.
    /// </summary>
    public decimal TotalAmount => _order.TotalAmount;

    /// <summary>
    /// Системный статус заказа без каких-либо псевдонимов.
    /// </summary>
    public OrderStatus Status => _order.Status;

    /// <summary>
    /// Каноническое время истечения удержания мест <see cref="OrderDto.HoldExpiresAt"/>.
    /// </summary>
    public DateTime HoldExpiresAt => _order.HoldExpiresAt;

    /// <summary>
    /// Признак заказа, ожидающего оплаты (системный статус <see cref="OrderStatus.Created"/>).
    /// </summary>
    public bool IsAwaitingPayment => _order.Status == OrderStatus.Created;

    /// <summary>
    /// Признак истёкшего срока удержания мест для неоплаченного заказа.
    /// </summary>
    public bool IsHoldExpired => IsAwaitingPayment && _order.HoldExpiresAt <= _virtualNow;

    /// <summary>
    /// Русскоязычная подпись статуса заказа для отображения в интерфейсе.
    /// </summary>
    public string StatusText => _order.Status switch
    {
        OrderStatus.Created => IsHoldExpired ? "Бронь просрочена" : "Ожидает оплаты",
        OrderStatus.Paid => "Оплачен",
        OrderStatus.Expired => "Срок оплаты истёк",
        OrderStatus.Cancelled => "Отменён",
        OrderStatus.PartiallyRefunded => "Частичный возврат",
        OrderStatus.Refunded => "Возвращён",
        _ => _order.Status.ToString()
    };

    /// <summary>
    /// Русскоязычная подпись канала оформления заказа.
    /// </summary>
    public string ChannelText => _order.Channel == OrderChannel.Cashier ? "Касса" : "Самообслуживание";

    /// <summary>
    /// Подпись срока действия брони. Пустая строка для оплаченных и закрытых заказов.
    /// </summary>
    public string HoldText
    {
        get
        {
            if (!IsAwaitingPayment)
            {
                return string.Empty;
            }

            return IsHoldExpired
                ? "Срок брони истёк — места будут освобождены"
                : $"Бронь действует до {_order.HoldExpiresAt:HH:mm}";
        }
    }

    /// <summary>
    /// Объединённая подпись статуса и канала продажи для правой колонки строки заказа.
    /// </summary>
    public string StatusAndChannelText => $"{StatusText} • {ChannelText}";
}
