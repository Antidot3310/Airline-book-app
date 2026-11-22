using System;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационный вариант выбора воздушного судна для экрана W-12.
/// Содержит русскоязычную подпись эксплуатационного статуса борта
/// и не изменяет контракт <see cref="AircraftDto"/>.
/// </summary>
public sealed class AircraftOptionViewModel
{
    private readonly AircraftDto _aircraft;

    /// <summary>
    /// Инициализирует новый экземпляр варианта выбора борта.
    /// </summary>
    /// <param name="aircraft">DTO воздушного судна из <c>IDirectoryService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="aircraft"/> равен null.</exception>
    public AircraftOptionViewModel(AircraftDto aircraft)
    {
        _aircraft = aircraft ?? throw new ArgumentNullException(nameof(aircraft));
    }

    /// <summary>
    /// Уникальный идентификатор воздушного судна.
    /// </summary>
    public int Id => _aircraft.Id;

    /// <summary>
    /// Бортовой номер воздушного судна.
    /// </summary>
    public string TailNumber => _aircraft.TailNumber;

    /// <summary>
    /// Модель типа воздушного судна.
    /// </summary>
    public string AircraftTypeName => _aircraft.AircraftTypeName;

    /// <summary>
    /// Эксплуатационный статус борта из ядра.
    /// </summary>
    public AircraftStatus Status => _aircraft.Status;

    /// <summary>
    /// Русскоязычная подпись эксплуатационного статуса борта.
    /// </summary>
    public string StatusText => _aircraft.Status switch
    {
        AircraftStatus.InService => "В строю",
        AircraftStatus.Maintenance => "На техобслуживании",
        _ => _aircraft.Status.ToString()
    };

    /// <summary>
    /// Полная подпись борта «RA-89014 • Superjet 100 • В строю».
    /// </summary>
    public string DisplayText => $"{_aircraft.TailNumber} • {_aircraft.AircraftTypeName} • {StatusText}";
}
