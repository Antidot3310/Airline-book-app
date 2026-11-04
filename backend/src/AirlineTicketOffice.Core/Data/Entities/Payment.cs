using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность платежа по заказу (M8).
/// </summary>
public class Payment
{
    /// <summary>
    /// Уникальный идентификатор платежа.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Идентификатор оплачиваемого заказа.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Навигационное свойство оплачиваемого заказа.
    /// </summary>
    public Order? Order { get; set; }

    /// <summary>
    /// Сумма платежа в рублях.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Способ проведения оплаты (банковская карта или наличные).
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Card;

    /// <summary>
    /// Текущий статус обработки платежа.
    /// </summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>
    /// Идентификатор транзакции во внешней платёжной системе (при безналичной оплате).
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Дата и время проведения платежа.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
