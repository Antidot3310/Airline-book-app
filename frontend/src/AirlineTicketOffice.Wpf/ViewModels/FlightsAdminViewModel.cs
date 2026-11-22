using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Управление рейсами» W-12.
/// Обеспечивает фильтрацию расписания по статусу, дате и номеру,
/// создание рейсов с проверкой статуса борта (TC-11), замену воздушного судна (TC-09/10)
/// и отмену рейса с массовым 100% возвратом через <c>IRefundService</c> (TC-08).
/// Все операции выполняются через контракты ядра и учитывают виртуальное системное время.
/// </summary>
public partial class FlightsAdminViewModel : ObservableObject, IDisposable
{
    private readonly IFlightService _flightService;
    private readonly IDirectoryService _directoryService;
    private readonly IRefundService _refundService;
    private readonly IVirtualClockService _clockService;
    private readonly ISessionService _sessionService;

    private bool _isSynchronizing;

    #region Права сессии и виртуальное время

    /// <summary>
    /// Признак наличия у текущей сессии права <see cref="SessionPermissions.ManageFlights"/>.
    /// </summary>
    public bool CanManageFlights { get; }

    /// <summary>
    /// Признак режима только для чтения: у сессии нет права управления рейсами.
    /// </summary>
    public bool IsReadOnlyMode => !CanManageFlights;

    /// <summary>
    /// Текущее виртуальное время ядра для отображения в шапке раздела.
    /// </summary>
    [ObservableProperty]
    private string _virtualNowText = string.Empty;

    #endregion

    #region Фильтрация рейсов

    /// <summary>
    /// Варианты статусного фильтра с русскоязычными подписями и счётчиками.
    /// </summary>
    public ObservableCollection<FlightStatusFilterOption> StatusFilters { get; }

    [ObservableProperty]
    private FlightStatusFilterOption? _selectedStatusFilter;

    [ObservableProperty]
    private string _filterFlightNumber = string.Empty;

    [ObservableProperty]
    private bool _filterByDateEnabled;

    [ObservableProperty]
    private DateTime _filterDate;

    [ObservableProperty]
    private ObservableCollection<FlightAdminRowViewModel> _allFlights = [];

    [ObservableProperty]
    private ObservableCollection<FlightAdminRowViewModel> _filteredFlights = [];

    [ObservableProperty]
    private int _totalFlightsCount;

    [ObservableProperty]
    private int _scheduledFlightsCount;

    [ObservableProperty]
    private int _departedFlightsCount;

    [ObservableProperty]
    private int _cancelledFlightsCount;

    /// <summary>
    /// Признак отсутствия рейсов, подходящих под выбранные фильтры.
    /// </summary>
    public bool HasNoFlights => FilteredFlights.Count == 0;

    /// <summary>
    /// Признак наличия рейсов, подходящих под выбранные фильтры.
    /// </summary>
    public bool HasFlights => !HasNoFlights;

    /// <summary>
    /// Сводка общего количества рейсов для панели фильтров.
    /// </summary>
    public string TotalFlightsText => $"Всего: {TotalFlightsCount}";

    /// <summary>
    /// Сводка количества запланированных рейсов.
    /// </summary>
    public string ScheduledFlightsText => $"Запланировано: {ScheduledFlightsCount}";

    /// <summary>
    /// Сводка количества вылетевших рейсов.
    /// </summary>
    public string DepartedFlightsText => $"Вылетело: {DepartedFlightsCount}";

    /// <summary>
    /// Сводка количества отменённых рейсов.
    /// </summary>
    public string CancelledFlightsText => $"Отменено: {CancelledFlightsCount}";

    partial void OnTotalFlightsCountChanged(int value) => OnPropertyChanged(nameof(TotalFlightsText));

    partial void OnScheduledFlightsCountChanged(int value) => OnPropertyChanged(nameof(ScheduledFlightsText));

    partial void OnDepartedFlightsCountChanged(int value) => OnPropertyChanged(nameof(DepartedFlightsText));

    partial void OnCancelledFlightsCountChanged(int value) => OnPropertyChanged(nameof(CancelledFlightsText));

    #endregion

    #region Форма создания рейса

    [ObservableProperty]
    private bool _isCreatingFlight;

    [ObservableProperty]
    private string _newFlightNumber = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AirportDto> _originAirports = [];

