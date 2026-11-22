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
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления служебного пульта управления эмулятором W-16.
/// Обеспечивает просмотр и перемотку виртуального времени (M12), настройку режимов
/// платёжного шлюза и журнала запросов (M14), ручное подтверждение зависших платежей (TC-15)
/// и запуск регламентных задач ядра. Пульт доступен независимо от роли пользователя,
/// но отображает текущую сессию и работает только через контракты <c>I...Service</c>.
/// </summary>
public partial class EmulatorViewModel : ObservableObject, IDisposable
{
    private const int MaxDelayMilliseconds = 5000;
    private const double MaxShiftMinutes = 5_256_000d;

    private readonly IVirtualClockService _clockService;
    private readonly IPaymentEmulator _paymentEmulator;
    private readonly IFlightService _flightService;
    private readonly ISessionService _sessionService;

    private List<EmulatorRequestRowViewModel> _allRequests = [];
    private int _schedulerEventsReceived;

    #region Текущая сессия

    /// <summary>
    /// Русскоязычная подпись роли текущей активной сессии.
    /// </summary>
    public string SessionRoleText => AuditLogRowViewModel.GetRoleText(_sessionService.CurrentRole);

    /// <summary>
    /// Пояснение о доступности служебного пульта независимо от роли (W-16).
    /// </summary>
    public string SessionHint =>
        $"Текущая роль: {SessionRoleText}. Служебный пульт доступен независимо от роли (W-16).";

    #endregion

    #region Блок 1: виртуальное время (M12)

    /// <summary>
    /// Текущее виртуальное время в формате «ЧЧ:мм:сс» для крупного отображения.
    /// </summary>
    [ObservableProperty]
    private string _currentVirtualTimeText = string.Empty;

    /// <summary>
    /// Текущая виртуальная дата в формате «дд.ММ.гггг».
    /// </summary>
    [ObservableProperty]
    private string _currentVirtualDateText = string.Empty;

    /// <summary>
    /// Русскоязычная подпись текущего режима виртуальных часов.
    /// </summary>
    [ObservableProperty]
    private string _currentClockModeText = string.Empty;

    /// <summary>
    /// Подпись множителя скорости «×12».
    /// </summary>
    [ObservableProperty]
    private string _clockSpeedText = "×1";

    /// <summary>
    /// Описание последнего изменения виртуального времени.
    /// </summary>
    [ObservableProperty]
    private string _lastTimeChangeText = string.Empty;

    /// <summary>
    /// Доступные режимы виртуальных часов согласно контракту <see cref="ClockMode"/>.
    /// </summary>
    public ObservableCollection<EmulatorClockModeOption> ClockModes { get; }

    /// <summary>
    /// Выбранный режим виртуальных часов для применения через <c>SetMode</c>.
    /// </summary>
    [ObservableProperty]
    private EmulatorClockModeOption? _selectedClockMode;

    /// <summary>
    /// Множитель скорости для ускоренного режима (1..60).
    /// </summary>
    [ObservableProperty]
    private double _clockSpeedMultiplier = 1.0;

    /// <summary>
    /// Признак доступности ползунка множителя скорости (только для ускоренного режима).
    /// </summary>
    public bool IsAcceleratedMode => SelectedClockMode?.Mode == ClockMode.Accelerated;

    /// <summary>
    /// Пояснение к выбранному режиму виртуальных часов.
    /// </summary>
    public string ClockModeHint => SelectedClockMode?.Description ?? string.Empty;

    /// <summary>
    /// Подпись цели перемотки «за 2 часа до вылета» для выбранного рейса.
    /// </summary>
    public string JumpTargetHint => SelectedFlight?.TargetTimeHint ?? string.Empty;

    partial void OnSelectedClockModeChanged(EmulatorClockModeOption? value)
    {
        OnPropertyChanged(nameof(IsAcceleratedMode));
        OnPropertyChanged(nameof(ClockModeHint));
    }

    partial void OnClockSpeedMultiplierChanged(double value)
    {
        ClockSpeedText = $"×{value:F0}";
    }

    /// <summary>
    /// Введённое количество единиц для произвольного шага перемотки.
    /// </summary>
    [ObservableProperty]
    private string _customShiftAmount = "30";

