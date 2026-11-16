using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Управление рейсами» W-12.
/// Обеспечивает создание рейсов с проверкой статуса борта (TC-11),
/// замену воздушного судна (TC-09/10) и отмену рейса с массовым 100% возвратом (TC-08).
/// </summary>
public partial class FlightsAdminViewModel : ObservableObject
{
    private readonly IFlightService _flightService;
    private readonly IDirectoryService _directoryService;
    private readonly IRefundService _refundService;
    private readonly IVirtualClockService _clockService;

    #region Фильтрация рейсов

    [ObservableProperty]
    private int _selectedStatusFilter;

    [ObservableProperty]
    private string _filterFlightNumber = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FlightDetailsDto> _allFlights = [];

    [ObservableProperty]
    private ObservableCollection<FlightDetailsDto> _filteredFlights = [];

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
    private string _departureTimeStr = "10:00";

    [ObservableProperty]
    private DateTime _arrivalDate;

    [ObservableProperty]
    private string _arrivalTimeStr = "12:30";

    [ObservableProperty]
    private ObservableCollection<AircraftDto> _availableAircraftList = [];

    [ObservableProperty]
    private AircraftDto? _selectedAircraft;

    #endregion

    #region Замена воздушного судна

    [ObservableProperty]
    private bool _isChangingAircraft;

    [ObservableProperty]
    private FlightDetailsDto? _selectedFlightForChangeAircraft;