    [ObservableProperty]
    private ObservableCollection<AirportDto> _destinationAirports = [];

    [ObservableProperty]
    private AirportDto? _selectedOriginAirport;

    [ObservableProperty]
    private AirportDto? _selectedDestinationAirport;

    [ObservableProperty]
    private DateTime _departureDate;

    [ObservableProperty]
    private string _departureTimeText = "10:00";

    [ObservableProperty]
    private DateTime _arrivalDate;

    [ObservableProperty]
    private string _arrivalTimeText = "12:30";

    /// <summary>
    /// Борты в строю, доступные для назначения на новый рейс.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AircraftOptionViewModel> _availableAircraftList = [];

    [ObservableProperty]
    private AircraftOptionViewModel? _selectedAircraft;

    #endregion

    #region Замена воздушного судна

    [ObservableProperty]
    private bool _isChangingAircraft;

    [ObservableProperty]
    private FlightAdminRowViewModel? _flightForAircraftChange;

    [ObservableProperty]
    private ObservableCollection<AircraftOptionViewModel> _replacementAircraftList = [];

    [ObservableProperty]
    private AircraftOptionViewModel? _selectedNewAircraft;

    /// <summary>
    /// Заголовок панели замены борта с номером выбранного рейса.
    /// </summary>
    public string ChangeAircraftTitle => FlightForAircraftChange is null
        ? "Замена воздушного судна"
        : $"Замена борта на рейсе {FlightForAircraftChange.FlightNumber}";

    #endregion

    #region Состояние и сообщения

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="FlightsAdminViewModel"/>.
    /// </summary>
    /// <param name="flightService">Сервис расписания рейсов M4.</param>
    /// <param name="directoryService">Сервис справочников аэропортов и бортов M3.</param>
    /// <param name="refundService">Сервис массовых возвратов при отмене рейса M9.</param>
    /// <param name="clockService">Сервис виртуального системного времени M12.</param>
    /// <param name="sessionService">Сервис текущей сессии для проверки прав M1.</param>
    public FlightsAdminViewModel(
        IFlightService flightService,
        IDirectoryService directoryService,
        IRefundService refundService,
        IVirtualClockService clockService,
        ISessionService sessionService)
    {
        _flightService = flightService ?? throw new ArgumentNullException(nameof(flightService));
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        CanManageFlights = _sessionService.HasPermission(SessionPermissions.ManageFlights);

        StatusFilters =
        [
            new FlightStatusFilterOption("Все рейсы", null),
            new FlightStatusFilterOption("Запланированные", FlightStatus.Scheduled),
            new FlightStatusFilterOption("Вылетевшие", FlightStatus.Departed),
            new FlightStatusFilterOption("Отменённые", FlightStatus.Cancelled)
        ];

        _isSynchronizing = true;
        SelectedStatusFilter = StatusFilters[0];
        StatusFilters[0].IsSelected = true;

        DateTime virtualNow = _clockService.CurrentVirtualTime;
        FilterDate = virtualNow.Date;
        DepartureDate = virtualNow.Date.AddDays(1);
        ArrivalDate = virtualNow.Date.AddDays(1);
        _isSynchronizing = false;

        UpdateVirtualNowText(virtualNow);

        _clockService.TimeChanged += OnVirtualTimeChanged;
        _clockService.FlightStatusChanged += OnFlightStatusChanged;

        RefreshData();
    }

    /// <summary>
    /// Перезагружает расписание, справочники аэропортов и доступные борты.
    /// </summary>
    [RelayCommand]
    public void RefreshData()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            LoadFlights();
            LoadDirectories();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки расписания: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Загружает рейсы из ядра и пересчитывает счётчики статусных фильтров.
    /// </summary>
    private void LoadFlights()
    {
        DateTime virtualNow = _clockService.CurrentVirtualTime;
        UpdateVirtualNowText(virtualNow);

        IReadOnlyList<FlightDetailsDto> flights = _flightService.GetAllFlights();
        AllFlights = new ObservableCollection<FlightAdminRowViewModel>(
            flights.Select(f => new FlightAdminRowViewModel(f, virtualNow, CanManageFlights)));

        TotalFlightsCount = AllFlights.Count;
        ScheduledFlightsCount = AllFlights.Count(f => f.Status == FlightStatus.Scheduled);
        DepartedFlightsCount = AllFlights.Count(f => f.Status == FlightStatus.Departed);
        CancelledFlightsCount = AllFlights.Count(f => f.Status == FlightStatus.Cancelled);

        foreach (FlightStatusFilterOption option in StatusFilters)
        {
            option.Count = AllFlights.Count(f => f.MatchesStatus(option.Status));
        }

        ApplyFilter();
    }