    /// <summary>
    /// Индекс выбранной единицы измерения шага (0 — минуты, 1 — часы, 2 — дни).
    /// </summary>
    [ObservableProperty]
    private int _selectedShiftUnitIndex;

    /// <summary>
    /// Доступные единицы измерения шага перемотки с русскоязычными подписями.
    /// </summary>
    public ObservableCollection<string> ShiftUnits { get; } = ["минуты", "часы", "дни"];

    /// <summary>
    /// Запланированные рейсы, до вылета которых остаётся более двух часов.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<EmulatorFlightOption> _upcomingFlights = [];

    /// <summary>
    /// Выбранный рейс для перехода к моменту «за 2 часа до вылета».
    /// </summary>
    [ObservableProperty]
    private EmulatorFlightOption? _selectedFlight;

    /// <summary>
    /// Признак наличия рейсов, доступных для перемотки.
    /// </summary>
    public bool HasUpcomingFlights => UpcomingFlights.Count > 0;

    /// <summary>
    /// Подпись списка рейсов для перемотки.
    /// </summary>
    public string FlightsHint => HasUpcomingFlights
        ? "Выберите рейс — виртуальное время будет установлено за 2 часа до его вылета."
        : "Нет запланированных рейсов позже чем через 2 часа от текущего виртуального времени.";

    partial void OnUpcomingFlightsChanged(ObservableCollection<EmulatorFlightOption> value)
    {
        OnPropertyChanged(nameof(HasUpcomingFlights));
        OnPropertyChanged(nameof(FlightsHint));
    }

    partial void OnSelectedFlightChanged(EmulatorFlightOption? value)
    {
        OnPropertyChanged(nameof(JumpTargetHint));
    }

    #endregion

    #region Блок 2: платёжный шлюз (M14)

    /// <summary>
    /// Искусственная задержка ответа платёжного шлюза в миллисекундах (0..5000).
    /// </summary>
    [ObservableProperty]
    private int _delayMilliseconds;

    /// <summary>
    /// Журнал входящих запросов к платёжному шлюзу с учётом фильтра «только зависшие».
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<EmulatorRequestRowViewModel> _incomingRequests = [];

    /// <summary>
    /// Признак отображения только платежей со статусом Indeterminate (TC-15).
    /// </summary>
    [ObservableProperty]
    private bool _showOnlyIndeterminate;

    /// <summary>
    /// Количество запросов, отображаемых в журнале.
    /// </summary>
    [ObservableProperty]
    private int _requestsCount;

    /// <summary>
    /// Количество зависших запросов со статусом Indeterminate.
    /// </summary>
    [ObservableProperty]
    private int _pendingRequestsCount;

    /// <summary>
    /// Признак наличия записей в журнале запросов.
    /// </summary>
    public bool HasRequests => IncomingRequests.Count > 0;

    /// <summary>
    /// Признак отсутствия записей в журнале запросов.
    /// </summary>
    public bool HasNoRequests => !HasRequests;

    /// <summary>
    /// Признак наличия зависших платежей, требующих ручного подтверждения.
    /// </summary>
    public bool HasPendingRequests => PendingRequestsCount > 0;

    /// <summary>
    /// Сводка по журналу запросов.
    /// </summary>
    public string RequestsSummaryText =>
        $"Запросов: {RequestsCount} • требуют подтверждения: {PendingRequestsCount}";

    /// <summary>
    /// Признак выбранного режима «Всегда успех».
    /// </summary>
    public bool IsModeAlwaysSuccess => _paymentEmulator.Mode == PaymentEmulatorMode.AlwaysSuccess;

    /// <summary>
    /// Признак выбранного режима «Всегда отказ».
    /// </summary>
    public bool IsModeAlwaysReject => _paymentEmulator.Mode == PaymentEmulatorMode.AlwaysReject;

    /// <summary>
    /// Признак выбранного режима «Имитация таймаута».
    /// </summary>
    public bool IsModeTimeout => _paymentEmulator.Mode == PaymentEmulatorMode.Timeout;

    /// <summary>
    /// Признак выбранного режима «Случайный ответ».
    /// </summary>
    public bool IsModeRandom => _paymentEmulator.Mode == PaymentEmulatorMode.Random;

