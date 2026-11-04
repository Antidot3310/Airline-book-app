namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Статус жизненного цикла заказа.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Создан (ожидает оплаты в течение срока удержания мест).
    /// </summary>
    Created,

    /// <summary>
    /// Оплачен покупателем.
    /// </summary>
    Paid,

    /// <summary>
    /// Истёк срок оплаты (бронь мест автоматически аннулирована).
    /// </summary>
    Expired,

    /// <summary>
    /// Отменён до завершения оплаты.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Частично возвращён (оформлен возврат части билетов заказа).
    /// </summary>
    PartiallyRefunded,

    /// <summary>
    /// Полностью возвращён (все билеты заказа возвращены).
    /// </summary>
    Refunded
}
