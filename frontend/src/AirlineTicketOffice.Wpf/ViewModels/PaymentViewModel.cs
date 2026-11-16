using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления экрана оплаты заказа W-06.
/// Поддерживает оплату банковской картой и наличными (для кассира),
/// 15-минутный обратный отсчёт времени удержания брони (M12)
/// и ручной опрос зависших платежей (TC-15).
/// </summary>
public partial class PaymentViewModel : ObservableObject, IDisposable
{
    private readonly IPaymentService _paymentService;
    private readonly IOrderTicketService _orderTicketService;
    private readonly ISessionService _sessionService;
    private readonly IVirtualClockService _clockService;

    [ObservableProperty]
    private int _orderId;

    [ObservableProperty]
    private string _orderNumber = string.Empty;

    [ObservableProperty]
    private decimal _totalAmount;

    [ObservableProperty]
    private DateTime _holdExpiresAt;

    [ObservableProperty]
    private string _flightInfo = string.Empty;

    [ObservableProperty]
    private string _passengersSummary = string.Empty;

    [ObservableProperty]
    private string _remainingTimeText = "15:00";

    [ObservableProperty]
    private bool _isExpired;

    [ObservableProperty]
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Card;

    [ObservableProperty]
    private string _cardNumber = string.Empty;

    [ObservableProperty]
    private string _cardHolder = string.Empty;

    [ObservableProperty]
    private string _expiryMonth = string.Empty;

    [ObservableProperty]
    private string _expiryYear = string.Empty;

    [ObservableProperty]
    private string _cvv = string.Empty;

    [ObservableProperty]
    private decimal _cashReceived;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isPaid;

    [ObservableProperty]
    private bool _isIndeterminate;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private int _paymentId;

    [ObservableProperty]
    private ObservableCollection<TicketDto> _tickets = [];

    /// <summary>
    /// Событие запроса возврата к поиску рейсов W-03.
    /// </summary>
    public event Action? NavigateToSearchRequested;