    /// <summary>
    /// Русскоязычная подпись текущего режима платёжного шлюза.
    /// </summary>
    public string PaymentModeTitle => _paymentEmulator.Mode switch
    {
        PaymentEmulatorMode.AlwaysSuccess => "Всегда успех",
        PaymentEmulatorMode.AlwaysReject => "Всегда отказ",
        PaymentEmulatorMode.Timeout => "Имитация таймаута",
        PaymentEmulatorMode.Random => "Случайный ответ",
        _ => _paymentEmulator.Mode.ToString()
    };

    /// <summary>
    /// Пояснение текущего режима платёжного шлюза.
    /// </summary>
    public string PaymentModeHint => _paymentEmulator.Mode switch
    {
        PaymentEmulatorMode.AlwaysSuccess => "Все входящие платежи одобряются платёжным шлюзом.",
        PaymentEmulatorMode.AlwaysReject => "Все входящие платежи отклоняются банком-эмитентом.",
        PaymentEmulatorMode.Timeout => "Платёж зависает в статусе Indeterminate — требуется ручное подтверждение (TC-15).",
        PaymentEmulatorMode.Random => "Случайный исход: с вероятностью 50 % успех или отказ.",
        _ => string.Empty
    };

    partial void OnIncomingRequestsChanged(ObservableCollection<EmulatorRequestRowViewModel> value)
    {
        OnPropertyChanged(nameof(HasRequests));
        OnPropertyChanged(nameof(HasNoRequests));
    }

    partial void OnRequestsCountChanged(int value)
    {
        OnPropertyChanged(nameof(RequestsSummaryText));
    }

    partial void OnPendingRequestsCountChanged(int value)
    {
        OnPropertyChanged(nameof(RequestsSummaryText));
        OnPropertyChanged(nameof(HasPendingRequests));
    }

    partial void OnShowOnlyIndeterminateChanged(bool value)
    {
        RebuildRequests();
    }

    partial void OnDelayMillisecondsChanged(int value)
    {
        int clamped = Math.Clamp(value, 0, MaxDelayMilliseconds);

        if (clamped != value)
        {
            DelayMilliseconds = clamped;
            return;
        }

        _paymentEmulator.DelayMilliseconds = clamped;
    }

    #endregion

    #region Блок 3: ручное подтверждение зависших платежей (TC-15)

    /// <summary>
    /// Идентификатор платежа, введённый оператором для ручного подтверждения.
    /// </summary>
    [ObservableProperty]
    private string _pendingPaymentId = string.Empty;

    /// <summary>
    /// Сообщение о результате ручного подтверждения или отказа платежа.
    /// </summary>
    [ObservableProperty]
    private string? _resolveMessage;

    /// <summary>
    /// Признак успешного результата последней операции ручного подтверждения.
    /// </summary>
    [ObservableProperty]
    private bool _isResolveSucceeded;

    /// <summary>
    /// Пояснение к полю идентификатора платежа.
    /// </summary>
    public string PendingPaymentHint =>
        "Введите ID платежа из результата оплаты или квитанции (W-10). " +
        "Ручное подтверждение доступно только для платежей со статусом Indeterminate.";

    #endregion

    #region Состояние, события планировщика и закрытие окна

    /// <summary>
    /// Информационное сообщение пульта или текст ошибки.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Описание последнего события регламентных задач ядра.
    /// </summary>
    [ObservableProperty]
    private string? _lastSchedulerEventText;

