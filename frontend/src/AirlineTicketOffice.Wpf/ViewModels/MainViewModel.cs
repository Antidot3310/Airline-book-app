using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Booking;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Главная модель представления приложения W-02.
/// Обеспечивает навигацию по разделам, отображение строки состояния (StatusBar)
/// и синхронизацию с сервисом виртуального времени (M12).
/// </summary>
public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ISessionService _sessionService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;

    private FlightSearchViewModel? _flightSearchViewModel;
    private MyTicketsViewModel? _myTicketsViewModel;
    private CashierDeskViewModel? _cashierDeskViewModel;

    [ObservableProperty]
    private string _currentTimeDisplay = string.Empty;

    [ObservableProperty]
    private string _clockModeDisplay = string.Empty;

    [ObservableProperty]
    private string _currentRoleName = string.Empty;

    [ObservableProperty]
    private string _currentRoleBadge = string.Empty;

    [ObservableProperty]
    private string _kernelStatus = "Ядро: Готово";

    [ObservableProperty]
    private object? _currentView;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="sessionService">Сервис управления сессиями.</param>
    /// <param name="clockService">Сервис виртуального времени.</param>
    /// <param name="navigationService">Сервис навигации.</param>
    /// <param name="serviceProvider">Провайдер служб для разрешения представлений.</param>
    public MainViewModel(
        ISessionService sessionService,
        IVirtualClockService clockService,
        INavigationService navigationService,
        IServiceProvider serviceProvider)
    {
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // Инициализируем отображение текущего времени и режима
        UpdateClockDisplay(_clockService.CurrentVirtualTime, _clockService.Mode);

        // Инициализируем данные роли
        UpdateRoleDisplay();

        // Подписываемся на события виртуального времени
        _clockService.TimeChanged += OnVirtualTimeChanged;

        // Открываем стартовый раздел по умолчанию в зависимости от роли
        InitializeDefaultSection();
    }

    /// <summary>
    /// Признак доступа к разделу «Поиск и бронирование» (W-03).
    /// </summary>
    public bool CanAccessFlightSearch => _sessionService.HasPermission(SessionPermissions.CreateBooking);

    /// <summary>
    /// Признак доступа к разделу «Мои билеты» (W-07).
    /// </summary>
    public bool CanAccessMyTickets => _sessionService.CurrentRole == UserRole.Passenger ||
                                      _sessionService.HasPermission(SessionPermissions.CreateBooking);

    /// <summary>
    /// Признак доступа к разделу «Рабочее место кассира» (W-08).
    /// </summary>
    public bool CanAccessCashierDesk => _sessionService.CurrentRole == UserRole.Cashier ||
                                        _sessionService.HasPermission(SessionPermissions.AcceptCashPayment);

    /// <summary>
    /// Признак доступа к разделу «Возврат билета» (W-09).
    /// </summary>
    public bool CanAccessRefunds => _sessionService.HasPermission(SessionPermissions.RefundTicket);

    /// <summary>
    /// Признак доступа к разделу «Справочники» (W-11).
    /// </summary>
    public bool CanAccessDirectories => _sessionService.HasPermission(SessionPermissions.ManageDirectories);

    /// <summary>
    /// Признак доступа к разделу «Рейсы» (W-12).
    /// </summary>
    public bool CanAccessFlightsAdmin => _sessionService.HasPermission(SessionPermissions.ManageFlights);

    /// <summary>
    /// Признак доступа к разделу «Тарифы» (W-13).
    /// </summary>
    public bool CanAccessTariffsAdmin => _sessionService.HasPermission(SessionPermissions.ManageTariffs);

    /// <summary>
    /// Признак доступа к разделу «Отчёты» (W-14).
    /// </summary>
    public bool CanAccessReports => _sessionService.HasPermission(SessionPermissions.ViewReports);

    /// <summary>
    /// Признак доступа к разделу «Журнал аудита» (W-15).
    /// </summary>
    public bool CanAccessAuditLog => _sessionService.HasPermission(SessionPermissions.ViewAuditLogs);

    /// <summary>
    /// Обработчик события изменения виртуального времени из ядра.
    /// </summary>
    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdateClockDisplay(e.NewVirtualTime, _clockService.Mode);
            });
        }
        else
        {
            UpdateClockDisplay(e.NewVirtualTime, _clockService.Mode);
        }
    }

    /// <summary>
    /// Обновляет отображение виртуального времени и режима.
    /// </summary>
    private void UpdateClockDisplay(DateTime time, ClockMode mode)
    {
        CurrentTimeDisplay = $"Виртуальное время: {time:dd.MM.yyyy HH:mm:ss}";

        ClockModeDisplay = mode switch
        {
            ClockMode.Manual => "Ручной режим",
            ClockMode.RealTime => "Реальное время",
            ClockMode.Accelerated => "Ускоренное время",
            _ => "Не определён"
        };
    }

    /// <summary>
    /// Обновляет текстовое описание текущей роли и бейдж.
    /// </summary>
    private void UpdateRoleDisplay()
    {
        CurrentRoleName = _sessionService.CurrentRole switch
        {
            UserRole.Passenger => "Пассажир",
            UserRole.Cashier => "Кассир",
            UserRole.Administrator => "Администратор",
            UserRole.System => "Системный процесс",
            _ => "Гость"
        };

        CurrentRoleBadge = $"Текущая роль: {CurrentRoleName}";
    }

    /// <summary>
    /// Устанавливает начальный рабочий раздел в зависимости от активной роли.
    /// </summary>
    private void InitializeDefaultSection()
    {
        switch (_sessionService.CurrentRole)
        {
            case UserRole.Passenger:
                NavigateToFlightSearch();
                break;
            case UserRole.Cashier:
                NavigateToCashierDesk();
                break;
            case UserRole.Administrator:
                NavigateToFlightsAdmin();
                break;
            default:
                NavigateToFlightSearch();
                break;
        }
    }

    /// <summary>
    /// Команда смены роли: закрывает сеанс и возвращает к окну W-01.
    /// </summary>
    [RelayCommand]
    public void SwitchRole()
    {
        _sessionService.CloseSession();
        _navigationService.OpenRoleSelectionWindow();
    }

    /// <summary>
    /// Команда открытия служебного окна эмулятора W-16.
    /// </summary>
    [RelayCommand]
    public void OpenEmulator()
    {
        _navigationService.OpenEmulatorWindow();
    }

    /// <summary>
    /// Переход к разделу «Поиск и бронирование» (W-03).
    /// </summary>
    [RelayCommand]
    public void NavigateToFlightSearch()
    {
        if (_flightSearchViewModel == null)
        {
            _flightSearchViewModel = _serviceProvider.GetRequiredService<FlightSearchViewModel>();
            _flightSearchViewModel.FlightSelected += OnFlightSelected;
        }

        CurrentView = _flightSearchViewModel;
    }

    /// <summary>
    /// Обработчик выбора рейса: переходит к интерактивной схеме мест W-04.
    /// </summary>
    private void OnFlightSelected(FlightListItemDto flight, int passengersCount)
    {
        var seatMapVm = _serviceProvider.GetRequiredService<SeatMapViewModel>();
        seatMapVm.Initialize(flight, passengersCount);
        seatMapVm.BackRequested += () => CurrentView = _flightSearchViewModel;
        seatMapVm.SeatsHeld += (holdResult) => OnSeatsHeld(seatMapVm, holdResult);
        CurrentView = seatMapVm;
    }

    /// <summary>
    /// Обработчик успешного удержания мест: переходит к вводу данных пассажиров W-05.
    /// </summary>
    private void OnSeatsHeld(SeatMapViewModel seatMapVm, HoldSeatsResultDto holdResult)
    {
        var passengerFormVm = _serviceProvider.GetRequiredService<PassengerFormViewModel>();
        passengerFormVm.Initialize(
            seatMapVm.FlightId,
            seatMapVm.FlightNumber,
            seatMapVm.RouteInfo,
            seatMapVm.DepartureTimeText,
            seatMapVm.SelectedSeats.ToList());

        passengerFormVm.BackRequested += () => CurrentView = seatMapVm;
        passengerFormVm.OrderCreated += OnOrderCreated;
        CurrentView = passengerFormVm;
    }

    /// <summary>
    /// Обработчик успешного создания заказа: переходит к экрану оплаты W-06.
    /// </summary>
    private void OnOrderCreated(OrderDto order)
    {
        var paymentVm = _serviceProvider.GetRequiredService<PaymentViewModel>();
        paymentVm.Initialize(order);
        paymentVm.NavigateToSearchRequested += NavigateToFlightSearch;
        paymentVm.PrintDocumentsRequested += OnPrintDocumentsRequested;
        CurrentView = paymentVm;
    }

    /// <summary>
    /// Обработчик перехода к печати документов W-10.
    /// </summary>
    private void OnPrintDocumentsRequested(int orderId)
    {
        var documentService = _serviceProvider.GetRequiredService<IDocumentService>();
        try
        {
            PrintDocumentDto doc = documentService.GeneratePaymentReceiptForOrder(orderId);
            _navigationService.OpenDocumentPreview(doc);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сформировать чек оплаты: {ex.Message}", "Печать чека", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Переход к разделу «Мои билеты» (W-07).
    /// </summary>
    [RelayCommand]
    public void NavigateToMyTickets()
    {
        if (_myTicketsViewModel == null)
        {
            _myTicketsViewModel = _serviceProvider.GetRequiredService<MyTicketsViewModel>();
            _myTicketsViewModel.OrderPayRequested += OnOrderCreated;
            _myTicketsViewModel.NavigateToSearchRequested += NavigateToFlightSearch;
            _myTicketsViewModel.RefundRequested += OnRefundRequested;
        }
        else
        {
            _myTicketsViewModel.LoadSessionTickets();
        }

        CurrentView = _myTicketsViewModel;
    }

    /// <summary>
    /// Обработчик запроса возврата билета: окно W-09 открывается сервисом навигации.
    /// </summary>
    private void OnRefundRequested(TicketItemViewModel ticket)
    {
        // Окно возврата открывается модально через NavigationService
        _cashierDeskViewModel?.RefreshShiftSummary();
    }

    /// <summary>
    /// Переход к разделу «Рабочее место кассира» (W-08).
    /// </summary>
    [RelayCommand]
    public void NavigateToCashierDesk()
    {
        if (_cashierDeskViewModel == null)
        {
            _cashierDeskViewModel = _serviceProvider.GetRequiredService<CashierDeskViewModel>();
            _cashierDeskViewModel.NewSaleRequested += (client) => NavigateToFlightSearch();
        }
        else
        {
            _cashierDeskViewModel.RefreshShiftSummary();
        }

        CurrentView = _cashierDeskViewModel;
    }

    /// <summary>
    /// Переход к разделу «Возврат билета» (W-09).
    /// Открывает рабочее место кассира с фокусом на блоке возврата.
    /// </summary>
    [RelayCommand]
    public void NavigateToRefunds()
    {
        NavigateToCashierDesk();
    }

    /// <summary>
    /// Переход к разделу «Справочники» (W-11).
    /// </summary>
    [RelayCommand]
    public void NavigateToDirectories()
    {
        CurrentView = new PlaceholderViewModel
        {
            Title = "Справочники авиакомпании",
            WindowCode = "W-11",
            PlannedIncrement = "Инкремент 39",
            Icon = "📚",
            Description = "Управление справочниками аэропортов вылета/прилёта, моделями воздушных судов и реестром самолётов авиакомпании."
        };
    }

    /// <summary>
    /// Переход к разделу «Управление рейсами» (W-12).
    /// </summary>
    [RelayCommand]
    public void NavigateToFlightsAdmin()
    {
        CurrentView = new PlaceholderViewModel
        {
            Title = "Управление рейсами и расписанием",
            WindowCode = "W-12",
            PlannedIncrement = "Инкремент 39",
            Icon = "🗓",
            Description = "Создание новых регулярных рейсов, назначение бортов, отслеживание статусов вылета и отмена рейсов с автоматическим массовым возвратом 100%."
        };
    }

    /// <summary>
    /// Переход к разделу «Тарифы» (W-13).
    /// </summary>
    [RelayCommand]
    public void NavigateToTariffsAdmin()
    {
        CurrentView = new PlaceholderViewModel
        {
            Title = "Управление тарифами и сборами",
            WindowCode = "W-13",
            PlannedIncrement = "Инкремент 39",
            Icon = "🏷",
            Description = "Настройка базовых тарифов направлений, коэффициентов бизнес-класса, сборов за багаж и интерактивный калькулятор проверки цен."
        };
    }

    /// <summary>
    /// Переход к разделу «Аналитические отчёты» (W-14).
    /// </summary>
    [RelayCommand]
    public void NavigateToReports()
    {
        CurrentView = new PlaceholderViewModel
        {
            Title = "Аналитические отчёты",
            WindowCode = "W-14",
            PlannedIncrement = "Инкремент 40",
            Icon = "📊",
            Description = "Формирование и выгрузка аналитики: отчёты по загрузке рейсов, финансовой выручке, популярности направлений и статистике возвратов билетов."
        };
    }

    /// <summary>
    /// Переход к разделу «Журнал аудита» (W-15).
    /// </summary>
    [RelayCommand]
    public void NavigateToAuditLog()
    {
        CurrentView = new PlaceholderViewModel
        {
            Title = "Журнал аудита операций",
            WindowCode = "W-15",
            PlannedIncrement = "Инкремент 40",
            Icon = "🛡",
            Description = "Просмотр хронологического журнала системных действий: создание броней, проведение платежей, возвраты, отмены рейсов и входы пользователей."
        };
    }

    /// <summary>
    /// Освобождает ресурсы и отписывается от событий виртуального времени.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;

        if (_flightSearchViewModel != null)
        {
            _flightSearchViewModel.FlightSelected -= OnFlightSelected;
        }

        if (_myTicketsViewModel != null)
        {
            _myTicketsViewModel.OrderPayRequested -= OnOrderCreated;
            _myTicketsViewModel.NavigateToSearchRequested -= NavigateToFlightSearch;
            _myTicketsViewModel.RefundRequested -= OnRefundRequested;
        }

        if (_cashierDeskViewModel != null)
        {
            _cashierDeskViewModel.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
