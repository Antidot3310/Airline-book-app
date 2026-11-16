using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Refunds;
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

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Текстовое представление процента возврата.
    /// </summary>
    public string RefundPercentText => $"{RefundPercent}%";

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
        IOrderTicketService orderTicketService)
    {
        _refundService = refundService ?? throw new ArgumentNullException(nameof(refundService));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
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
        IsRefundAllowed = calc.IsRefundAllowed;

        OnPropertyChanged(nameof(RefundPercentText));
        OnPropertyChanged(nameof(RefundBadgeColor));
    }

    /// <summary>
    /// Подтверждает и выполняет операцию возврата билета.
    /// </summary>
    [RelayCommand]
    public void ConfirmRefund()
    {
        if (!IsRefundAllowed || IsProcessing)
        {
            return;
        }

        IsProcessing = true;
        ErrorMessage = null;

        Result<RefundResultDto> result = _refundService.ProcessRefund(TicketId);
        IsProcessing = false;

        if (result.IsFailure)
        {
            ErrorMessage = $"Ошибка проведения возврата: {result.ErrorMessage}";
            return;
        }

        RefundResultDto refundResult = result.Value;
        RefundCompleted?.Invoke(refundResult);

        // Формируем чек возврата W-10 и открываем окно предпросмотра
        try
        {
            PrintDocumentDto receiptDoc = _documentService.GenerateRefundReceipt(refundResult.RefundId);
            _navigationService.OpenDocumentPreview(receiptDoc);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Возврат успешно проведён, но не удалось сформировать чек: {ex.Message}",
                "Возврат билета",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
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
