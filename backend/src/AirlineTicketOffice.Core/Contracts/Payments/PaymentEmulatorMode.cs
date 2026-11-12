namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Режим работы эмулятора платёжного шлюза (M14).
/// </summary>
public enum PaymentEmulatorMode
{
    /// <summary>
    /// Все входящие платежи успешно одобряются платёжным шлюзом.
    /// </summary>
    AlwaysSuccess,

    /// <summary>
    /// Все входящие платежи отклоняются банком-эмитентом.
    /// </summary>
    AlwaysReject,

    /// <summary>
    /// Платёжный шлюз не отвечает в установленное время (возвращается статус Indeterminate).
    /// </summary>
    Timeout,

    /// <summary>
    /// Случайный исход обработки платежа (с вероятностью 50% успех или отказ).
    /// </summary>
    Random
}