    /// <summary>
    /// Загружает активные аэропорты и борты, находящиеся в строю.
    /// </summary>
    private void LoadDirectories()
    {
        List<AirportDto> airports = _directoryService.GetAllAirports().Where(a => a.IsActive).ToList();
        int originId = SelectedOriginAirport?.Id ?? 0;
        int destinationId = SelectedDestinationAirport?.Id ?? 0;

        OriginAirports = new ObservableCollection<AirportDto>(airports);
        DestinationAirports = new ObservableCollection<AirportDto>(airports);

        SelectedOriginAirport = airports.FirstOrDefault(a => a.Id == originId) ?? airports.FirstOrDefault();
        SelectedDestinationAirport = airports.FirstOrDefault(a => a.Id == destinationId)
            ?? airports.FirstOrDefault(a => a.Id != SelectedOriginAirport?.Id);

        List<AircraftOptionViewModel> inServiceAircraft = _directoryService.GetAllAircrafts()
            .Where(a => a.IsActive && a.Status == AircraftStatus.InService)
            .Select(a => new AircraftOptionViewModel(a))
            .ToList();

        int aircraftId = SelectedAircraft?.Id ?? 0;
        AvailableAircraftList = new ObservableCollection<AircraftOptionViewModel>(inServiceAircraft);
        SelectedAircraft = inServiceAircraft.FirstOrDefault(a => a.Id == aircraftId)
            ?? inServiceAircraft.FirstOrDefault();

        if (IsChangingAircraft && FlightForAircraftChange is not null)
        {
            ConfigureReplacementAircraftList(FlightForAircraftChange.AircraftId);
        }
    }

    /// <summary>
    /// Применяет статусный, датный и текстовый фильтры к загруженному расписанию.
    /// </summary>
    public void ApplyFilter()
    {
        if (_isSynchronizing)
        {
            return;
        }

        IEnumerable<FlightAdminRowViewModel> query = AllFlights;

        if (SelectedStatusFilter?.Status is FlightStatus status)
        {
            query = query.Where(f => f.Status == status);
        }

        if (FilterByDateEnabled)
        {
            DateTime filterDay = FilterDate.Date;
            query = query.Where(f => f.DepartureTime.Date == filterDay);
        }

        string needle = FilterFlightNumber.Trim().ToUpperInvariant();
        if (needle.Length > 0)
        {
            query = query.Where(f =>
                f.FlightNumber.ToUpperInvariant().Contains(needle)
                || f.OriginAirportCode.ToUpperInvariant().Contains(needle)
                || f.DestinationAirportCode.ToUpperInvariant().Contains(needle)
                || f.OriginCity.ToUpperInvariant().Contains(needle)
                || f.DestinationCity.ToUpperInvariant().Contains(needle));
        }

        FilteredFlights = new ObservableCollection<FlightAdminRowViewModel>(query);
    }

    partial void OnSelectedStatusFilterChanged(FlightStatusFilterOption? value) => ApplyFilter();

    partial void OnFilterFlightNumberChanged(string value) => ApplyFilter();

    partial void OnFilterByDateEnabledChanged(bool value) => ApplyFilter();

    partial void OnFilterDateChanged(DateTime value)
    {
        if (FilterByDateEnabled)
        {
            ApplyFilter();
        }
    }

    partial void OnDepartureDateChanged(DateTime value)
    {
        if (ArrivalDate.Date < value.Date)
        {
            ArrivalDate = value.Date;
        }
    }

    partial void OnFilteredFlightsChanged(ObservableCollection<FlightAdminRowViewModel> value)
    {
        OnPropertyChanged(nameof(HasNoFlights));
        OnPropertyChanged(nameof(HasFlights));
    }

    partial void OnFlightForAircraftChangeChanged(FlightAdminRowViewModel? value)
    {
        OnPropertyChanged(nameof(ChangeAircraftTitle));
    }

