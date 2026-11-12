using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Результат обработки транзакции эмулятором платёжного шлюза (M14).
/// </summary>
public class EmulatorTransactionResult
{
    /// <summary>
    /// Статус обработки транзакции (Success, Failed или Indeterminate).
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Идентификатор транзакции во внешней платёжной системе (заполняется при успехе).
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Текстовое сообщение с пояснением результата обработки платежа.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
