using System;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Tariffs;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка тарифной сетки W-13.
/// Дополняет DTO ядра <see cref="TariffDto"/> русскоязычными названиями городов
/// и справочными производными ценами классов обслуживания.
/// </summary>
public sealed class TariffRowViewModel
{
    /// <summary>
    /// Множитель стоимости бизнес-класса относительно базовой ставки эконома (правило M5).
    /// </summary>
    public const decimal BusinessClassMultiplier = 2.0m;

    private readonly TariffDto _tariff;

    /// <summary>
    /// Инициализирует новый экземпляр строки тарифной сетки.
    /// </summary>
    /// <param name="tariff">DTO тарифа из <c>ITariffCalculator</c>.</param>
    /// <param name="origin">Аэропорт вылета для отображения города (может быть <c>null</c>).</param>
    /// <param name="destination">Аэропорт назначения для отображения города (может быть <c>null</c>).</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="tariff"/> равен null.</exception>
    public TariffRowViewModel(TariffDto tariff, AirportDto? origin, AirportDto? destination)
    {
        _tariff = tariff ?? throw new ArgumentNullException(nameof(tariff));
        OriginCity = origin?.City ?? string.Empty;
        DestinationCity = destination?.City ?? string.Empty;
    }

    /// <summary>
    /// Уникальный идентификатор тарифа.
    /// </summary>
    public int Id => _tariff.Id;

    /// <summary>
    /// Идентификатор аэропорта вылета.
    /// </summary>
    public int OriginAirportId => _tariff.OriginAirportId;

    /// <summary>
    /// Идентификатор аэропорта назначения.
    /// </summary>
    public int DestinationAirportId => _tariff.DestinationAirportId;

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта вылета.
    /// </summary>
    public string OriginAirportCode => _tariff.OriginAirportCode;

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта назначения.
    /// </summary>
    public string DestinationAirportCode => _tariff.DestinationAirportCode;

    /// <summary>
    /// Город вылета из справочника аэропортов.
    /// </summary>
    public string OriginCity { get; }

    /// <summary>
    /// Город назначения из справочника аэропортов.
    /// </summary>
    public string DestinationCity { get; }

    /// <summary>
    /// Маршрут в компактном виде «SVO ➔ LED».
    /// </summary>
    public string RouteText => $"{_tariff.OriginAirportCode} ➔ {_tariff.DestinationAirportCode}";

    /// <summary>
    /// Маршрут с названиями городов «Москва — Санкт-Петербург».
    /// </summary>
    public string CitiesText => $"{OriginCity} — {DestinationCity}";

    /// <summary>
    /// Базовая ставка тарифа эконом-класса в рублях.
    /// </summary>
    public decimal BasePrice => _tariff.BasePrice;

    /// <summary>
    /// Фиксированный сбор за одно место багажа в рублях.
    /// </summary>
    public decimal BaggageFee => _tariff.BaggageFee;

    /// <summary>
    /// Справочная базовая ставка бизнес-класса (базовая цена × 2.0).
    /// </summary>
    public decimal BusinessBasePrice => _tariff.BasePrice * BusinessClassMultiplier;

    /// <summary>
    /// Справочная стоимость билета эконом-класса с багажом.
    /// </summary>
    public decimal EconomyWithBaggagePrice => _tariff.BasePrice + _tariff.BaggageFee;

    /// <summary>
    /// Справочная стоимость билета бизнес-класса с багажом.
    /// </summary>
    public decimal BusinessWithBaggagePrice =>
        (_tariff.BasePrice * BusinessClassMultiplier) + _tariff.BaggageFee;

    /// <summary>
    /// Проверяет попадание тарифа в текстовый фильтр по коду или городу маршрута.
    /// </summary>
    /// <param name="upperNeedle">Поисковая строка в верхнем регистре.</param>
    /// <returns><c>true</c>, если тариф соответствует фильтру.</returns>
    public bool MatchesSearch(string upperNeedle)
    {
        if (string.IsNullOrWhiteSpace(upperNeedle))
        {
            return true;
        }

        return OriginAirportCode.ToUpperInvariant().Contains(upperNeedle)
            || DestinationAirportCode.ToUpperInvariant().Contains(upperNeedle)
            || OriginCity.ToUpperInvariant().Contains(upperNeedle)
            || DestinationCity.ToUpperInvariant().Contains(upperNeedle);
    }
}