    /// <summary>
    /// Переключает активный статусный фильтр.
    /// </summary>
    /// <param name="option">Выбранный вариант фильтра.</param>
    [RelayCommand]
    public void SelectStatusFilter(FlightStatusFilterOption? option)
    {
        if (option is null)
        {
            return;
        }

        _isSynchronizing = true;
        foreach (FlightStatusFilterOption filter in StatusFilters)
        {
            filter.IsSelected = ReferenceEquals(filter, option);
        }

        SelectedStatusFilter = option;
        _isSynchronizing = false;

        ApplyFilter();
    }

    /// <summary>
    /// Сбрасывает все фильтры расписания к исходному состоянию.
    /// </summary>
    [RelayCommand]
    public void ResetFilters()
    {
        _isSynchronizing = true;

        FilterFlightNumber = string.Empty;
        FilterByDateEnabled = false;

        foreach (FlightStatusFilterOption filter in StatusFilters)
        {
            filter.IsSelected = ReferenceEquals(filter, StatusFilters[0]);
        }

        SelectedStatusFilter = StatusFilters[0];
        _isSynchronizing = false;

        ApplyFilter();
    }

    #region Создание нового рейса

    /// <summary>
    /// Открывает панель создания нового рейса.
    /// </summary>
    [RelayCommand]
    public void StartCreateFlight()
    {
        if (!EnsureManagePermission("создание рейса"))
        {
            return;
        }

        IsCreatingFlight = true;
        IsChangingAircraft = false;
        StatusMessage = null;

        DateTime tomorrow = _clockService.CurrentVirtualTime.Date.AddDays(1);
        DepartureDate = tomorrow;
        ArrivalDate = tomorrow;
        DepartureTimeText = "10:00";
        ArrivalTimeText = "12:30";
    }

    /// <summary>
    /// Закрывает панель создания рейса.
    /// </summary>
    [RelayCommand]
    public void CancelCreateFlight()
    {
        IsCreatingFlight = false;
        StatusMessage = null;
    }

