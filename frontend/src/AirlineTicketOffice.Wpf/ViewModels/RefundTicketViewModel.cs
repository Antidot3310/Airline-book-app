using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления модального окна оформления возврата билета W-09.
/// Выполняет предварительный расчёт возврата по шкале (90% / 50% / 100%),
/// проведение возврата, освобождение места и печать чека возврата (W-10).
/// </summary>
public partial class RefundTicketViewModel : ObservableObject
{
    private readonly IRefundService _refundService;
    private readonly IDocumentService _documentService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;
    private readonly IOrderTicketService _orderTicketService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private int _ticketId;

    [ObservableProperty]
    private string _ticketNumber = string.Empty;

    [ObservableProperty]
    private string _orderNumber = string.Empty;

    [ObservableProperty]
    private string _passengerName = string.Empty;

    [ObservableProperty]
    private string _passengerDocument = string.Empty;

    [ObservableProperty]
    private string _flightNumber = string.Empty;

    [ObservableProperty]
    private string _routeText = string.Empty;

    [ObservableProperty]
    private string _departureTimeText = string.Empty;

    [ObservableProperty]
    private decimal _originalPrice;

    [ObservableProperty]
    private string _timeUntilDepartureText = string.Empty;

    [ObservableProperty]
    private string _ruleDescription = string.Empty;

    [ObservableProperty]
    private int _refundPercent;

    [ObservableProperty]
    private decimal _penaltyAmount;

    [ObservableProperty]
    private decimal _refundAmount;

    [ObservableProperty]
    private bool _isRefundAllowed;

    [ObservableProperty]
    private bool _isProcessing;

    /// <summary>
    /// Признак наличия у текущей сессии права на оформление возврата билета.
    /// </summary>
    [ObservableProperty]
    private bool _hasRefundPermission;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Признак доступности кнопки подтверждения возврата:
    /// расчёт разрешает возврат, у сессии есть право <see cref="SessionPermissions.RefundTicket"/>,
    /// и операция ещё не выполняется.
    /// </summary>
    public bool CanConfirmRefund => IsRefundAllowed && HasRefundPermission && !IsProcessing;

    /// <summary>
    /// Текстовое представление процента возврата.
    /// </summary>
    public string RefundPercentText => $"{RefundPercent}%";

    /// <summary>
    /// Текстовое представление процента удержания (штрафа).
    /// </summary>
    public string PenaltyPercentText => $"{100 - RefundPercent}%";

    /// <summary>
    /// Обновляет вычисляемые подписи при изменении процента возврата.
    /// </summary>
    partial void OnRefundPercentChanged(int value)
    {
        OnPropertyChanged(nameof(RefundPercentText));
        OnPropertyChanged(nameof(RefundBadgeColor));
        OnPropertyChanged(nameof(PenaltyPercentText));
    }

    /// <summary>
    /// Обновляет доступность команды подтверждения при изменении результата расчёта.
    /// </summary>
    partial void OnIsRefundAllowedChanged(bool value) => OnPropertyChanged(nameof(CanConfirmRefund));

    /// <summary>
    /// Обновляет доступность команды подтверждения при изменении прав сессии.
    /// </summary>
    partial void OnHasRefundPermissionChanged(bool value) => OnPropertyChanged(nameof(CanConfirmRefund));

    /// <summary>
    /// Блокирует повторное подтверждение возврата во время выполнения операции.
    /// </summary>
    partial void OnIsProcessingChanged(bool value) => OnPropertyChanged(nameof(CanConfirmRefund));

    /// <summary>
    /// Цвет фона бейджа процента возврата.
    /// </summary>
    public string RefundBadgeColor => RefundPercent switch
    {
        >= 90 => "#4CAF50", // Зелёный (100% или 90%)
        50 => "#FF9800",    // Оранжевый (50%)
        _ => "#E53935"      // Красный (0%)
    };

