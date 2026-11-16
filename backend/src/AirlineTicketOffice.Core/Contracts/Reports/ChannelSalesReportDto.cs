using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Reports;

/// <summary>
/// DTO аналитического отчёта по продажам в разрезе каналов за выбранный период.
/// </summary>
public class ChannelSalesReportDto
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
    /// Общее количество оплаченных заказов по всем каналам.
    /// </summary>
    public int TotalOrders { get; set; }

    /// <summary>
    /// Общее количество проданных билетов по всем каналам.
    /// </summary>
    public int TotalTickets { get; set; }

    /// <summary>
    /// Общая сумма выручки по всем каналам продаж.
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Детализированные строки продаж по каналам.
    /// </summary>
    public IReadOnlyList<ChannelSalesItemDto> Items { get; set; } = [];
}

/// <summary>
/// Элемент отчёта по продажам конкретного канала.
/// </summary>
public class ChannelSalesItemDto
{
    /// <summary>
    /// Канал оформления заказа (Касса или Самообслуживание).
    /// </summary>
    public OrderChannel Channel { get; set; }

    /// <summary>
    /// Человекочитаемое наименование канала продаж ("Касса" или "Самообслуживание").
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Количество оформленных заказов через данный канал.
    /// </summary>
    public int OrdersCount { get; set; }

    /// <summary>
    /// Количество проданных билетов через данный канал.
    /// </summary>
    public int TicketsCount { get; set; }

    /// <summary>
    /// Суммарный объём продаж в рублях по данному каналу.
    /// </summary>
    public decimal TotalSalesAmount { get; set; }

    /// <summary>
    /// Доля канала в общей выручке в процентах (0-100%).
    /// </summary>
    public double PercentageOfTotalRevenue { get; set; }
}