    [ObservableProperty]
    private AircraftDto? _selectedNewAircraft;

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
    public FlightsAdminViewModel(
        IFlightService flightService,
        IDirectoryService directoryService,
        IRefundService refundService,
        IVirtualClockService clockService)
    {
        _flightService = flightService ?? throw new ArgumentNullException(nameof(flightService));
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));

        DateTime tomorrow = _clockService.CurrentVirtualTime.Date.AddDays(1);
        DepartureDate = tomorrow;
        ArrivalDate = tomorrow;

        RefreshData();
    }

    /// <summary>
    /// Перезагружает данные расписания и справочников.
    /// </summary>
    [RelayCommand]
    public void RefreshData()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            // 1. Загрузка списка всех рейсов
            IReadOnlyList<FlightDetailsDto> flights = _flightService.GetAllFlights();
            AllFlights = new ObservableCollection<FlightDetailsDto>(flights);
            ApplyFilter();

            // 2. Загрузка активных аэропортов
            IReadOnlyList<AirportDto> airports = _directoryService.GetAllAirports().Where(a => a.IsActive).ToList();
            OriginAirports = new ObservableCollection<AirportDto>(airports);
            DestinationAirports = new ObservableCollection<AirportDto>(airports);

            if (SelectedOriginAirport == null && airports.Count > 0)
            {
                SelectedOriginAirport = airports[0];
            }
            if (SelectedDestinationAirport == null && airports.Count > 1)
            {
                SelectedDestinationAirport = airports[1];
            }

            // 3. Загрузка доступных бортов (в строю)
            IReadOnlyList<AircraftDto> aircrafts = _directoryService.GetAllAircrafts().Where(a => a.IsActive).ToList();
            AvailableAircraftList = new ObservableCollection<AircraftDto>(aircrafts);

            if (SelectedAircraft == null && AvailableAircraftList.Count > 0)
            {
                SelectedAircraft = AvailableAircraftList.FirstOrDefault(a => a.Status == AircraftStatus.InService) ?? AvailableAircraftList[0];
            }
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
    /// Применяет выбранные фильтры по статусу и номеру рейса.
    /// </summary>
    public void ApplyFilter()
    {
        IEnumerable<FlightDetailsDto> query = AllFlights;

        if (SelectedStatusFilter == 1)
        {
            query = query.Where(f => f.Status == FlightStatus.Scheduled);
        }
        else if (SelectedStatusFilter == 2)
        {
            query = query.Where(f => f.Status == FlightStatus.Departed);
        }
        else if (SelectedStatusFilter == 3)
        {
            query = query.Where(f => f.Status == FlightStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(FilterFlightNumber))
        {
            string needle = FilterFlightNumber.Trim().ToUpperInvariant();
            query = query.Where(f => f.FlightNumber.ToUpperInvariant().Contains(needle));
        }

        FilteredFlights = new ObservableCollection<FlightDetailsDto>(query);
    }

    /// <summary>
    /// Переключает фильтр по статусу рейса (0 = Все, 1 = Запланированные, 2 = Вылетел, 3 = Отменён).
    /// </summary>
    /// <param name="filterIndex">Индекс фильтра.</param>
    [RelayCommand]
    public void FilterByStatus(object? filterIndex)
    {
        if (filterIndex != null && int.TryParse(filterIndex.ToString(), out int idx))
        {
            SelectedStatusFilter = idx;
            ApplyFilter();
        }
    }

    partial void OnFilterFlightNumberChanged(string value)
    {
        ApplyFilter();
    }

    #region Создание нового рейса

    /// <summary>
    /// Открывает панель создания нового рейса.
    /// </summary>
    [RelayCommand]
    public void StartCreateFlight()
    {
        IsCreatingFlight = true;
        IsChangingAircraft = false;
        StatusMessage = null;
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
    /// Создаёт новый рейс с автогенерацией посадочных мест и проверкой борта (TC-11).
    /// </summary>
    [RelayCommand]
    public void CreateFlight()
    {
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(NewFlightNumber))
        {
            StatusMessage = "Введите номер рейса (например, SU-105).";
            return;
        }

        if (SelectedOriginAirport == null || SelectedDestinationAirport == null)
        {
            StatusMessage = "Выберите аэропорт вылета и назначения.";
            return;
        }

        if (SelectedOriginAirport.Id == SelectedDestinationAirport.Id)
        {
            StatusMessage = "Аэропорт вылета и назначения не могут совпадать.";
            return;
        }

        if (SelectedAircraft == null)
        {
            StatusMessage = "Выберите воздушное судно для рейса.";
            return;
        }

        if (!TimeSpan.TryParseExact(DepartureTimeStr.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan depTime) &&
            !TimeSpan.TryParse(DepartureTimeStr.Trim(), out depTime))
        {
            StatusMessage = "Некорректный формат времени вылета (ожидается ЧЧ:ММ).";
            return;
        }

        if (!TimeSpan.TryParseExact(ArrivalTimeStr.Trim(), @"h\:mm", CultureInfo.InvariantCulture, out TimeSpan arrTime) &&
            !TimeSpan.TryParse(ArrivalTimeStr.Trim(), out arrTime))
        {
            StatusMessage = "Некорректный формат времени прилёта (ожидается ЧЧ:ММ).";
            return;
        }

        DateTime fullDeparture = DepartureDate.Date + depTime;
        DateTime fullArrival = ArrivalDate.Date + arrTime;

        if (fullArrival <= fullDeparture)
        {
            StatusMessage = "Время прилёта должно быть позже времени вылета.";
            return;
        }

        var request = new CreateFlightRequest
        {
            FlightNumber = NewFlightNumber.Trim().ToUpperInvariant(),
            OriginAirportId = SelectedOriginAirport.Id,
            DestinationAirportId = SelectedDestinationAirport.Id,
            AircraftId = SelectedAircraft.Id,
            DepartureTime = fullDeparture,
            ArrivalTime = fullArrival
        };

        Result<int> result = _flightService.CreateFlight(request);
        if (result.IsFailure)
        {
            // Кейс TC-11 (борт на ТО) и другие ошибки валидации
            StatusMessage = $"Ошибка создания рейса: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Рейс {request.FlightNumber} успешно создан и добавлен в расписание!";
        IsCreatingFlight = false;
        NewFlightNumber = string.Empty;

        RefreshData();
    }

    #endregion

    #region Смена борта (TC-09 / TC-10)

    /// <summary>
    /// Открывает панель замены воздушного судна для выбранного рейса.
    /// </summary>
    /// <param name="flight">Выбранный рейс.</param>
    [RelayCommand]
    public void OpenChangeAircraft(FlightDetailsDto? flight)
    {
        if (flight == null)
        {
            return;
        }

        if (flight.Status != FlightStatus.Scheduled)
        {
            StatusMessage = "Смена борта возможна только для рейсов в статусе «Запланирован».";
            return;
        }

        SelectedFlightForChangeAircraft = flight;
        IsChangingAircraft = true;
        IsCreatingFlight = false;
        SelectedNewAircraft = AvailableAircraftList.FirstOrDefault(a => a.Id != flight.AircraftId && a.Status == AircraftStatus.InService);
        StatusMessage = null;
    }

    /// <summary>
    /// Закрывает панель смены борта.
    /// </summary>
    [RelayCommand]
    public void CancelChangeAircraft()
    {
        IsChangingAircraft = false;
        SelectedFlightForChangeAircraft = null;
        StatusMessage = null;
    }

    /// <summary>
    /// Выполняет замену борта на рейсе с проверкой занятости мест (TC-10).
    /// </summary>
    [RelayCommand]
    public void ConfirmChangeAircraft()
    {
        if (SelectedFlightForChangeAircraft == null || SelectedNewAircraft == null)
        {
            return;
        }

        StatusMessage = null;
        Result result = _flightService.ChangeAircraft(SelectedFlightForChangeAircraft.Id, SelectedNewAircraft.Id);

        if (result.IsFailure)
        {
            // Кейс TC-10 (на рейс уже куплены или забронированы билеты)
            StatusMessage = $"Запрет смены борта: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Борт на рейсе {SelectedFlightForChangeAircraft.FlightNumber} успешно заменён на {SelectedNewAircraft.TailNumber} ({SelectedNewAircraft.AircraftTypeName}).";
        IsChangingAircraft = false;
        SelectedFlightForChangeAircraft = null;

        RefreshData();
    }

    #endregion

    #region Отмена рейса с массовым возвратом (TC-08)

    /// <summary>
    /// Выполняет отмену рейса с запуском массового 100% возврата билетов (TC-08).
    /// </summary>
    /// <param name="flight">Отменяемый рейс.</param>
    [RelayCommand]
    public void CancelFlight(FlightDetailsDto? flight)
    {
        if (flight == null)
        {
            return;
        }

        if (flight.Status == FlightStatus.Cancelled)
        {
            StatusMessage = $"Рейс {flight.FlightNumber} уже отменён.";
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            $"ВНИМАНИЕ! Отмена рейса выполнит автоматический 100% вынужденный возврат средств по всем оплаченным билетам и аннулирует брони (кейс TC-08).\n\n" +
            $"Вы уверены, что хотите отменить рейс {flight.FlightNumber} ({flight.OriginAirportCode} ➔ {flight.DestinationAirportCode})?",
            "Отмена рейса и массовый возврат",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = null;

        Result<MassRefundResultDto> massResult = _refundService.ProcessFlightCancellationRefund(flight.Id);
        IsLoading = false;

        if (massResult.IsFailure)
        {
            StatusMessage = $"Ошибка при отмене рейса: {massResult.ErrorMessage}";
            return;
        }

        MassRefundResultDto info = massResult.Value;
        MessageBox.Show(
            $"Рейс {flight.FlightNumber} успешно отменён!\n\n" +
            $"• Возвращено билетов: {info.RefundedTicketsCount} шт.\n" +
            $"• Сумма 100% выплат клиентам: {info.TotalRefundAmount:N0} ₽.\n\n" +
            "Все места на рейсе заблокированы.",
            "Отмена рейса выполнена",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        StatusMessage = $"Рейс {flight.FlightNumber} отменён. Возвращено билетов: {info.RefundedTicketsCount}, сумма выплат: {info.TotalRefundAmount:N0} ₽.";
        RefreshData();
    }

    #endregion
}
