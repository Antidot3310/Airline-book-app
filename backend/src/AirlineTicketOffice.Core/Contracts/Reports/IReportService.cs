namespace AirlineTicketOffice.Core.Contracts.Reports;

/// <summary>
/// Сервис формирования аналитической и управленческой отчётности (M10).
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Формирует отчёт по загрузке рейсов за указанный период вылетов.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по загрузке рейсов с деталями по классам обслуживания.</returns>
    FlightLoadReportDto GetFlightLoadReport(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Формирует отчёт по выручке по направлениям за указанный период вылетов.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по валовой и чистой выручке в разрезе маршрутов.</returns>
    RouteRevenueReportDto GetRevenueByRoutesReport(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Формирует отчёт по продажам в разрезе каналов (касса vs самообслуживание) за указанный период.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по объёмам продаж и долям выручки по каналам.</returns>
    ChannelSalesReportDto GetSalesByChannelsReport(DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Формирует статистический отчёт по возвратам билетов за указанный период.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по суммам выплат и удержанных штрафов в разрезе причин возвратов.</returns>
    RefundStatisticsReportDto GetRefundsStatisticsReport(DateTime fromDate, DateTime toDate);
}
