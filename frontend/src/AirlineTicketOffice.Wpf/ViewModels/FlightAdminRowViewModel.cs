using System;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка расписания для экрана W-12 «Управление рейсами».
/// Выполняет исключительно форматирование DTO ядра <see cref="FlightDetailsDto"/>:
/// русскоязычные подписи статусов, маршрута, борта и загрузки кресел.
/// Контракты <c>AirlineTicketOffice.Core.Contracts.*</c> при этом не изменяются.
/// </summary>
public sealed class FlightAdminRowViewModel
{
    private readonly FlightDetailsDto _flight;
    private readonly DateTime _virtualNow;
    private readonly bool _canManageFlights;

    /// <summary>
    /// Инициализирует новый экземпляр строки расписания.
    /// </summary>
    /// <param name="flight">DTO рейса, полученный из <c>IFlightService</c>.</param>
    /// <param name="virtualNow">Текущее виртуальное время ядра для относительных подписей.</param>
    /// <param name="canManageFlights">Признак наличия у сессии права <c>ManageFlights</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="flight"/> равен null.</exception>
    public FlightAdminRowViewModel(FlightDetailsDto flight, DateTime virtualNow, bool canManageFlights)
    {
        _flight = flight ?? throw new ArgumentNullException(nameof(flight));
        _virtualNow = virtualNow;
        _canManageFlights = canManageFlights;
    }

    /// <summary>
    /// Уникальный идентификатор рейса.
    /// </summary>
    public int Id => _flight.Id;

    /// <summary>
    /// Номер рейса (например, SU-101).
    /// </summary>
    public string FlightNumber => _flight.FlightNumber;

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта вылета.
    /// </summary>
    public string OriginAirportCode => _flight.OriginAirportCode;

    /// <summary>
    /// Трёхбуквенный IATA-код аэропорта назначения.
    /// </summary>
    public string DestinationAirportCode => _flight.DestinationAirportCode;

    /// <summary>
    /// Город вылета.
    /// </summary>
    public string OriginCity => _flight.OriginCity;

    /// <summary>
    /// Город назначения.
    /// </summary>
    public string DestinationCity => _flight.DestinationCity;

    /// <summary>
    /// Маршрут в компактном виде «SVO ➔ LED».
    /// </summary>
    public string RouteText => $"{_flight.OriginAirportCode} ➔ {_flight.DestinationAirportCode}";

    /// <summary>
    /// Маршрут с названиями городов «Москва — Санкт-Петербург».
    /// </summary>
    public string CitiesText => $"{_flight.OriginCity} — {_flight.DestinationCity}";

    /// <summary>
    /// Идентификатор назначенного воздушного судна.
    /// </summary>
    public int AircraftId => _flight.AircraftId;

    /// <summary>
    /// Бортовой номер воздушного судна.
    /// </summary>
    public string AircraftTailNumber => _flight.AircraftTailNumber;

    /// <summary>
    /// Модель воздушного судна.
    /// </summary>
    public string AircraftModel => _flight.AircraftModel;

    /// <summary>
    /// Борт в формате «RA-89014 • Superjet 100».
    /// </summary>
    public string AircraftText => $"{_flight.AircraftTailNumber} • {_flight.AircraftModel}";

    /// <summary>
    /// Каноническое время вылета рейса.
    /// </summary>
    public DateTime DepartureTime => _flight.DepartureTime;

    /// <summary>
    /// Каноническое время прилёта рейса.
    /// </summary>
    public DateTime ArrivalTime => _flight.ArrivalTime;

    /// <summary>
    /// Время вылета для отображения «дд.ММ.гггг ЧЧ:мм».
    /// </summary>
    public string DepartureText => _flight.DepartureTime.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Время прилёта для отображения «дд.ММ.гггг ЧЧ:мм».
    /// </summary>
    public string ArrivalText => _flight.ArrivalTime.ToString("dd.MM.yyyy HH:mm");

    /// <summary>
    /// Продолжительность полёта в формате «2 ч 15 мин».
    /// </summary>
    public string DurationText
    {
        get
        {
            TimeSpan duration = _flight.ArrivalTime - _flight.DepartureTime;
            return duration <= TimeSpan.Zero
                ? "—"
                : $"{(int)duration.TotalHours} ч {duration.Minutes:00} мин";
        }
    }

    /// <summary>
    /// Общее количество кресел на борту.
    /// </summary>
    public int TotalSeats => _flight.TotalSeats;

    /// <summary>
    /// Количество свободных для бронирования кресел.
    /// </summary>
    public int AvailableSeats => _flight.AvailableSeats;

    /// <summary>
    /// Количество занятых (проданных, удержанных или заблокированных) кресел.
    /// </summary>
    public int OccupiedSeats => Math.Max(0, _flight.TotalSeats - _flight.AvailableSeats);

    /// <summary>
    /// Подпись загрузки «12 из 87 свободно».
    /// </summary>
    public string SeatsText => $"{_flight.AvailableSeats} из {_flight.TotalSeats} свободно";

    /// <summary>
    /// Загрузка борта в процентах для индикатора заполняемости.
    /// </summary>
    public double OccupancyPercent =>
        _flight.TotalSeats <= 0 ? 0d : (double)OccupiedSeats / _flight.TotalSeats * 100d;

    /// <summary>
    /// Эксплуатационный статус рейса из ядра.
    /// </summary>
    public FlightStatus Status => _flight.Status;

    /// <summary>
    /// Русскоязычная подпись статуса рейса для интерфейса.
    /// </summary>
    public string StatusText => _flight.Status switch
    {
        FlightStatus.Scheduled => "Запланирован",
        FlightStatus.Departed => "Вылетел",
        FlightStatus.Cancelled => "Отменён",
        _ => _flight.Status.ToString()
    };

    /// <summary>
    /// Пояснение к статусу относительно виртуального времени ядра.
    /// </summary>
    public string DepartureRelativeText
    {
        get
        {
            if (_flight.Status == FlightStatus.Cancelled)
            {
                return "Рейс отменён — места заблокированы";
            }

            if (_flight.Status == FlightStatus.Departed)
            {
                return "Рейс выполнен";
            }

            TimeSpan left = _flight.DepartureTime - _virtualNow;
            if (left <= TimeSpan.Zero)
            {
                return "Время вылета прошло";
            }

            return left.TotalDays >= 1d
                ? $"До вылета {left.Days} дн. {left.Hours} ч."
                : $"До вылета {left.Hours} ч. {left.Minutes} мин.";
        }
    }

    /// <summary>
    /// Признак доступности замены борта: рейс запланирован и у сессии есть право управления рейсами.
    /// </summary>
    public bool CanChangeAircraft => _canManageFlights && _flight.Status == FlightStatus.Scheduled;

    /// <summary>
    /// Признак доступности отмены рейса: рейс ещё не вылетел и у сессии есть право управления рейсами.
    /// </summary>
    public bool CanCancel => _canManageFlights && _flight.Status == FlightStatus.Scheduled;

    /// <summary>
    /// Проверяет соответствие рейса выбранному статусному фильтру.
    /// </summary>
    /// <param name="status">Статус фильтра; <c>null</c> означает «все рейсы».</param>
    /// <returns><c>true</c>, если рейс попадает в фильтр.</returns>
    public bool MatchesStatus(FlightStatus? status) => status is null || _flight.Status == status.Value;
}
