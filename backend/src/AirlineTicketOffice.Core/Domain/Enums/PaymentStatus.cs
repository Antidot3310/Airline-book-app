namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Статус проведения платежа.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Платёж ожидает обработки или подтверждения.
    /// </summary>
    Pending,

    /// <summary>
    /// Платёж успешно совершён.
    /// </summary>
    Success,

    /// <summary>
    /// Ошибка при проведении платежа.
    /// </summary>
    Failed,

    /// <summary>
    /// Неопределённый статус платежа (требуется ручная сверка или повторная проверка).
    /// </summary>
    Indeterminate,

    /// <summary>
    /// Платёж возвращён плательщику.
    /// </summary>
    Refunded
}
