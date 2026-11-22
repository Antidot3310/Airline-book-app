using System;
using AirlineTicketOffice.Core.Contracts.Reports;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка отчёта по выручке направлений W-14.
/// Форматирует DTO ядра <see cref="RouteRevenueItemDto"/>: маршрут, города,
/// денежные суммы и признак убыточного направления. Контракты ядра не изменяются.
/// </summary>
public sealed class RouteRevenueRowViewModel
{
    private readonly RouteRevenueItemDto _item;

    /// <summary>
    /// Инициализирует новый экземпляр строки отчёта по выручке направления.
    /// </summary>
    /// <param name="item">DTO строки отчёта, полученный из <c>IReportService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="item"/> равен null.</exception>
    public RouteRevenueRowViewModel(RouteRevenueItemDto item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    /// Маршрут в компактном виде «SVO ➔ LED».
    /// </summary>
    public string Route => string.IsNullOrWhiteSpace(_item.Route)
        ? "—"
        : _item.Route.Replace("->", "➔", StringComparison.Ordinal);

    /// <summary>
    /// Названия городов направления «Москва — Санкт-Петербург».
    /// </summary>
    public string CitiesText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_item.OriginCity) && string.IsNullOrWhiteSpace(_item.DestinationCity))
            {
                return "—";
            }

            return $"{_item.OriginCity} — {_item.DestinationCity}";
        }
    }

    /// <summary>
    /// Количество проданных билетов по направлению.
    /// </summary>
    public int TicketsSold => _item.TicketsSold;

    /// <summary>
    /// Валовая выручка от продаж по направлению.
    /// </summary>
    public decimal GrossRevenue => _item.GrossRevenue;

    /// <summary>
    /// Валовая выручка для отображения «1 250 000 ₽».
    /// </summary>
    public string GrossRevenueText => $"{_item.GrossRevenue:N0} ₽";

    /// <summary>
    /// Сумма возвратов по направлению.
    /// </summary>
    public decimal RefundedAmount => _item.RefundedAmount;

    /// <summary>
    /// Сумма возвратов для отображения «-85 000 ₽».
    /// </summary>
    public string RefundedAmountText => $"-{_item.RefundedAmount:N0} ₽";

    /// <summary>
    /// Чистая выручка по направлению (валовая выручка минус возвраты).
    /// </summary>
    public decimal NetRevenue => _item.NetRevenue;

    /// <summary>
    /// Чистая выручка для отображения «1 165 000 ₽».
    /// </summary>
    public string NetRevenueText => $"{_item.NetRevenue:N0} ₽";

    /// <summary>
    /// Признак убыточного направления: сумма возвратов превысила валовую выручку.
    /// </summary>
    public bool IsLossMaking => _item.NetRevenue < 0m;

    /// <summary>
    /// Средняя стоимость проданного билета по направлению.
    /// </summary>
    public string AverageTicketPriceText => _item.TicketsSold > 0
        ? $"{_item.GrossRevenue / _item.TicketsSold:N0} ₽"
        : "—";
}