    /// <summary>
    /// Событие запроса перехода к окну предпросмотра и печати документов W-10.
    /// </summary>
    public event Action<int>? PrintDocumentsRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="PaymentViewModel"/>.
    /// </summary>
    public PaymentViewModel(
        IPaymentService paymentService,
        IOrderTicketService orderTicketService,
        ISessionService sessionService,
        IVirtualClockService clockService)
    {
        _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));

        _clockService.TimeChanged += OnVirtualTimeChanged;
    }

    /// <summary>
    /// Признак активной роли кассира (дает доступ к оплате наличными в кассе).
    /// </summary>
    public bool IsCashier => _sessionService.CurrentRole == UserRole.Cashier;

    /// <summary>
    /// Признак выбора оплаты банковской картой.
    /// </summary>
    public bool IsCardMethod => SelectedPaymentMethod == PaymentMethod.Card;

    /// <summary>
    /// Признак выбора оплаты наличными средствами.
    /// </summary>
    public bool IsCashMethod => SelectedPaymentMethod == PaymentMethod.Cash;

    /// <summary>
    /// Автоматически рассчитываемая сумма сдачи при оплате наличными в рублях.
    /// </summary>
    public decimal ChangeAmount => Math.Max(0m, CashReceived - TotalAmount);

    /// <summary>
    /// Признак достаточности внесенных наличных средств для полной оплаты заказа.
    /// </summary>
    public bool IsCashSufficient => CashReceived >= TotalAmount;

    /// <summary>
    /// Инициализирует экран оплаты параметрами созданного заказа.
    /// </summary>
    /// <param name="order">DTO созданного заказа.</param>
    public void Initialize(OrderDto order)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        OrderId = order.Id;
        OrderNumber = order.OrderNumber;
        TotalAmount = order.TotalAmount;
        HoldExpiresAt = order.HoldExpiresAt;

        // По умолчанию в кассе предлагаем наличные, для пассажира — карту
        SelectedPaymentMethod = IsCashier ? PaymentMethod.Cash : PaymentMethod.Card;
        CashReceived = TotalAmount;

        // Сводная информация о билетах
        Tickets = new ObservableCollection<TicketDto>(order.Tickets);

        if (order.Tickets.Count > 0)
        {
            TicketDto first = order.Tickets[0];
            FlightInfo = $"Рейс {first.FlightNumber} • Вылет {first.DepartureTime:dd.MM.yyyy HH:mm}";

            var passengerList = order.Tickets.Select(t => $"{t.PassengerName} (место {t.SeatNumber})");
            PassengersSummary = string.Join(", ", passengerList);
        }

        IsPaid = order.Status == OrderStatus.Paid;
        IsExpired = order.Status == OrderStatus.Expired;
        IsIndeterminate = false;
        ErrorMessage = null;
        SuccessMessage = null;

        UpdateRemainingTime();
    }

    /// <summary>
    /// Обработчик события изменения виртуального времени из ядра.
    /// </summary>
    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(UpdateRemainingTime);
        }
        else
        {
            UpdateRemainingTime();
        }
    }

    /// <summary>
    /// Пересчитывает оставшееся время действия брони и блокирует оплату при истечении.
    /// </summary>
    public void UpdateRemainingTime()
    {
        if (IsPaid)
        {
            RemainingTimeText = "Заказ оплачен";
            return;
        }

        DateTime currentTime = _clockService.CurrentVirtualTime;
        TimeSpan remaining = HoldExpiresAt - currentTime;

        if (remaining <= TimeSpan.Zero)
        {
            IsExpired = true;
            RemainingTimeText = "00:00 — Срок бронирования истёк";
            ErrorMessage = "Время действия брони истекло. Места аннулированы. Оплата заблокирована (TC-04Б).";
        }
        else
        {
            IsExpired = false;
            RemainingTimeText = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        }
    }

    /// <summary>
    /// Автоматически подставляет валидные тестовые реквизиты банковской карты.
    /// </summary>
    [RelayCommand]
    public void FillTestCard()
    {
        CardNumber = "4276 1234 5678 9012";
        CardHolder = "IVAN IVANOV";
        ExpiryMonth = "12";
        ExpiryYear = "28";
        Cvv = "123";
        ErrorMessage = null;
    }

    /// <summary>
    /// Переключает способ оплаты на банковскую карту.
    /// </summary>
    [RelayCommand]
    public void SelectCardMethod()
    {
        SelectedPaymentMethod = PaymentMethod.Card;
        OnPropertyChanged(nameof(IsCardMethod));
        OnPropertyChanged(nameof(IsCashMethod));
    }

    /// <summary>
    /// Переключает способ оплаты на наличные в кассе.
    /// </summary>
    [RelayCommand]
    public void SelectCashMethod()
    {
        SelectedPaymentMethod = PaymentMethod.Cash;
        OnPropertyChanged(nameof(IsCardMethod));
        OnPropertyChanged(nameof(IsCashMethod));
    }

    /// <summary>
    /// Выполняет проведение платежа по выбранному способу (карта / наличные).
    /// </summary>
    [RelayCommand]
    public void Pay()
    {
        if (IsExpired)
        {
            ErrorMessage = "Невозможно провести оплату: срок бронирования истёк.";
            return;
        }

        if (IsProcessing)
        {
            return;
        }

        IsProcessing = true;
        ErrorMessage = null;
        SuccessMessage = null;

        if (SelectedPaymentMethod == PaymentMethod.Card)
        {
            if (string.IsNullOrWhiteSpace(CardNumber) ||
                string.IsNullOrWhiteSpace(CardHolder) ||
                string.IsNullOrWhiteSpace(ExpiryMonth) ||
                string.IsNullOrWhiteSpace(ExpiryYear) ||
                string.IsNullOrWhiteSpace(Cvv))
            {
                ErrorMessage = "Пожалуйста, заполните все реквизиты банковской карты.";
                IsProcessing = false;
                return;
            }

            var card = new CardDetailsDto
            {
                CardNumber = CardNumber.Trim(),
                CardHolder = CardHolder.Trim(),
                ExpiryMonth = ExpiryMonth.Trim(),
                ExpiryYear = ExpiryYear.Trim(),
                Cvv = Cvv.Trim()
            };

            Result<PaymentResultDto> cardResult = _paymentService.PayWithCard(OrderId, card);
            ProcessPaymentOutcome(cardResult);
        }
        else
        {
            if (!IsCashSufficient)
            {
                ErrorMessage = $"Внесённая сумма ({CashReceived:N0} руб.) меньше суммы заказа ({TotalAmount:N0} руб.).";
                IsProcessing = false;
                return;
            }

            Result<PaymentResultDto> cashResult = _paymentService.PayWithCash(OrderId, CashReceived);
            ProcessPaymentOutcome(cashResult);
        }

        IsProcessing = false;
    }

    /// <summary>
    /// Обрабатывает результат проведения платежа от платёжного сервиса.
    /// </summary>
    private void ProcessPaymentOutcome(Result<PaymentResultDto> result)
    {
        if (result.IsFailure)
        {
            ErrorMessage = result.ErrorMessage;
            return;
        }

        PaymentResultDto payment = result.Value;
        PaymentId = payment.PaymentId;

        switch (payment.Status)
        {
            case PaymentStatus.Success:
                IsPaid = true;
                IsIndeterminate = false;
                ErrorMessage = null;
                SuccessMessage = "Заказ успешно оплачен! Электронные билеты оформлены.";
                RefreshOrderTickets();
                break;

            case PaymentStatus.Indeterminate:
                IsIndeterminate = true;
                IsPaid = false;
                ErrorMessage = "Платёж находится в статусе ожидания ответа от банка (Indeterminate). Нажмите кнопку «Проверить статус платежа» для ручного подтверждения (TC-15).";
                break;

            case PaymentStatus.Failed:
                ErrorMessage = "Платёж отклонён платёжной системой. Проверьте данные карты или повторите попытку.";
                break;
        }
    }

    /// <summary>
    /// Выполняет проверку актуального статуса платежа по идентификатору (кейс TC-15).
    /// </summary>
    [RelayCommand]
    public void CheckPaymentStatus()
    {
        if (PaymentId <= 0)
        {
            ErrorMessage = "Идентификатор платежа не найден.";
            return;
        }

        IsProcessing = true;
        Result<PaymentStatus> statusResult = _paymentService.CheckPaymentStatus(PaymentId);
        IsProcessing = false;

        if (statusResult.IsFailure)
        {
            ErrorMessage = $"Не удалось проверить статус: {statusResult.ErrorMessage}";
            return;
        }

        switch (statusResult.Value)
        {
            case PaymentStatus.Success:
                IsPaid = true;
                IsIndeterminate = false;
                ErrorMessage = null;
                SuccessMessage = "Платёж подтверждён банком! Заказ переведён в статус «Оплачен».";
                RefreshOrderTickets();
                break;

            case PaymentStatus.Indeterminate:
                ErrorMessage = "Статус платежа всё ещё уточняется. Проверьте через несколько секунд или используйте эмулятор W-16.";
                break;

            case PaymentStatus.Failed:
                IsIndeterminate = false;
                ErrorMessage = "Платёж отклонён платёжным шлюзом.";
                break;
        }
    }

    /// <summary>
    /// Обновляет список билетов оплаченного заказа.
    /// </summary>
    private void RefreshOrderTickets()
    {
        Result<OrderDto> orderResult = _orderTicketService.GetOrderById(OrderId);
        if (orderResult.IsSuccess)
        {
            Tickets = new ObservableCollection<TicketDto>(orderResult.Value.Tickets);
        }
    }

    /// <summary>
    /// Выполняет добровольную отмену брони пользователем до оплаты.
    /// </summary>
    [RelayCommand]
    public void CancelBooking()
    {
        _orderTicketService.CancelOrder(OrderId);
        NavigateToSearchRequested?.Invoke();
    }

    /// <summary>
    /// Переходит обратно к экрану поиска рейсов.
    /// </summary>
    [RelayCommand]
    public void NavigateToSearch()
    {
        NavigateToSearchRequested?.Invoke();
    }

    /// <summary>
    /// Переходит к экрану предпросмотра и печати документов W-10.
    /// </summary>
    [RelayCommand]
    public void PrintDocuments()
    {
        PrintDocumentsRequested?.Invoke(OrderId);
    }

    /// <summary>
    /// Отслеживает изменение внесённой суммы наличных для пересчёта сдачи.
    /// </summary>
    partial void OnCashReceivedChanged(decimal value)
    {
        OnPropertyChanged(nameof(ChangeAmount));
        OnPropertyChanged(nameof(IsCashSufficient));
    }

    /// <summary>
    /// Отслеживает изменение выбранного способа оплаты.
    /// </summary>
    partial void OnSelectedPaymentMethodChanged(PaymentMethod value)
    {
        OnPropertyChanged(nameof(IsCardMethod));
        OnPropertyChanged(nameof(IsCashMethod));
    }

    /// <summary>
    /// Освобождает ресурсы и отписывается от событий виртуального времени.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;
        GC.SuppressFinalize(this);
    }
}
