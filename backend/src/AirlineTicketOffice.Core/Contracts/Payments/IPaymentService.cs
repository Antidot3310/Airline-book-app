using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Контракт сервиса проведения платежей и выдачи билетов (M8).
/// Обеспечивает безналичную оплату картой через шлюз, приём наличных в кассе с расчётом сдачи
/// и проверку статуса зависших транзакций.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Выполняет оплату активного заказа банковской картой через платёжный шлюз.
    /// </summary>
    /// <param name="orderId">Идентификатор оплачиваемого заказа.</param>
    /// <param name="card">Реквизиты банковской карты плательщика.</param>
    /// <returns>Результат операции с данными проведённого платежа <see cref="PaymentResultDto"/>.</returns>
    Result<PaymentResultDto> PayWithCard(int orderId, CardDetailsDto card);

    /// <summary>
    /// Выполняет оплату активного заказа наличными в кассе с проверкой суммы и расчётом сдачи.
    /// </summary>
    /// <param name="orderId">Идентификатор оплачиваемого заказа.</param>
    /// <param name="cashReceived">Сумма наличных средств, внесённая клиентом в рублях.</param>
    /// <returns>Результат операции с данными проведённого платежа и расчётом сдачи <see cref="PaymentResultDto"/>.</returns>
    Result<PaymentResultDto> PayWithCash(int orderId, decimal cashReceived);

    /// <summary>
    /// Проверяет актуальный статус платежа по его идентификатору.
    /// </summary>
    /// <param name="paymentId">Идентификатор платежа в базе данных.</param>
    /// <returns>Результат с текущим статусом платежа <see cref="PaymentStatus"/>.</returns>
    Result<PaymentStatus> CheckPaymentStatus(int paymentId);
}