    /// <summary>
    /// Событие запроса закрытия окна эмулятора.
    /// </summary>
    public event Action? CloseRequested;

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="EmulatorViewModel"/>.
    /// </summary>
    /// <param name="clockService">Сервис виртуального времени и планировщика M12.</param>
    /// <param name="paymentEmulator">Эмулятор платёжного шлюза M14.</param>
    /// <param name="flightService">Сервис расписания рейсов M4 для перемотки к вылету.</param>
    /// <param name="sessionService">Сервис текущей сессии M1 (информационное отображение роли).</param>
    public EmulatorViewModel(
        IVirtualClockService clockService,
        IPaymentEmulator paymentEmulator,
        IFlightService flightService,
        ISessionService sessionService)
    {
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _paymentEmulator = paymentEmulator ?? throw new ArgumentNullException(nameof(paymentEmulator));
        _flightService = flightService ?? throw new ArgumentNullException(nameof(flightService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        ClockModes =
        [
            new EmulatorClockModeOption(
                ClockMode.RealTime,
                "Реальное время",
                "Виртуальное время соответствует системным часам хоста; перемотка выполняется вручную."),
            new EmulatorClockModeOption(
                ClockMode.Accelerated,
                "Ускоренный режим",
                "Ядро сохраняет множитель скорости; шаг ускорения выполняется кнопками перемотки пульта."),
            new EmulatorClockModeOption(
                ClockMode.Manual,
                "Ручной режим",
                "Виртуальное время изменяется только вручную командами пульта.")
        ];

        SelectedClockMode = ClockModes.FirstOrDefault(option => option.Mode == _clockService.Mode)
            ?? ClockModes[2];
        ClockSpeedMultiplier = _clockService.SpeedMultiplier <= 0d
            ? 1d
            : Math.Round(_clockService.SpeedMultiplier, 0);
        DelayMilliseconds = Math.Clamp(_paymentEmulator.DelayMilliseconds, 0, MaxDelayMilliseconds);

        UpdateClockDisplay(_clockService.CurrentVirtualTime, _clockService.Mode);
        NotifyPaymentModeChanged();

        _clockService.TimeChanged += OnVirtualTimeChanged;
        _clockService.HoldsExpired += OnHoldsExpired;
        _clockService.FlightStatusChanged += OnFlightStatusChanged;

        RefreshRequests();
        RefreshFlights();
    }

    #region Команды управления временем (M12)

    /// <summary>
    /// Применяет выбранный режим виртуальных часов и множитель скорости через <c>SetMode</c>.
    /// </summary>
    [RelayCommand]
    public void ApplyClockMode()
    {
        if (SelectedClockMode is null)
        {
            StatusMessage = "Выберите режим виртуальных часов.";
            return;
        }

        double speed = SelectedClockMode.Mode == ClockMode.Accelerated
            ? Math.Clamp(Math.Round(ClockSpeedMultiplier, 0), 1d, 60d)
            : 1d;

        _clockService.SetMode(SelectedClockMode.Mode, speed);
        ClockSpeedMultiplier = speed;
        UpdateClockDisplay(_clockService.CurrentVirtualTime, _clockService.Mode);

        StatusMessage = _clockService.Mode == ClockMode.Accelerated
            ? $"Режим часов: «{SelectedClockMode.Title}», множитель ×{speed:F0}. " +
              "Течение ускоренного времени имитируется кнопками перемотки пульта."
            : $"Режим часов: «{SelectedClockMode.Title}».";
    }

    /// <summary>
    /// Смещает виртуальное время вперёд на 15 минут (срок истечения брони места).
    /// </summary>
    [RelayCommand]
    public void Advance15Minutes()
    {
        ShiftTime(TimeSpan.FromMinutes(15), "Сдвиг виртуального времени на +15 минут");
    }

    /// <summary>
    /// Смещает виртуальное время вперёд на 1 час.
    /// </summary>
    [RelayCommand]
    public void Advance1Hour()
    {
        ShiftTime(TimeSpan.FromHours(1), "Сдвиг виртуального времени на +1 час");
    }

    /// <summary>
    /// Смещает виртуальное время вперёд на 1 сутки.
    /// </summary>
    [RelayCommand]
    public void Advance1Day()
    {
        ShiftTime(TimeSpan.FromDays(1), "Сдвиг виртуального времени на +1 день");
    }

    /// <summary>
    /// Смещает виртуальное время на произвольный положительный шаг,
    /// заданный в минутах, часах или днях.
    /// </summary>
    [RelayCommand]
    public void AdvanceCustom()
    {
        if (!TryParseShiftAmount(CustomShiftAmount, out double amount))
        {
            StatusMessage = "Введите положительное число для шага перемотки (например, 30 или 1,5).";
            return;
        }

        double minutes = amount * (SelectedShiftUnitIndex switch
        {
            1 => 60d,
            2 => 1440d,
            _ => 1d
        });

        if (minutes < 1d)
        {
            StatusMessage = "Минимальный шаг перемотки — 1 минута.";
            return;
        }

        if (minutes > MaxShiftMinutes)
        {
            StatusMessage = "Максимальный шаг перемотки — 10 лет.";
            return;
        }

        string unitText = SelectedShiftUnitIndex switch
        {
            1 => "ч",
            2 => "дн.",
            _ => "мин."
        };

        ShiftTime(
            TimeSpan.FromMinutes(minutes),
            $"Сдвиг виртуального времени на +{FormatShiftAmount(amount)} {unitText}");
    }

    private void ShiftTime(TimeSpan delta, string actionText)
    {
        if (delta <= TimeSpan.Zero)
        {
            StatusMessage = "Шаг перемотки должен быть положительным.";
            return;
        }

        _schedulerEventsReceived = 0;
        _clockService.AdvanceTime(delta);

        string nowText = _clockService.CurrentVirtualTime.ToString("dd.MM.yyyy HH:mm");
        StatusMessage = _schedulerEventsReceived > 0 && !string.IsNullOrWhiteSpace(LastSchedulerEventText)
            ? $"{actionText}. Виртуальное время: {nowText}. {LastSchedulerEventText}"
            : $"{actionText}. Виртуальное время: {nowText}.";
    }

    /// <summary>
    /// Перематывает виртуальное время к моменту «за 2 часа до вылета» выбранного рейса.
    /// </summary>
    [RelayCommand]
    public void JumpToTwoHoursBeforeFlight()
    {
        EmulatorFlightOption? flight = SelectedFlight;

        if (flight is null)
        {
            StatusMessage = "Выберите рейс из списка запланированных вылетов.";
            return;
        }

        DateTime now = _clockService.CurrentVirtualTime;
        DateTime target = flight.TargetTime;

        if (target <= now)
        {
            StatusMessage =
                $"Момент {target:dd.MM.yyyy HH:mm} (за 2 часа до вылета {flight.FlightNumber}) " +
                "уже не впереди текущего виртуального времени.";
            return;
        }

        ShiftTime(
            target - now,
            $"Перемотка за 2 часа до вылета рейса {flight.FlightNumber} ({flight.RouteText})");
    }

    /// <summary>
    /// Сбрасывает виртуальное время к текущему системному времени хоста
    /// и переводит часы в режим <see cref="ClockMode.RealTime"/>.
    /// </summary>
    [RelayCommand]
    public void ResetTime()
    {
        _schedulerEventsReceived = 0;
        _clockService.ResetToRealTime();
        StatusMessage =
            "Виртуальные часы сброшены на текущее системное время хоста и переведены в режим «Реальное время».";
    }

    /// <summary>
    /// Принудительно запускает регламентные фоновые процессы ядра
    /// (снятие просроченных броней и перевод статусов вылетевших рейсов).
    /// </summary>
    [RelayCommand]
    public void TriggerJobs()
    {
        _schedulerEventsReceived = 0;
        _clockService.TriggerScheduledJobs();

        StatusMessage = _schedulerEventsReceived > 0 && !string.IsNullOrWhiteSpace(LastSchedulerEventText)
            ? $"Регламентные задачи выполнены. {LastSchedulerEventText}"
            : "Регламентные задачи выполнены: просроченных броней и вылетевших рейсов не обнаружено.";
    }

    /// <summary>
    /// Перезагружает список запланированных рейсов, до вылета которых остаётся более двух часов.
    /// </summary>
    [RelayCommand]
    public void RefreshFlights()
    {
        try
        {
            DateTime threshold = _clockService.CurrentVirtualTime.AddHours(2);
            int selectedId = SelectedFlight?.Id ?? 0;

            List<EmulatorFlightOption> options = _flightService.GetAllFlights()
                .Where(flight => flight.Status == FlightStatus.Scheduled && flight.DepartureTime > threshold)
                .OrderBy(flight => flight.DepartureTime)
                .Take(100)
                .Select(flight => new EmulatorFlightOption(flight))
                .ToList();

            UpcomingFlights = new ObservableCollection<EmulatorFlightOption>(options);
            SelectedFlight = options.FirstOrDefault(option => option.Id == selectedId)
                ?? options.FirstOrDefault();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось загрузить расписание рейсов: {ex.Message}";
        }
    }

    #endregion

    #region Команды платёжного шлюза (M14)

    /// <summary>
    /// Устанавливает режим ответа эмулятора платёжного шлюза.
    /// </summary>
    /// <param name="modeName">Имя режима <see cref="PaymentEmulatorMode"/> из CommandParameter.</param>
    [RelayCommand]
    public void SelectPaymentMode(object? modeName)
    {
        if (modeName is null)
        {
            return;
        }

        string raw = modeName.ToString() ?? string.Empty;

        if (!Enum.TryParse(raw, ignoreCase: true, out PaymentEmulatorMode mode) || !Enum.IsDefined(mode))
        {
            StatusMessage = "Неизвестный режим платёжного шлюза.";
            return;
        }

        _paymentEmulator.SetResponseMode(mode);
        NotifyPaymentModeChanged();

        StatusMessage = mode == PaymentEmulatorMode.Timeout
            ? "Режим шлюза: «Имитация таймаута». Следующие оплаты картой получат статус Indeterminate " +
              "и потребуют ручного подтверждения (TC-15)."
            : $"Режим шлюза: «{PaymentModeTitle}». {PaymentModeHint}";
    }

    private void NotifyPaymentModeChanged()
    {
        OnPropertyChanged(nameof(IsModeAlwaysSuccess));
        OnPropertyChanged(nameof(IsModeAlwaysReject));
        OnPropertyChanged(nameof(IsModeTimeout));
        OnPropertyChanged(nameof(IsModeRandom));
        OnPropertyChanged(nameof(PaymentModeTitle));
        OnPropertyChanged(nameof(PaymentModeHint));
    }

    /// <summary>
    /// Обновляет журнал входящих платёжных запросов из эмулятора шлюза.
    /// </summary>
    [RelayCommand]
    public void RefreshRequests()
    {
        try
        {
            _allRequests = _paymentEmulator.GetIncomingRequestsLog()
                .Select(entry => new EmulatorRequestRowViewModel(entry))
                .ToList();

            RebuildRequests();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось получить журнал запросов эмулятора: {ex.Message}";
        }
    }

    /// <summary>
    /// Очищает журнал входящих запросов эмулятора платёжного шлюза.
    /// </summary>
    [RelayCommand]
    public void ClearLog()
    {
        _paymentEmulator.ClearLog();
        _allRequests = [];
        RebuildRequests();
        StatusMessage = "Журнал запросов эмулятора очищен.";
    }

    private void RebuildRequests()
    {
        IEnumerable<EmulatorRequestRowViewModel> query = _allRequests;

        if (ShowOnlyIndeterminate)
        {
            query = query.Where(request => request.RequiresManualResolve);
        }

        IncomingRequests = new ObservableCollection<EmulatorRequestRowViewModel>(query);
        RequestsCount = IncomingRequests.Count;
        PendingRequestsCount = _allRequests.Count(request => request.RequiresManualResolve);
    }

    #endregion

    #region Ручной резолв таймаутов (TC-15)

    /// <summary>
    /// Вручную подтверждает зависший платёж как успешный: заказ оплачивается, билеты выпускаются.
    /// </summary>
    [RelayCommand]
    public void ResolveSuccess()
    {
        ResolvePayment(true);
    }

    /// <summary>
    /// Вручную отклоняет зависший платёж.
    /// </summary>
    [RelayCommand]
    public void ResolveReject()
    {
        ResolvePayment(false);
    }

    private void ResolvePayment(bool markSuccess)
    {
        ResolveMessage = null;
        IsResolveSucceeded = false;

        if (!int.TryParse(
                PendingPaymentId.Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int paymentId)
            || paymentId <= 0)
        {
            ResolveMessage = "Введите корректный числовой ID платежа (положительное целое число).";
            return;
        }

        try
        {
            Result result = _paymentEmulator.ManuallyResolvePayment(paymentId, markSuccess);

            if (result.IsFailure)
            {
                ResolveMessage = $"Ошибка ручного подтверждения: {result.ErrorMessage}";
                return;
            }
        }
        catch (Exception ex)
        {
            ResolveMessage = $"Ошибка ручного подтверждения: {ex.Message}";
            return;
        }

        IsResolveSucceeded = true;
        ResolveMessage = markSuccess
            ? $"Платёж #{paymentId} подтверждён вручную: статус Success, заказ оплачен, билеты выпущены."
            : $"Платёж #{paymentId} отклонён оператором: статус Failed, билеты не выпущены.";
        PendingPaymentId = string.Empty;

        RefreshRequests();
        StatusMessage = ResolveMessage;
    }

    #endregion

    #region Виртуальные часы, события ядра и освобождение ресурсов

    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            LastTimeChangeText =
                $"Предыдущее время: {e.OldVirtualTime:dd.MM.yyyy HH:mm:ss} → " +
                $"новое: {e.NewVirtualTime:dd.MM.yyyy HH:mm:ss}.";

            SynchronizeClockModeSelection();
            UpdateClockDisplay(e.NewVirtualTime, _clockService.Mode);
            RefreshFlights();
        });
    }

    private void OnHoldsExpired(object? sender, HoldsExpiredEventArgs e)
    {
        RunOnUiThread(() =>
        {
            _schedulerEventsReceived++;
            LastSchedulerEventText =
                $"Снятие броней: аннулировано заказов {e.ExpiredOrdersCount}, " +
                $"освобождено мест {e.ReleasedSeatsCount}.";
            StatusMessage = LastSchedulerEventText;
        });
    }

    private void OnFlightStatusChanged(object? sender, FlightStatusChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            _schedulerEventsReceived++;
            string statusText = e.NewStatus switch
            {
                FlightStatus.Scheduled => "Запланирован",
                FlightStatus.Departed => "Вылетел",
                FlightStatus.Cancelled => "Отменён",
                _ => e.NewStatus.ToString()
            };

            LastSchedulerEventText = $"Рейс #{e.FlightId} переведён в статус «{statusText}».";
            StatusMessage = LastSchedulerEventText;
            RefreshFlights();
        });
    }

    private void SynchronizeClockModeSelection()
    {
        EmulatorClockModeOption? option = ClockModes
            .FirstOrDefault(candidate => candidate.Mode == _clockService.Mode);

        if (option is not null && !ReferenceEquals(SelectedClockMode, option))
        {
            SelectedClockMode = option;
        }

        double speed = _clockService.SpeedMultiplier;
        if (speed > 0d && Math.Abs(ClockSpeedMultiplier - speed) > 0.001d)
        {
            ClockSpeedMultiplier = speed;
        }
    }

    private void UpdateClockDisplay(DateTime time, ClockMode mode)
    {
        CurrentVirtualTimeText = time.ToString("HH:mm:ss");
        CurrentVirtualDateText = time.ToString("dd.MM.yyyy");
        CurrentClockModeText = mode switch
        {
            ClockMode.RealTime => "Реальное время",
            ClockMode.Accelerated => "Ускоренный режим",
            ClockMode.Manual => "Ручной режим",
            _ => mode.ToString()
        };
        ClockSpeedText = $"×{ClockSpeedMultiplier:F0}";
    }

    private static bool TryParseShiftAmount(string? text, out double amount)
    {
        amount = 0d;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.Trim().Replace(',', '.');
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;

        bool parsed = double.TryParse(normalized, styles, CultureInfo.CurrentCulture, out amount)
            || double.TryParse(normalized, styles, CultureInfo.InvariantCulture, out amount);

        return parsed && double.IsFinite(amount) && amount > 0d;
    }

    private static string FormatShiftAmount(double amount) =>
        amount.ToString("0.##", CultureInfo.CurrentCulture);

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
    }

    /// <summary>
    /// Закрывает окно эмулятора.
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Освобождает ресурсы и отписывается от событий виртуального времени и планировщика.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;
        _clockService.HoldsExpired -= OnHoldsExpired;
        _clockService.FlightStatusChanged -= OnFlightStatusChanged;
        GC.SuppressFinalize(this);
    }

    #endregion
}
