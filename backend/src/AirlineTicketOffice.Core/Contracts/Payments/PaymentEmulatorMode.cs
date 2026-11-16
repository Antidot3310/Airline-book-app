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

/// <summary>
/// Режим ответа эмулятора платёжного шлюза.
/// </summary>
public enum GatewayResponseMode
{
    /// <summary>
    /// Успешное проведение платежа.
    /// </summary>
    AlwaysSuccess = PaymentEmulatorMode.AlwaysSuccess,

    /// <summary>
    /// Отказ банка-эмитента в проведении платежа.
    /// </summary>
    AlwaysFail = PaymentEmulatorMode.AlwaysReject,

    /// <summary>
    /// Таймаут обращения к платёжному шлюзу.
    /// </summary>
    Timeout = PaymentEmulatorMode.Timeout,

    /// <summary>
    /// Случайный исход.
    /// </summary>
    Random = PaymentEmulatorMode.Random
}
