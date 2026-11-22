using System;
using AirlineTicketOffice.Core.Contracts.Flights;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Вариант выбора рейса в пульте эмулятора W-16 для перехода к моменту
/// «за 2 часа до вылета». Форматирует DTO ядра <see cref="FlightDetailsDto"/>
/// без изменения контрактов ядра.
/// </summary>
public sealed class EmulatorFlightOption
{
    private readonly FlightDetailsDto _flight;

    /// <summary>
    /// Инициализирует новый экземпляр варианта выбора рейса.
    /// </summary>
    /// <param name="flight">DTO рейса, полученный из <c>IFlightService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="flight"/> равен null.</exception>
    public EmulatorFlightOption(FlightDetailsDto flight)
    {
        _flight = flight ?? throw new ArgumentNullException(nameof(flight));
    }

    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int Id => _flight.Id;

    /// <summary>
    /// Номер рейса (например, SU-101).
    /// </summary>
    public string FlightNumber => _flight.FlightNumber;

    /// <summary>
    /// Каноническое время вылета рейса.
    /// </summary>
    public DateTime DepartureTime => _flight.DepartureTime;

    /// <summary>
    /// Время вылета для отображения «дд.ММ.гггг ЧЧ:мм».
    /// </summary>
    public string DepartureText => _flight.DepartureTime.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Целевой момент перемотки — за 2 часа до вылета.
    /// </summary>
    public DateTime TargetTime => _flight.DepartureTime.AddHours(-2);

    /// <summary>
    /// Целевой момент перемотки для отображения «дд.ММ.гггг ЧЧ:мм».
    /// </summary>
    public string TargetTimeText => TargetTime.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Маршрут в компактном виде «SVO ➔ LED».
    /// </summary>
    public string RouteText => $"{_flight.OriginAirportCode} ➔ {_flight.DestinationAirportCode}";

    /// <summary>
    /// Подпись варианта в раскрывающемся списке.
    /// </summary>
    public string DisplayText =>
        $"{FlightNumber} • {RouteText} • вылет {DepartureText} • осталось мест: {_flight.AvailableSeats}";

    /// <summary>
    /// Подпись целевого виртуального времени.
    /// </summary>
    public string TargetTimeHint => $"Цель перемотки: {TargetTimeText}";
}
