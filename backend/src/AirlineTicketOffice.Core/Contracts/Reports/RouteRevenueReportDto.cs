namespace AirlineTicketOffice.Core.Contracts.Reports;

/// <summary>
/// DTO аналитического отчёта по выручке по направлениям за выбранный период.
/// </summary>
public class RouteRevenueReportDto
{
    /// <summary>
    /// Начальная дата периода отчёта.
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// Конечная дата периода отчёта.
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Общее количество проданных билетов по всем направлениям.
    /// </summary>
    public int TotalTicketsSold { get; set; }

    /// <summary>
    /// Общая валовая выручка от продажи билетов.
    /// </summary>
    public decimal TotalGrossRevenue { get; set; }

    /// <summary>
    /// Общая сумма возвратов по билетам.
    /// </summary>
    public decimal TotalRefundedAmount { get; set; }

    /// <summary>
    /// Общая чистая выручка (валовая выручка минус возвраты).
    /// </summary>
    public decimal TotalNetRevenue { get; set; }

    /// <summary>
    /// Детализированные строки выручки по направлениям.
    /// </summary>
    public IReadOnlyList<RouteRevenueItemDto> Items { get; set; } = [];
}

/// <summary>
/// Элемент отчёта по выручке конкретного направления (маршрута).
/// </summary>
public class RouteRevenueItemDto
{
    /// <summary>
    /// Маршрут рейса в формате "КодОткуда -> КодКуда" (например, "SVO -> LED").
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// Город вылета.
    /// </summary>
    public string OriginCity { get; set; } = string.Empty;

    /// <summary>
    /// Город прилёта.
    /// </summary>
    public string DestinationCity { get; set; } = string.Empty;

    /// <summary>
    /// Количество проданных билетов по направлению.
    /// </summary>
    public int TicketsSold { get; set; }

    /// <summary>
    /// Валовая выручка от продаж по направлению.
    /// </summary>
    public decimal GrossRevenue { get; set; }

    /// <summary>
    /// Сумма возвратов по направлению.
    /// </summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>
    /// Чистая выручка по направлению (валовая выручка минус возвраты).
    /// </summary>
    public decimal NetRevenue { get; set; }
}
