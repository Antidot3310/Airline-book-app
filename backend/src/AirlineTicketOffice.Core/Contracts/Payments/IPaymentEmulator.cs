using AirlineTicketOffice.Core.Contracts.Common;

namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Контракт эмулятора внешнего платёжного шлюза (M14).
/// Позволяет настраивать режим ответа (успех, отказ, таймаут, случайный исход),
/// просматривать журнал входящих запросов и вручную разрешать зависшие платежи.
/// </summary>
public interface IPaymentEmulator
{
    /// <summary>
    /// Текущий режим обработки входящих платёжных запросов.
    /// </summary>
    PaymentEmulatorMode Mode { get; set; }

    /// <summary>
    /// Искусственная задержка ответа платёжного шлюза в миллисекундах.
    /// </summary>
    int DelayMilliseconds { get; set; }

    /// <summary>
    /// Обрабатывает запрос на оплату заказа банковской картой в соответствии с текущим режимом <see cref="Mode"/>.
    /// </summary>
    /// <param name="orderId">Идентификатор оплачиваемого заказа.</param>
    /// <param name="amount">Сумма платежа в рублях.</param>
    /// <param name="card">Реквизиты банковской карты.</param>
    /// <returns>Результат обработки транзакции <see cref="EmulatorTransactionResult"/>.</returns>
    EmulatorTransactionResult ProcessPayment(int orderId, decimal amount, CardDetailsDto card);

    /// <summary>
    /// Возвращает снимок журнала входящих платёжных запросов, поступивших в эмулятор.
    /// </summary>
    /// <returns>Список записей журнала входящих запросов.</returns>
    IReadOnlyList<EmulatorLogEntryDto> GetIncomingRequestsLog();

    /// <summary>
    /// Очищает журнал входящих платёжных запросов эмулятора.
    /// </summary>
    void ClearLog();

    /// <summary>
    /// Выполняет ручное подтверждение или отклонение оператором зависшего платежа со статусом Indeterminate (окно W-16, кейс TC-15).
    /// </summary>
    /// <param name="paymentId">Идентификатор платежа в базе данных.</param>
    /// <param name="markSuccess">Признак исхода: <c>true</c> — подтвердить успех (выпустить билеты), <c>false</c> — отклонить платёж.</param>
    /// <returns>Результат операции <see cref="Result"/>.</returns>
    Result ManuallyResolvePayment(int paymentId, bool markSuccess);
}
