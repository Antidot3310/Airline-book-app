using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления служебного пульта управления эмулятором W-16.
/// Обеспечивает сдвиг виртуального времени (M12), настройку режимов платёжного шлюза (M14)
/// и ручное подтверждение таймаутов зависших транзакций (кейс TC-15).
/// </summary>
public partial class EmulatorViewModel : ObservableObject, IDisposable
{
    private readonly IVirtualClockService _clockService;
    private readonly IPaymentEmulator _paymentEmulator;
    private readonly INavigationService _navigationService;

    #region Блок 1: Управление виртуальным временем

    [ObservableProperty]
    private string _currentVirtualTimeText = string.Empty;

    [ObservableProperty]
    private string _currentClockModeText = string.Empty;

    #endregion

    #region Блок 2: Настройка платёжного шлюза (M14)

    [ObservableProperty]
    private int _selectedModeIndex;

    [ObservableProperty]
    private int _delayMilliseconds;

    [ObservableProperty]
    private ObservableCollection<EmulatorLogEntryDto> _incomingRequests = [];

    #endregion

    #region Блок 3: Ручное подтверждение зависших платежей (TC-15)

    [ObservableProperty]
    private string _pendingPaymentId = string.Empty;

    #endregion

    #region Состояние и сообщения

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _resolveMessage;

    #endregion

    /// <summary>
    /// Событие запроса закрытия окна эмулятора.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="EmulatorViewModel"/>.
    /// </summary>
    public EmulatorViewModel(
        IVirtualClockService clockService,
        IPaymentEmulator paymentEmulator,
        INavigationService navigationService)
    {
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _paymentEmulator = paymentEmulator ?? throw new ArgumentNullException(nameof(paymentEmulator));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        // Инициализируем текущие значения эмулятора
        SelectedModeIndex = (int)_paymentEmulator.Mode;
        DelayMilliseconds = _paymentEmulator.DelayMilliseconds;

        UpdateClockDisplay(_clockService.CurrentVirtualTime, _clockService.Mode);

        _clockService.TimeChanged += OnVirtualTimeChanged;

        RefreshRequests();
    }

    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(() => UpdateClockDisplay(e.NewVirtualTime, _clockService.Mode));
        }
        else
        {
            UpdateClockDisplay(e.NewVirtualTime, _clockService.Mode);
        }
    }

    private void UpdateClockDisplay(DateTime time, ClockMode mode)
    {
        CurrentVirtualTimeText = time.ToString("dd.MM.yyyy HH:mm:ss");
        CurrentClockModeText = mode switch
        {
            ClockMode.Manual => "Ручной режим (Manual)",
            ClockMode.RealTime => "Реальное время (RealTime)",
            ClockMode.Accelerated => "Ускоренный режим (Accelerated)",
            _ => mode.ToString()
        };
    }

    #region Команды управления временем (M12)

    /// <summary>
    /// Смещает виртуальное время вперёд на 15 минут (срок истечения брони места).
    /// </summary>
    [RelayCommand]
    public void Advance15Minutes()
    {
        _clockService.AdvanceTime(TimeSpan.FromMinutes(15));
        StatusMessage = "Виртуальное время сдвинуто на +15 минут. Проверены сроки бронирования.";
    }

    /// <summary>
    /// Смещает виртуальное время вперёд на 1 час.
    /// </summary>
    [RelayCommand]
    public void Advance1Hour()
    {
        _clockService.AdvanceTime(TimeSpan.FromHours(1));
        StatusMessage = "Виртуальное время сдвинуто на +1 час.";
    }

    /// <summary>
    /// Смещает виртуальное время вперёд на 1 сутки.
    /// </summary>
    [RelayCommand]
    public void Advance1Day()
    {
        _clockService.AdvanceTime(TimeSpan.FromDays(1));
        StatusMessage = "Виртуальное время сдвинуто на +1 день.";
    }

    /// <summary>
    /// Сбрасывает виртуальное время к текущему системному времени компьютера.
    /// </summary>
    [RelayCommand]
    public void ResetTime()
    {
        _clockService.ResetToRealTime();
        StatusMessage = "Виртуальные часы сброшены на текущее системное время хоста.";
    }

    /// <summary>
    /// Принудительно запускает регламентные фоновые процессы ядра (снятие броней, отправка рейсов).
    /// </summary>
    [RelayCommand]
    public void TriggerJobs()
    {
        _clockService.TriggerScheduledJobs();
        StatusMessage = "Регламентные задачи выполнены: аннулированы просроченные брони и обновлены статусы рейсов.";
    }

    #endregion

    #region Настройка платёжного шлюза (M14)

    partial void OnSelectedModeIndexChanged(int value)
    {
        _paymentEmulator.Mode = (PaymentEmulatorMode)value;
        StatusMessage = $"Режим шлюза изменён на: {_paymentEmulator.Mode}";
    }

    partial void OnDelayMillisecondsChanged(int value)
    {
        _paymentEmulator.DelayMilliseconds = value;
    }

    /// <summary>
    /// Обновляет журнал входящих платёжных запросов.
    /// </summary>
    [RelayCommand]
    public void RefreshRequests()
    {
        IReadOnlyList<EmulatorLogEntryDto> list = _paymentEmulator.GetIncomingRequestsLog();
        IncomingRequests = new ObservableCollection<EmulatorLogEntryDto>(list);
    }

    /// <summary>
    /// Очищает журнал запросов эмулятора.
    /// </summary>
    [RelayCommand]
    public void ClearLog()
    {
        _paymentEmulator.ClearLog();
        IncomingRequests.Clear();
        StatusMessage = "Журнал запросов эмулятора очищен.";
    }

    #endregion

    #region Ручной резолв таймаутов (кейс TC-15)

    /// <summary>
    /// Вручную подтверждает зависший платёж как успешный (TC-15).
    /// </summary>
    [RelayCommand]
    public void ResolveSuccess()
    {
        ResolvePayment(markSuccess: true);
    }

    /// <summary>
    /// Вручную отклоняет зависший платёж (TC-15).
    /// </summary>
    [RelayCommand]
    public void ResolveReject()
    {
        ResolvePayment(markSuccess: false);
    }

    private void ResolvePayment(bool markSuccess)
    {
        ResolveMessage = null;

        if (!int.TryParse(PendingPaymentId.Trim(), out int paymentId) || paymentId <= 0)
        {
            ResolveMessage = "Введите корректный числовой ID платежа.";
            return;
        }

        Result result = _paymentEmulator.ManuallyResolvePayment(paymentId, markSuccess);
        if (result.IsFailure)
        {
            ResolveMessage = $"Ошибка резолва: {result.ErrorMessage}";
            return;
        }

        string outcome = markSuccess ? "УСПЕШНО ПОДТВЕРЖДЁН (билеты выпущены)" : "ОТКЛОНЁН (места освобождены)";
        ResolveMessage = $"Платёж #{paymentId} вручную {outcome}!";
        RefreshRequests();
    }

    #endregion

    /// <summary>
    /// Закрывает окно эмулятора.
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Освобождает ресурсы и отписывается от событий часов.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;
        GC.SuppressFinalize(this);
    }
}
