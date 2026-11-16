using AirlineTicketOffice.Core.Contracts.Common;

namespace AirlineTicketOffice.Core.Contracts.Refunds;

/// <summary>
/// Контракт сервиса расчёта и проведения возвратов авиабилетов (M9).
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Выполняет предварительный расчёт суммы возврата и штрафа для указанного билета.
    /// </summary>
    /// <param name="ticketId">Идентификатор билета.</param>
    /// <returns>Результат с деталями расчёта возврата или описание причины запрета.</returns>
    Result<RefundCalculationDto> CalculateRefund(int ticketId);

    /// <summary>
    /// Оформляет возврат указанного билета с освобождением места и пересчётом статуса заказа.
    /// </summary>
    /// <param name="ticketId">Идентификатор возвращаемого билета.</param>
    /// <returns>Результат с параметрами проведённого возврата или сообщение об ошибке.</returns>
    Result<RefundResultDto> ProcessRefund(int ticketId);

    /// <summary>
    /// Выполняет массовый вынужденный возврат всех оплаченных билетов при отмене рейса.
    /// </summary>
    /// <param name="flightId">Идентификатор отменяемого рейса.</param>
    /// <returns>Сводный результат массового возврата по рейсу или сообщение об ошибке.</returns>
    Result<MassRefundResultDto> ProcessFlightCancellationRefund(int flightId);
}
