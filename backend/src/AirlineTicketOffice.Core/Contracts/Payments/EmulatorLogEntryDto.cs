using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Запись журнала входящих запросов эмулятора платёжного шлюза (M14).
/// </summary>
public class EmulatorLogEntryDto
{
    /// <summary>
    /// Порядковый идентификатор записи в журнале эмулятора.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Дата и время поступления запроса по виртуальным часам системы.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Идентификатор оплачиваемого заказа.
    /// </summary>
    public int OrderId { get; set; }

    /// <summary>
    /// Сумма платёжного запроса в рублях.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Маскированный номер карты (например, "**** **** **** 1234").
    /// </summary>
    public string MaskedCardNumber { get; set; } = string.Empty;

    /// <summary>
    /// Итоговый статус обработки транзакции.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Внешний идентификатор транзакции (при успешном проведении платежа).
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Текстовое сообщение с результатом обработки запроса.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
