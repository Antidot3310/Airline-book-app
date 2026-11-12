using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Результат проведения или попытки проведения платежа по заказу (M8).
/// </summary>
public class PaymentResultDto
{
    /// <summary>
    /// Уникальный идентификатор записи платежа в базе данных.
    /// </summary>
    public int PaymentId { get; set; }

    /// <summary>
    /// Идентификатор оплачиваемого заказа.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Пользовательский номер заказа (например, "ORD-A1B2C3D4").
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Сумма платежа к оплате по заказу в рублях.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Внесённая сумма наличными в рублях (заполняется при оплате наличными в кассе).
    /// </summary>
    public decimal? CashReceived { get; set; }

    /// <summary>
    /// Сумма сдачи в рублях при оплате наличными.
    /// </summary>
    public decimal ChangeAmount { get; set; }

    /// <summary>
    /// Способ оплаты (банковская карта или наличные).
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>
    /// Текущий статус платежа (Success, Failed, Indeterminate, Refunded).
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Идентификатор транзакции во внешней платёжной системе (при безналичной оплате).
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Дата и время регистрации платежа по виртуальным часам системы.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Информационное сообщение с пояснением результата операции.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
