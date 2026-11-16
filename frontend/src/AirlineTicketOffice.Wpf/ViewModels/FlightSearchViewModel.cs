using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Flights;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления экрана поиска рейсов W-03.
/// Позволяет фильтровать рейсы по аэропортам вылета/прилёта, дате и числу пассажиров,
/// отображает результаты расписания с тарифами и инициирует переход к выбору мест W-04.
/// </summary>
public partial class FlightSearchViewModel : ObservableObject
{
    private readonly IFlightService _flightService;
    private readonly IDirectoryService _directoryService;
    private readonly IVirtualClockService _clockService;

    [ObservableProperty]
    private AirportDto? _selectedOriginAirport;

    [ObservableProperty]
    private AirportDto? _selectedDestinationAirport;

    [ObservableProperty]
    private DateTime _departureDate;

    [ObservableProperty]
    private int _passengersCount = 1;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private bool _hasSearched;

    [ObservableProperty]
    private ObservableCollection<AirportDto> _airports = [];

    [ObservableProperty]
    private ObservableCollection<FlightListItemDto> _flights = [];

    /// <summary>
    /// Допустимый диапазон количества пассажиров в одном заказе (от 1 до 5).
    /// </summary>
    public IReadOnlyList<int> AllowedPassengersCounts { get; } = [1, 2, 3, 4, 5];

    /// <summary>
    /// Событие выбора рейса для перехода к схеме салона W-04 с передачей рейса и числа пассажиров.
    /// </summary>
    public event Action<FlightListItemDto, int>? FlightSelected;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FlightSearchViewModel"/>.
    /// </summary>
    /// <param name="flightService">Сервис управления рейсами и расписанием.</param>
    /// <param name="directoryService">Сервис справочников авиакомпании.</param>
    /// <param name="clockService">Сервис виртуального времени.</param>
    public FlightSearchViewModel(
        IFlightService flightService,
        IDirectoryService directoryService,
        IVirtualClockService clockService)
    {
        _flightService = flightService ?? throw new ArgumentNullException(nameof(flightService));
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));

        // Устанавливаем дату вылета по умолчанию равной текущей дате виртуального времени
        DepartureDate = _clockService.CurrentVirtualTime.Date;

        // Загружаем список доступных активных аэропортов
        LoadAirports();
    }

    /// <summary>
    /// Загружает справочник аэропортов и устанавливает начальные значения по умолчанию.
    /// </summary>
    public void LoadAirports()
    {
        IReadOnlyList<AirportDto> allAirports = _directoryService.GetAllAirports();
        var activeAirports = allAirports.Where(a => a.IsActive).ToList();

        Airports = new ObservableCollection<AirportDto>(activeAirports);

        // Устанавливаем популярный маршрут по умолчанию (например, SVO -> LED или первые два доступных)
        if (Airports.Count >= 2)
        {
            SelectedOriginAirport = Airports.FirstOrDefault(a => a.Code == "SVO") ?? Airports[0];
            SelectedDestinationAirport = Airports.FirstOrDefault(a => a.Code == "LED") ?? Airports[1];
        }
    }

    /// <summary>
    /// Выполняет поиск рейсов по заданным критериям фильтрации.
    /// </summary>
    [RelayCommand]
    public void SearchFlights()
    {
        ErrorMessage = null;

        if (SelectedOriginAirport == null || SelectedDestinationAirport == null)
        {
            ErrorMessage = "Пожалуйста, выберите аэропорты вылета и прилёта.";
            return;
        }

        if (SelectedOriginAirport.Id == SelectedDestinationAirport.Id)
        {
            ErrorMessage = "Аэропорт вылета и аэропорт прилёта не могут совпадать.";
            return;
        }

        IsSearching = true;
        HasSearched = true;

        IReadOnlyList<FlightListItemDto> searchResults = _flightService.SearchFlights(
            SelectedOriginAirport.Id,
            SelectedDestinationAirport.Id,
            DepartureDate,
            PassengersCount);

        Flights = new ObservableCollection<FlightListItemDto>(searchResults);
        HasResults = Flights.Count > 0;

        if (!HasResults)
        {
            ErrorMessage = $"Рейсы по направлению {SelectedOriginAirport.City} → {SelectedDestinationAirport.City} на {DepartureDate:dd.MM.yyyy} не найдены.";
        }

        IsSearching = false;
    }

    /// <summary>
    /// Выбирает рейс и инициирует переход к интерактивной схеме мест (W-04).
    /// </summary>
    /// <param name="flight">Выбранный пользователем рейс.</param>
    [RelayCommand]
    public void SelectFlight(FlightListItemDto? flight)
    {
        if (flight == null)
        {
            return;
        }

        FlightSelected?.Invoke(flight, PassengersCount);
    }
}