    /// <summary>
    /// Создаёт новый рейс с автогенерацией посадочных мест и проверкой статуса борта (TC-11).
    /// </summary>
    [RelayCommand]
    public void CreateFlight()
    {
        if (!EnsureManagePermission("создание рейса"))
        {
            return;
        }

        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewFlightNumber))
        {
            StatusMessage = "Введите номер рейса (например, SU-105).";
            return;
        }

        string flightNumber = NewFlightNumber.Trim().ToUpperInvariant();
        if (flightNumber.Length is < 3 or > 10 ||
            !flightNumber.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            StatusMessage = "Номер рейса должен содержать от 3 до 10 символов: латинские буквы, цифры и дефис (например, SU-105).";
            return;
        }

        if (SelectedOriginAirport is null || SelectedDestinationAirport is null)
        {
            StatusMessage = "Выберите аэропорт вылета и назначения.";
            return;
        }

        if (SelectedOriginAirport.Id == SelectedDestinationAirport.Id)
        {
            StatusMessage = "Аэропорт вылета и назначения не могут совпадать.";
            return;
        }

        if (SelectedAircraft is null)
        {
            StatusMessage = "Выберите воздушное судно для рейса.";
            return;
        }

        if (!TryParseTimeOfDay(DepartureTimeText, out TimeSpan departureTime))
        {
            StatusMessage = "Некорректное время вылета. Укажите время в формате ЧЧ:ММ (например, 10:00).";
            return;
        }

        if (!TryParseTimeOfDay(ArrivalTimeText, out TimeSpan arrivalTime))
        {
            StatusMessage = "Некорректное время прилёта. Укажите время в формате ЧЧ:ММ (например, 12:30).";
            return;
        }

        DateTime fullDeparture = DepartureDate.Date + departureTime;
        DateTime fullArrival = ArrivalDate.Date + arrivalTime;

        DateTime virtualNow = _clockService.CurrentVirtualTime;
        if (fullDeparture <= virtualNow)
        {
            StatusMessage = $"Время вылета должно быть позже текущего виртуального времени ({virtualNow:dd.MM.yyyy HH:mm}).";
            return;
        }

        if (fullArrival <= fullDeparture)
        {
            StatusMessage = "Время прилёта должно быть позже времени вылета.";
            return;
        }

        var request = new CreateFlightRequest
        {
            FlightNumber = flightNumber,
            OriginAirportId = SelectedOriginAirport.Id,
            DestinationAirportId = SelectedDestinationAirport.Id,
            AircraftId = SelectedAircraft.Id,
            DepartureTime = fullDeparture,
            ArrivalTime = fullArrival
        };

        Result<int> result = _flightService.CreateFlight(request);
        if (result.IsFailure)
        {
            StatusMessage = $"Ошибка создания рейса: {result.ErrorMessage}";
            return;
        }

        IsCreatingFlight = false;
        NewFlightNumber = string.Empty;

        RefreshData();
        StatusMessage = $"Рейс {flightNumber} успешно создан, посадочные места сгенерированы автоматически.";
    }

    #endregion

    #region Смена борта (TC-09 / TC-10)

    /// <summary>
    /// Открывает панель замены воздушного судна для выбранного рейса.
    /// </summary>
    /// <param name="row">Выбранная строка расписания.</param>
    [RelayCommand]
    public void OpenChangeAircraft(FlightAdminRowViewModel? row)
    {
        if (!EnsureManagePermission("замена воздушного судна"))
        {
            return;
        }

        if (row is null)
        {
            return;
        }

        if (row.Status == FlightStatus.Cancelled)
        {
            StatusMessage = $"Рейс {row.FlightNumber} отменён — замена борта недоступна.";
            return;
        }

        if (row.Status == FlightStatus.Departed)
        {
            StatusMessage = $"Рейс {row.FlightNumber} уже вылетел — замена борта недоступна.";
            return;
        }

        FlightForAircraftChange = row;
        IsChangingAircraft = true;
        IsCreatingFlight = false;
        StatusMessage = null;

        ConfigureReplacementAircraftList(row.AircraftId);
        if (ReplacementAircraftList.Count == 0)
        {
            StatusMessage = "В строю нет других воздушных судов для замены. Проверьте справочник бортов (W-11).";
        }
    }

    /// <summary>
    /// Закрывает панель замены борта.
    /// </summary>
    [RelayCommand]
    public void CancelChangeAircraft()
    {
        IsChangingAircraft = false;
        FlightForAircraftChange = null;
        SelectedNewAircraft = null;
        StatusMessage = null;
    }

    /// <summary>
    /// Выполняет замену борта с проверкой запрета при проданных билетах (TC-10).
    /// </summary>
    [RelayCommand]
    public void ConfirmChangeAircraft()
    {
        if (!EnsureManagePermission("замена воздушного судна"))
        {
            return;
        }

        if (FlightForAircraftChange is null)
        {
            StatusMessage = "Выберите рейс для замены воздушного судна.";
            return;
        }

        if (SelectedNewAircraft is null)
        {
            StatusMessage = "Выберите новый борт из списка доступных воздушных судов.";
            return;
        }

        if (SelectedNewAircraft.Id == FlightForAircraftChange.AircraftId)
        {
            StatusMessage = "Новый борт совпадает с текущим — выберите другое воздушное судно.";
            return;
        }

        StatusMessage = null;
        Result result = _flightService.ChangeAircraft(FlightForAircraftChange.Id, SelectedNewAircraft.Id);

        if (result.IsFailure)
        {
            StatusMessage = $"Запрет смены борта: {result.ErrorMessage}";
            return;
        }

        string successMessage =
            $"Борт на рейсе {FlightForAircraftChange.FlightNumber} заменён на {SelectedNewAircraft.TailNumber} ({SelectedNewAircraft.AircraftTypeName}). " +
            "Посадочные места перегенерированы по схеме нового воздушного судна.";

        IsChangingAircraft = false;
        FlightForAircraftChange = null;
        SelectedNewAircraft = null;

        RefreshData();
        StatusMessage = successMessage;
    }

    /// <summary>
    /// Формирует список бортов для замены, исключая текущее воздушное судно рейса.
    /// </summary>
    /// <param name="currentAircraftId">Идентификатор текущего борта рейса.</param>
    private void ConfigureReplacementAircraftList(int currentAircraftId)
    {
        List<AircraftOptionViewModel> options = AvailableAircraftList
            .Where(a => a.Id != currentAircraftId)
            .ToList();

        int selectedId = SelectedNewAircraft?.Id ?? 0;
        ReplacementAircraftList = new ObservableCollection<AircraftOptionViewModel>(options);
        SelectedNewAircraft = options.FirstOrDefault(a => a.Id == selectedId) ?? options.FirstOrDefault();
    }

    #endregion

    #region Отмена рейса с массовым возвратом (TC-08)

    /// <summary>
    /// Отменяет рейс с запуском массового 100% возврата через <c>IRefundService</c>.
    /// </summary>
    /// <param name="row">Отменяемая строка расписания.</param>
    [RelayCommand]
    public void CancelFlight(FlightAdminRowViewModel? row)
    {
        if (!EnsureManagePermission("отмена рейса"))
        {
            return;
        }

        if (row is null)
        {
            return;
        }

        if (row.Status == FlightStatus.Cancelled)
        {
            StatusMessage = $"Рейс {row.FlightNumber} уже отменён.";
            return;
        }

        if (row.Status == FlightStatus.Departed)
        {
            StatusMessage = $"Рейс {row.FlightNumber} уже вылетел — отмена невозможна.";
            return;
        }

        string warning =
            "ВНИМАНИЕ! Отмена рейса выполнит автоматический 100% вынужденный возврат средств " +
            "по всем оплаченным билетам и аннулирует брони (кейс TC-08).\n\n" +
            $"Рейс: {row.FlightNumber} ({row.RouteText}), вылет {row.DepartureText}.\n" +
            $"Занято мест: {row.OccupiedSeats} из {row.TotalSeats}.\n\n" +
            "Продолжить отмену рейса и массовый возврат?";

        MessageBoxResult confirmation = MessageBox.Show(
            warning,
            "Отмена рейса и массовый возврат",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = null;

        Result<MassRefundResultDto> massResult = _refundService.ProcessFlightCancellationRefund(row.Id);
        IsLoading = false;

        if (massResult.IsFailure)
        {
            StatusMessage = $"Ошибка при отмене рейса: {massResult.ErrorMessage}";
            return;
        }

        MassRefundResultDto info = massResult.Value;
        string successMessage =
            $"Рейс {row.FlightNumber} отменён. Возвращено билетов: {info.RefundedTicketsCount}, " +
            $"сумма 100% выплат: {info.TotalRefundAmount:N0} ₽. Все места заблокированы.";

        RefreshData();
        StatusMessage = successMessage;

        MessageBox.Show(
            $"{successMessage}\n\nВиртуальное время операции: {_clockService.CurrentVirtualTime:dd.MM.yyyy HH:mm}.",
            "Отмена рейса выполнена",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Виртуальное время и служебные методы

    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        RunOnUiThread(() => UpdateVirtualNowText(e.NewVirtualTime));
    }

    private void OnFlightStatusChanged(object? sender, FlightStatusChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            try
            {
                LoadFlights();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Не удалось обновить статусы рейсов: {ex.Message}";
            }
        });
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
    }

    private void UpdateVirtualNowText(DateTime virtualNow)
    {
        VirtualNowText = $"Виртуальное время: {virtualNow:dd.MM.yyyy HH:mm}";
    }

    private bool EnsureManagePermission(string actionDescription)
    {
        if (CanManageFlights)
        {
            return true;
        }

        StatusMessage = $"Недостаточно прав: {actionDescription} доступно только администратору.";
        return false;
    }

    private static bool TryParseTimeOfDay(string? text, out TimeSpan time)
    {
        string value = (text ?? string.Empty).Trim();
        time = TimeSpan.Zero;

        if (value.Length == 0)
        {
            return false;
        }

        bool parsed = TimeSpan.TryParseExact(value, @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan exact)
            || TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out exact)
            || TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out exact)
            || TimeSpan.TryParse(value, CultureInfo.CurrentCulture, out exact);

        if (!parsed || exact < TimeSpan.Zero || exact >= TimeSpan.FromDays(1))
        {
            return false;
        }

        time = exact;
        return true;
    }

    /// <summary>
    /// Отписывается от событий виртуального времени и статусов рейсов.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;
        _clockService.FlightStatusChanged -= OnFlightStatusChanged;
        GC.SuppressFinalize(this);
    }

    #endregion
}
