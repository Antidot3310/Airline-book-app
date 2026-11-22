using System;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка отчёта по продажам в разрезе каналов W-14.
/// Форматирует DTO ядра <see cref="ChannelSalesItemDto"/>: русскоязычное наименование
/// канала, иконку, суммы и долю выручки. Контракты ядра не изменяются.
/// </summary>
public sealed class ChannelSalesRowViewModel
{
    private readonly ChannelSalesItemDto _item;

    /// <summary>
    /// Инициализирует новый экземпляр строки отчёта по каналу продаж.
    /// </summary>
    /// <param name="item">DTO строки отчёта, полученный из <c>IReportService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="item"/> равен null.</exception>
    public ChannelSalesRowViewModel(ChannelSalesItemDto item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    /// Канал оформления заказа из доменной модели.
    /// </summary>
    public OrderChannel Channel => _item.Channel;

    /// <summary>
    /// Русскоязычное наименование канала продаж.
    /// </summary>
    public string ChannelName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_item.ChannelName))
            {
                return _item.ChannelName;
            }

            return _item.Channel == OrderChannel.Cashier ? "Касса" : "Самообслуживание";
        }
    }

    /// <summary>
    /// Символьная иконка канала продаж для таблицы.
    /// </summary>
    public string ChannelIcon => _item.Channel == OrderChannel.Cashier ? "🏦" : "🌐";

    /// <summary>
    /// Количество оформленных заказов через канал.
    /// </summary>
    public int OrdersCount => _item.OrdersCount;

    /// <summary>
    /// Количество проданных билетов через канал.
    /// </summary>
    public int TicketsCount => _item.TicketsCount;

    /// <summary>
    /// Суммарный объём продаж по каналу.
    /// </summary>
    public decimal TotalSalesAmount => _item.TotalSalesAmount;

    /// <summary>
    /// Суммарный объём продаж для отображения «1 250 000 ₽».
    /// </summary>
    public string TotalSalesAmountText => $"{_item.TotalSalesAmount:N0} ₽";

    /// <summary>
    /// Доля канала в общей выручке в процентах.
    /// </summary>
    public double PercentageOfTotalRevenue => _item.PercentageOfTotalRevenue;

    /// <summary>
    /// Доля канала в общей выручке для отображения «62,5 %».
    /// </summary>
    public string RevenueShareText => $"{_item.PercentageOfTotalRevenue:F1} %";

    /// <summary>
    /// Средняя стоимость билета по каналу продаж.
    /// </summary>
    public string AverageTicketText => _item.TicketsCount > 0
        ? $"{_item.TotalSalesAmount / _item.TicketsCount:N0} ₽"
        : "—";
}
