using System;
using System.Collections.Generic;

namespace AirlineTicketOffice.Core.Contracts.Documents;

/// <summary>
/// Сервис формирования печатных форм и текстовых макетов документов системы (M15).
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Формирует маршрутную квитанцию электронного авиабилета.
    /// </summary>
    /// <param name="ticketId">Идентификатор билета.</param>
    /// <returns>Сформированный печатный документ.</returns>
    /// <exception cref="KeyNotFoundException">Выбрасывается, если билет не найден.</exception>
    PrintDocumentDto GenerateTicketDocument(int ticketId);

    /// <summary>
    /// Формирует кассовый чек / квитанцию об оплате заказа.
    /// </summary>
    /// <param name="paymentId">Идентификатор платежа.</param>
    /// <returns>Сформированный печатный документ.</returns>
    /// <exception cref="KeyNotFoundException">Выбрасывается, если платёж не найден.</exception>
    PrintDocumentDto GeneratePaymentReceipt(int paymentId);

    /// <summary>
    /// Формирует квитанцию о возврате авиабилета.
    /// </summary>
    /// <param name="refundId">Идентификатор возврата.</param>
    /// <returns>Сформированный печатный документ.</returns>
    /// <exception cref="KeyNotFoundException">Выбрасывается, если возврат не найден.</exception>
    PrintDocumentDto GenerateRefundReceipt(int refundId);

    /// <summary>
    /// Формирует печатный макет аналитического отчёта.
    /// </summary>
    /// <param name="reportTitle">Название отчёта.</param>
    /// <param name="reportData">Объект с данными отчёта (FlightLoadReportDto, RouteRevenueReportDto и др.).</param>
    /// <returns>Сформированный печатный документ.</returns>
    PrintDocumentDto GenerateReportDocument(string reportTitle, object reportData);
}
