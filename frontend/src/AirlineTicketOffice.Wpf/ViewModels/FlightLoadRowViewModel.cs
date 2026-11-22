using System;
using AirlineTicketOffice.Core.Contracts.Reports;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка отчёта по загрузке рейсов W-14.
/// Выполняет исключительно форматирование DTO ядра <see cref="FlightLoadItemDto"/>:
/// маршрут, время вылета, занятость мест по классам обслуживания и русскоязычную
/// оценку уровня загрузки. Контракты <c>AirlineTicketOffice.Core.Contracts.*</c> не изменяются.
/// </summary>
public sealed class FlightLoadRowViewModel
{
    private readonly FlightLoadItemDto _item;

    /// <summary>
    /// Инициализирует новый экземпляр строки отчёта по загрузке рейса.
    /// </summary>
    /// <param name="item">DTO строки отчёта, полученный из <c>IReportService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="item"/> равен null.</exception>
    public FlightLoadRowViewModel(FlightLoadItemDto item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId => _item.FlightId;

    /// <summary>
    /// Номер рейса (например, SU-101).
    /// </summary>
    public string FlightNumber =>
        string.IsNullOrWhiteSpace(_item.FlightNumber) ? "—" : _item.FlightNumber;

    /// <summary>
    /// Маршрут в компактном виде «SVO ➔ LED».
    /// </summary>
    public string Route => FormatRoute(_item.Route);

    /// <summary>
    /// Каноническое время вылета рейса.
    /// </summary>
    public DateTime DepartureTime => _item.DepartureTime;

    /// <summary>
    /// Время вылета для отображения «дд.ММ.гггг ЧЧ:мм».
    /// </summary>
    public string DepartureText => _item.DepartureTime.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Модель воздушного судна, выполняющего рейс.
    /// </summary>
    public string AircraftModel =>
        string.IsNullOrWhiteSpace(_item.AircraftModel) ? "—" : _item.AircraftModel;

    /// <summary>
    /// Общее количество мест на рейсе.
    /// </summary>
    public int TotalSeats => _item.TotalSeats;

    /// <summary>
    /// Количество проданных мест на рейсе.
    /// </summary>
    public int SoldSeats => _item.SoldSeats;

    /// <summary>
    /// Количество свободных мест на рейсе.
    /// </summary>
    public int FreeSeats => Math.Max(0, _item.TotalSeats - _item.SoldSeats);

    /// <summary>
    /// Процент занятости мест на рейсе (0–100 %).
    /// </summary>
    public double LoadPercentage => _item.LoadPercentage;

    /// <summary>
    /// Подпись загрузки рейса «42,5 %».
    /// </summary>
    public string LoadPercentText => $"{_item.LoadPercentage:F1} %";

    /// <summary>
    /// Подпись продаж «18 из 87».
    /// </summary>
    public string SeatsText => $"{_item.SoldSeats} из {_item.TotalSeats}";

    /// <summary>
    /// Подпись занятости мест бизнес-класса «3 из 12 (25,0 %)».
    /// </summary>
    public string BusinessLoadText =>
        FormatSoldSeats(_item.BusinessSeatsSold, _item.BusinessSeatsTotal, _item.BusinessLoadPercentage);

    /// <summary>
    /// Подпись занятости мест эконом-класса «15 из 75 (20,0 %)».
    /// </summary>
    public string EconomyLoadText =>
        FormatSoldSeats(_item.EconomySeatsSold, _item.EconomySeatsTotal, _item.EconomyLoadPercentage);

    /// <summary>
    /// Признак низкой загрузки рейса (менее 50 %).
    /// </summary>
    public bool IsLowLoad => _item.LoadPercentage < 50d;

    /// <summary>
    /// Признак высокой загрузки рейса (85 % и более).
    /// </summary>
    public bool IsHighLoad => _item.LoadPercentage >= 85d;

    /// <summary>
    /// Русскоязычная оценка уровня загрузки рейса для подсказки в таблице.
    /// </summary>
    public string LoadLevelText => _item.LoadPercentage switch
    {
        <= 0d => "Продаж нет",
        < 50d => "Низкая загрузка",
        < 75d => "Средняя загрузка",
        < 90d => "Высокая загрузка",
        _ => "Максимальная загрузка"
    };

    private static string FormatRoute(string? route)
    {
        return string.IsNullOrWhiteSpace(route)
            ? "—"
            : route.Replace("->", "➔", StringComparison.Ordinal);
    }

    private static string FormatSoldSeats(int sold, int total, double percent)
    {
        return total <= 0
            ? "—"
            : $"{sold} из {total} ({percent:F1} %)";
    }
}
