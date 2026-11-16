using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис формирования печатных форм и текстовых макетов документов системы (M15).
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly AirlineDbContext _dbContext;
    private readonly IVirtualClockService _clockService;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса формирования документов.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных «Авиакасса».</param>
    /// <param name="clockService">Сервис системного виртуального времени.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если один из аргументов равен null.</exception>
    public DocumentService(AirlineDbContext dbContext, IVirtualClockService clockService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
    }

    /// <summary>
    /// Формирует маршрутную квитанцию электронного авиабилета.
    /// </summary>
    /// <param name="ticketId">Идентификатор билета.</param>
    /// <returns>Сформированный печатный документ.</returns>
    /// <exception cref="KeyNotFoundException">Выбрасывается, если билет не найден.</exception>
    public PrintDocumentDto GenerateTicketDocument(int ticketId)
    {
        Ticket? ticket = _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Order)
            .Include(t => t.Client)
            .Include(t => t.FlightSeat)
            .Include(t => t.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(t => t.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .Include(t => t.Flight)
                .ThenInclude(f => f!.Aircraft)
                    .ThenInclude(a => a!.AircraftType)
            .FirstOrDefault(t => t.Id == ticketId);

        if (ticket == null)
        {
            throw new KeyNotFoundException($"Билет с ID {ticketId} не найден.");
        }

        string ticketStatusText = ticket.Status switch
        {
            TicketStatus.Paid => "Оплачен",
            TicketStatus.Used => "Использован (вылетел)",
            TicketStatus.Refunded => "Возвращён",
            TicketStatus.Booked => "Забронирован",
            TicketStatus.Cancelled => "Аннулирован",
            _ => ticket.Status.ToString()
        };

        string seatClassText = ticket.FlightSeat?.SeatClass switch
        {
            SeatClass.Business => "Бизнес-класс",
            SeatClass.Economy => "Эконом-класс",
            _ => "Эконом-класс"
        };

        string baggageText = ticket.HasBaggage
            ? "Включено 1 место до 23 кг"
            : "Не включён (только ручная кладь до 10 кг)";

        string originCode = ticket.Flight?.OriginAirport?.Code ?? string.Empty;
        string originCity = ticket.Flight?.OriginAirport?.City ?? string.Empty;
        string destCode = ticket.Flight?.DestinationAirport?.Code ?? string.Empty;
        string destCity = ticket.Flight?.DestinationAirport?.City ?? string.Empty;
        string routeText = $"{originCode} ({originCity}) -> {destCode} ({destCity})";

        string aircraftModel = ticket.Flight?.Aircraft?.AircraftType?.Name ?? "Н/Д";
        string tailNumber = ticket.Flight?.Aircraft?.TailNumber ?? "Н/Д";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("================================================================");
        sb.AppendLine("                    АВИАКОМПАНИЯ \"АВИАКАССА\"                    ");
        sb.AppendLine("             МАРШРУТНАЯ КВИТАНЦИЯ ЭЛЕКТРОННОГО БИЛЕТА           ");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Номер билета:       {ticket.TicketNumber}");
        sb.AppendLine($"Номер заказа:       {ticket.Order?.OrderNumber ?? "Н/Д"}");
        sb.AppendLine($"Дата оформления:    {_clockService.CurrentVirtualTime:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Статус билета:      {ticketStatusText}");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("ПАССАЖИР:");
        sb.AppendLine($"ФИО:                {ticket.Client?.FullName ?? "Н/Д"}");
        sb.AppendLine($"Документ:           {ticket.Client?.DocumentNumber ?? "Н/Д"}");
        if (!string.IsNullOrEmpty(ticket.Client?.ContactPhone))
        {
            sb.AppendLine($"Телефон:            {ticket.Client.ContactPhone}");
        }
        if (!string.IsNullOrEmpty(ticket.Client?.ContactEmail))
        {
            sb.AppendLine($"Email:              {ticket.Client.ContactEmail}");
        }
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("ДЕТАЛИ ПЕРЕЛЁТА:");
        sb.AppendLine($"Рейс:               {ticket.Flight?.FlightNumber ?? "Н/Д"}");
        sb.AppendLine($"Маршрут:            {routeText}");
        sb.AppendLine($"Вылет:              {ticket.Flight?.DepartureTime:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Прилёт:             {ticket.Flight?.ArrivalTime:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Самолёт:            {aircraftModel} (борт {tailNumber})");
        sb.AppendLine($"Место в салоне:     {ticket.FlightSeat?.SeatNumber ?? "Н/Д"}");
        sb.AppendLine($"Класс обслуживания: {seatClassText}");
        sb.AppendLine($"Багаж:              {baggageText}");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("СТОИМОСТЬ:");
        sb.AppendLine($"Тариф и сборы:      {ticket.Price:N2} руб.");
        sb.AppendLine($"ИТОГО К ОПЛАТЕ:     {ticket.Price:N2} руб.");
        sb.AppendLine("================================================================");
        sb.AppendLine("Электронный билет подтверждает заключение договора воздушной");
        sb.AppendLine("перевозки. Регистрация в аэропорту заканчивается за 40 минут");
        sb.AppendLine("до времени вылета рейса. Приятного полёта!");
        sb.AppendLine("================================================================");

        return new PrintDocumentDto
        {
            Title = "Маршрутная квитанция электронного билета",
            DocumentNumber = ticket.TicketNumber,
            CreatedAt = _clockService.CurrentVirtualTime,
            FormattedContent = sb.ToString()
        };
    }

    /// <summary>
    /// Формирует кассовый чек / квитанцию об оплате заказа.
    /// </summary>
    /// <param name="paymentId">Идентификатор платежа.</param>
    /// <returns>Сформированный печатный документ.</returns>
    /// <exception cref="KeyNotFoundException">Выбрасывается, если платёж не найден.</exception>
    public PrintDocumentDto GeneratePaymentReceipt(int paymentId)
    {
        Payment? payment = _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Order)
            .FirstOrDefault(p => p.Id == paymentId);

        if (payment == null)
        {
            throw new KeyNotFoundException($"Платёж с ID {paymentId} не найден.");
        }

        string paymentMethodText = payment.PaymentMethod switch
        {
            PaymentMethod.Card => "Банковская карта",
            PaymentMethod.Cash => "Наличные в кассе",
            _ => payment.PaymentMethod.ToString()
        };

        string paymentStatusText = payment.Status switch
        {
            PaymentStatus.Success => "Успешно оплачено",
            PaymentStatus.Pending => "В обработке",
            PaymentStatus.Failed => "Отклонён",
            PaymentStatus.Indeterminate => "Статус уточняется",
            PaymentStatus.Refunded => "Возвращён",
            _ => payment.Status.ToString()
        };

        decimal vatAmount = Math.Round(payment.Amount * 20m / 120m, 2);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("================================================================");
        sb.AppendLine("                        ООО \"АВИАКАССА\"                         ");
        sb.AppendLine("                         КАССОВЫЙ ЧЕК                           ");
        sb.AppendLine("================================================================");
        sb.AppendLine($"ИНН: 7701234567                 Кассовый чек № CHK-{payment.Id:D6}");
        sb.AppendLine($"Дата и время: {payment.CreatedAt:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine($"Заказ №:            {payment.Order?.OrderNumber ?? "Н/Д"}");
        sb.AppendLine($"Способ оплаты:      {paymentMethodText}");
        sb.AppendLine($"Статус операции:    {paymentStatusText}");
        if (!string.IsNullOrEmpty(payment.ExternalTransactionId))
        {
            sb.AppendLine($"ID транзакции:      {payment.ExternalTransactionId}");
        }
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("Наименование услуги                           Кол-во       Сумма");
        sb.AppendLine($"Оплата авиабилетов по заказу                       1   {payment.Amount,10:N2} руб.");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine($"ИТОГО К ОПЛАТЕ:                                     {payment.Amount,10:N2} руб.");
        sb.AppendLine($"В том числе НДС 20%:                                {vatAmount,10:N2} руб.");
        sb.AppendLine("================================================================");
        sb.AppendLine("                      СПАСИБО ЗА ПОКУПКУ!                       ");
        sb.AppendLine("================================================================");

        return new PrintDocumentDto
        {
            Title = "Кассовый чек",
            DocumentNumber = $"CHK-{payment.Id:D6}",
            CreatedAt = payment.CreatedAt,
            FormattedContent = sb.ToString()
        };
    }

    /// <inheritdoc />
    public PrintDocumentDto GeneratePaymentReceiptForOrder(int orderId)
    {
        Payment? payment = _dbContext.Payments
            .AsNoTracking()
            .Include(p => p.Order)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault(p => p.OrderId == orderId && p.Status == PaymentStatus.Success)
            ?? _dbContext.Payments
                .AsNoTracking()
                .Include(p => p.Order)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault(p => p.OrderId == orderId);

        if (payment == null)
        {
            throw new KeyNotFoundException($"Платёж для заказа с ID {orderId} не найден.");
        }

        return GeneratePaymentReceipt(payment.Id);
    }

    /// <summary>
    /// Формирует квитанцию о возврате авиабилета.
    /// </summary>
    /// <param name="refundId">Идентификатор возврата.</param>
    /// <returns>Сформированный печатный документ.</returns>
    /// <exception cref="KeyNotFoundException">Выбрасывается, если возврат не найден.</exception>
    public PrintDocumentDto GenerateRefundReceipt(int refundId)
    {
        Refund? refund = _dbContext.Refunds
            .AsNoTracking()
            .Include(r => r.Ticket)
                .ThenInclude(t => t!.Order)
            .Include(r => r.Ticket)
                .ThenInclude(t => t!.Flight)
            .Include(r => r.Ticket)
                .ThenInclude(t => t!.Client)
            .FirstOrDefault(r => r.Id == refundId);

        if (refund == null)
        {
            throw new KeyNotFoundException($"Возврат с ID {refundId} не найден.");
        }

        string reasonText = refund.Reason switch
        {
            RefundReason.Voluntary => "Добровольный возврат пассажира",
            RefundReason.FlightCancelled => "Вынужденный возврат (отмена рейса)",
            _ => refund.Reason.ToString()
        };

        decimal originalPrice = refund.Ticket?.Price ?? 0m;
        decimal penaltyAmount = Math.Max(0m, originalPrice - refund.Amount);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("================================================================");
        sb.AppendLine("                        ООО \"АВИАКАССА\"                         ");
        sb.AppendLine("                  КВИТАНЦИЯ О ВОЗВРАТЕ БИЛЕТА                   ");
        sb.AppendLine("================================================================");
        sb.AppendLine($"Квитанция возврата:  REF-{refund.Id:D6}");
        sb.AppendLine($"Дата операции:       {refund.CreatedAt:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine($"Номер билета:        {refund.Ticket?.TicketNumber ?? "Н/Д"}");
        sb.AppendLine($"Номер заказа:        {refund.Ticket?.Order?.OrderNumber ?? "Н/Д"}");
        sb.AppendLine($"Пассажир:            {refund.Ticket?.Client?.FullName ?? "Н/Д"}");
        sb.AppendLine($"Рейс:                {refund.Ticket?.Flight?.FlightNumber ?? "Н/Д"}");
        sb.AppendLine($"Причина возврата:    {reasonText}");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine("ФИНАНСОВЫЙ РАСЧЁТ ВОЗВРАТА:");
        sb.AppendLine($"Исходная стоимость билета:       {originalPrice,10:N2} руб.");
        sb.AppendLine($"Процент возврата:                {refund.RefundPercent,10}%");
        sb.AppendLine($"Удержанный штраф авиакомпании:   {penaltyAmount,10:N2} руб.");
        sb.AppendLine("----------------------------------------------------------------");
        sb.AppendLine($"СУММА К ВЫПЛАТЕ КЛИЕНТУ:         {refund.Amount,10:N2} руб.");
        sb.AppendLine("================================================================");
        sb.AppendLine("Денежные средства подлежат возврату тем же способом, которым");
        sb.AppendLine("была произведена оплата. Срок зачисления зависит от банка.");
        sb.AppendLine("================================================================");

        return new PrintDocumentDto
        {
            Title = "Квитанция о возврате билета",
            DocumentNumber = $"REF-{refund.Id:D6}",
            CreatedAt = refund.CreatedAt,
            FormattedContent = sb.ToString()
        };
    }

    /// <summary>
    /// Формирует печатный макет аналитического отчёта.
    /// </summary>
    /// <param name="reportTitle">Название отчёта.</param>
    /// <param name="reportData">Объект с данными отчёта (FlightLoadReportDto, RouteRevenueReportDto и др.).</param>
    /// <returns>Сформированный печатный документ.</returns>
    public PrintDocumentDto GenerateReportDocument(string reportTitle, object reportData)
    {
        ArgumentNullException.ThrowIfNull(reportData);

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                            ООО \"АВИАКАССА\"                                     ");
        sb.AppendLine($"              {reportTitle.ToUpperInvariant()}");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"Дата формирования:   {_clockService.CurrentVirtualTime:dd.MM.yyyy HH:mm:ss}");

        switch (reportData)
        {
            case FlightLoadReportDto loadReport:
                BuildFlightLoadReportContent(sb, loadReport);
                break;

            case RouteRevenueReportDto revReport:
                BuildRouteRevenueReportContent(sb, revReport);
                break;

            case ChannelSalesReportDto chanReport:
                BuildChannelSalesReportContent(sb, chanReport);
                break;

            case RefundStatisticsReportDto refReport:
                BuildRefundStatisticsReportContent(sb, refReport);
                break;

            default:
                BuildGenericReportContent(sb, reportData);
                break;
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("Документ сформирован автоматически в системе «Авиакасса».");
        sb.AppendLine("================================================================================");

        return new PrintDocumentDto
        {
            Title = reportTitle,
            DocumentNumber = $"REP-{_clockService.CurrentVirtualTime:yyyyMMdd-HHmmss}",
            CreatedAt = _clockService.CurrentVirtualTime,
            FormattedContent = sb.ToString()
        };
    }

    private static void BuildFlightLoadReportContent(StringBuilder sb, FlightLoadReportDto report)
    {
        sb.AppendLine($"Период отчёта:       {report.FromDate:dd.MM.yyyy} - {report.ToDate:dd.MM.yyyy}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Рейс    Маршрут        Вылет            ВС       Мест Продано Загрузка   Бизнес   Эконом");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (FlightLoadItemDto item in report.Items)
        {
            string line = $"{item.FlightNumber,-7} {item.Route,-14} {item.DepartureTime:dd.MM.yyyy HH:mm} {item.AircraftModel,-8} {item.TotalSeats,4} {item.SoldSeats,7} {item.LoadPercentage,6:F1}% {item.BusinessLoadPercentage,7:F1}% {item.EconomyLoadPercentage,7:F1}%";
            sb.AppendLine(line);
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("СВОДНЫЕ ИТОГИ:");
        sb.AppendLine($"Всего рейсов:           {report.TotalFlights}");
        sb.AppendLine($"Общая вместимость мест: {report.TotalCapacity}");
        sb.AppendLine($"Всего продано мест:     {report.TotalSoldSeats}");
        sb.AppendLine($"Средняя загрузка:       {report.AverageLoadPercentage:F1}%");
    }

    private static void BuildRouteRevenueReportContent(StringBuilder sb, RouteRevenueReportDto report)
    {
        sb.AppendLine($"Период отчёта:       {report.FromDate:dd.MM.yyyy} - {report.ToDate:dd.MM.yyyy}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Маршрут        Города                 Продано    Вал. выручка     Возвраты  Чистая выручка");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (RouteRevenueItemDto item in report.Items)
        {
            string cities = $"{item.OriginCity} -> {item.DestinationCity}";
            if (cities.Length > 20)
            {
                cities = cities[..17] + "...";
            }

            string line = $"{item.Route,-14} {cities,-22} {item.TicketsSold,7} {item.GrossRevenue,15:N2} {item.RefundedAmount,12:N2} {item.NetRevenue,15:N2}";
            sb.AppendLine(line);
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("СВОДНЫЕ ИТОГИ:");
        sb.AppendLine($"Всего билетов продано:   {report.TotalTicketsSold}");
        sb.AppendLine($"Общая валовая выручка:   {report.TotalGrossRevenue:N2} руб.");
        sb.AppendLine($"Общая сумма возвратов:   {report.TotalRefundedAmount:N2} руб.");
        sb.AppendLine($"ИТОГОВАЯ ЧИСТАЯ ВЫРУЧКА: {report.TotalNetRevenue:N2} руб.");
    }

    private static void BuildChannelSalesReportContent(StringBuilder sb, ChannelSalesReportDto report)
    {
        sb.AppendLine($"Период отчёта:       {report.FromDate:dd.MM.yyyy} - {report.ToDate:dd.MM.yyyy}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Канал продаж           Заказов  Билетов         Сумма продаж (руб.)   Доля выручки");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (ChannelSalesItemDto item in report.Items)
        {
            string line = $"{item.ChannelName,-22} {item.OrdersCount,7} {item.TicketsCount,8} {item.TotalSalesAmount,27:N2} {item.PercentageOfTotalRevenue,13:F1}%";
            sb.AppendLine(line);
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("СВОДНЫЕ ИТОГИ:");
        sb.AppendLine($"Всего заказов:          {report.TotalOrders}");
        sb.AppendLine($"Всего билетов:          {report.TotalTickets}");
        sb.AppendLine($"ИТОГОВАЯ ВЫРУЧКА:       {report.TotalRevenue:N2} руб.");
    }

    private static void BuildRefundStatisticsReportContent(StringBuilder sb, RefundStatisticsReportDto report)
    {
        sb.AppendLine($"Период отчёта:       {report.FromDate:dd.MM.yyyy} - {report.ToDate:dd.MM.yyyy}");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("Причина возврата                    Кол-во     Сумма возвратов     Удержано штрафов");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (RefundStatisticsItemDto item in report.Items)
        {
            string line = $"{item.ReasonName,-35} {item.RefundsCount,6} {item.TotalRefundAmount,19:N2} {item.TotalPenaltyAmount,20:N2}";
            sb.AppendLine(line);
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("СВОДНЫЕ ИТОГИ:");
        sb.AppendLine($"Всего операций возврата: {report.TotalRefundsCount}");
        sb.AppendLine($"Общая сумма выплат:      {report.TotalRefundAmount:N2} руб.");
        sb.AppendLine($"Общая сумма штрафов:     {report.TotalPenaltyAmount:N2} руб.");
    }

    private static void BuildGenericReportContent(StringBuilder sb, object data)
    {
        PropertyInfo[] properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        sb.AppendLine("--------------------------------------------------------------------------------");
        foreach (PropertyInfo prop in properties)
        {
            object? val = prop.GetValue(data);
            sb.AppendLine($"{prop.Name,-30}: {val}");
        }
    }
}