    /// <summary>
    /// Событие запроса закрытия окна диалога возврата.
    /// </summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Событие успешного завершения операции возврата.
    /// </summary>
    public event Action<RefundResultDto>? RefundCompleted;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RefundTicketViewModel"/>.
    /// </summary>
    public RefundTicketViewModel(
        IRefundService refundService,
        IDocumentService documentService,
        IVirtualClockService clockService,
        INavigationService navigationService,
        IOrderTicketService orderTicketService,
        ISessionService sessionService)
    {
        _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    /// <summary>
    /// Инициализирует модель представления данными билета и расчёта возврата.
    /// </summary>
    /// <param name="ticketId">Идентификатор авиабилета.</param>
    public void Initialize(int ticketId)
    {
        TicketId = ticketId;
        ErrorMessage = null;
        IsProcessing = false;
        HasRefundPermission = _sessionService.HasPermission(SessionPermissions.RefundTicket);

        Result<TicketDto> ticketResult = _orderTicketService.GetTicketById(ticketId);
        if (ticketResult.IsFailure)
        {
            ErrorMessage = ticketResult.ErrorMessage;
            IsRefundAllowed = false;
            return;
        }

        TicketDto ticket = ticketResult.Value;
        TicketNumber = ticket.TicketNumber;
        OrderNumber = ticket.OrderNumber;
        PassengerName = ticket.PassengerName;
        PassengerDocument = ticket.PassengerDocument;
        FlightNumber = ticket.FlightNumber;
        RouteText = $"{ticket.OriginAirportCode} ➔ {ticket.DestinationAirportCode}";
        DepartureTimeText = ticket.DepartureTime.ToString("dd.MM.yyyy HH:mm");
        OriginalPrice = ticket.Price;

        DateTime virtualTime = _clockService.CurrentVirtualTime;
        TimeSpan timeUntilDeparture = ticket.DepartureTime - virtualTime;

        if (timeUntilDeparture <= TimeSpan.Zero)
        {
            TimeUntilDepartureText = "Рейс уже вылетел";
        }
        else
        {
            int hours = (int)timeUntilDeparture.TotalHours;
            int minutes = timeUntilDeparture.Minutes;
            TimeUntilDepartureText = $"{hours} ч. {minutes} мин.";
        }

        Result<RefundCalculationDto> calcResult = _refundService.CalculateRefund(ticketId);
        if (calcResult.IsFailure)
        {
            ErrorMessage = calcResult.ErrorMessage;
            IsRefundAllowed = false;
            RefundPercent = 0;
            RefundAmount = 0m;
            PenaltyAmount = ticket.Price;
            RuleDescription = calcResult.ErrorMessage;
            return;
        }

        RefundCalculationDto calc = calcResult.Value;
        RefundPercent = calc.RefundPercent;
        RefundAmount = calc.RefundAmount;
        PenaltyAmount = calc.PenaltyAmount;
        RuleDescription = calc.ReasonDescription;
        IsRefundAllowed = calc.IsRefundAllowed && HasRefundPermission;

        if (!HasRefundPermission)
        {
            ErrorMessage = "Недостаточно прав для оформления возврата: текущая роль не имеет разрешения «RefundTicket».";
        }
    }

    /// <summary>
    /// Подтверждает и выполняет операцию возврата билета.
    /// </summary>
    [RelayCommand]
    public void ConfirmRefund()
    {
        if (!CanConfirmRefund)
        {
            return;
        }

        ErrorMessage = null;
        IsProcessing = true;

        Result<RefundResultDto> result;
        try
        {
            result = _refundService.ProcessRefund(TicketId);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Ошибка проведения возврата: {ex.Message}";
            return;
        }
        finally
        {
            IsProcessing = false;
        }

        if (result.IsFailure)
        {
            ErrorMessage = $"Ошибка проведения возврата: {result.ErrorMessage}";
            return;
        }

        RefundResultDto refundResult = result.Value;

        // Повторный возврат того же билета недопустим — блокируем кнопку до закрытия окна.
        IsRefundAllowed = false;
        RefundAmount = refundResult.RefundAmount;
        PenaltyAmount = refundResult.PenaltyAmount;
        RefundPercent = refundResult.RefundPercent;

        RefundCompleted?.Invoke(refundResult);

        // Формируем чек возврата W-10 и открываем окно предпросмотра.
        try
        {
            PrintDocumentDto receiptDoc = _documentService.GenerateRefundReceipt(refundResult.RefundId);
            _navigationService.OpenDocumentPreview(receiptDoc);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Возврат успешно проведён, но чек возврата не сформирован: {ex.Message}";
            return;
        }

        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Отменяет возврат и закрывает диалоговое окно.
    /// </summary>
    [RelayCommand]
    public void Cancel()
    {
        CloseRequested?.Invoke();
    }
}
