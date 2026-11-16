using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Session;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления экрана ввода персональных данных пассажиров W-05.
/// Обеспечивает ввод ФИО, паспорта, контактных данных и опций багажа для каждого пассажира
/// с последующим созданием заказа в сервисе заказов (M7).
/// </summary>
public partial class PassengerFormViewModel : ObservableObject
{
    private readonly IOrderTicketService _orderTicketService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private int _flightId;

    [ObservableProperty]
    private string _flightNumber = string.Empty;

    [ObservableProperty]
    private string _routeInfo = string.Empty;

    [ObservableProperty]
    private string _departureTimeText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PassengerItemViewModel> _passengers = [];

    [ObservableProperty]
    private decimal _baseTicketsTotal;

    [ObservableProperty]
    private decimal _baggageTotal;

    [ObservableProperty]
    private decimal _orderTotal;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isSubmitting;

    /// <summary>
    /// Событие запроса возврата к интерактивной схеме мест W-04.
    /// </summary>
    public event Action? BackRequested;

    /// <summary>
    /// Событие успешного создания заказа для перехода к экрану оплаты W-06.
    /// </summary>
    public event Action<OrderDto>? OrderCreated;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="PassengerFormViewModel"/>.
    /// </summary>
    /// <param name="orderTicketService">Сервис управления заказами и билетами.</param>
    /// <param name="sessionService">Сервис управления пользовательскими сессиями.</param>
    public PassengerFormViewModel(
        IOrderTicketService orderTicketService,
        ISessionService sessionService)
    {
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    }

    /// <summary>
    /// Инициализирует форму списка пассажиров на основе выбранных мест на схеме W-04.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <param name="flightNumber">Номер рейса (например, "SU-101").</param>
    /// <param name="routeInfo">Обозначение маршрута (например, "SVO -> LED").</param>
    /// <param name="departureTimeText">Строковое представление даты и времени вылета.</param>
    /// <param name="selectedSeats">Список выбранных пользователем посадочных мест.</param>
    public void Initialize(
        int flightId,
        string flightNumber,
        string routeInfo,
        string departureTimeText,
        IReadOnlyList<SeatItemViewModel> selectedSeats)
    {
        if (selectedSeats == null || selectedSeats.Count == 0)
        {
            throw new ArgumentException("Список выбранных мест не может быть пустым.", nameof(selectedSeats));
        }

        FlightId = flightId;
        FlightNumber = flightNumber;
        RouteInfo = routeInfo;
        DepartureTimeText = departureTimeText;
        ErrorMessage = null;

        var items = new List<PassengerItemViewModel>();
        int index = 1;

        foreach (SeatItemViewModel seat in selectedSeats)
        {
            var p = new PassengerItemViewModel
            {
                Index = index++,
                SeatNumber = seat.SeatNumber,
                SeatClass = seat.Class,
                BaseSeatPrice = seat.Price,
                HasBaggage = false
            };

            p.BaggageChanged += RecalculateTotals;
            items.Add(p);
        }

        Passengers = new ObservableCollection<PassengerItemViewModel>(items);
        RecalculateTotals();
    }

    /// <summary>
    /// Пересчитывает финансовые итоги заказа: тарифы мест, багаж и итоговую сумму.
    /// </summary>
    public void RecalculateTotals()
    {
        BaseTicketsTotal = Passengers.Sum(p => p.BaseSeatPrice);
        BaggageTotal = Passengers.Sum(p => p.BaggagePrice);
        OrderTotal = Passengers.Sum(p => p.TicketPrice);
    }

    /// <summary>
    /// Выполняет проверку корректности заполнения обязательных полей каждого пассажира.
    /// </summary>
    /// <returns><c>true</c>, если все данные валидны; иначе <c>false</c>.</returns>
    public bool Validate()
    {
        ErrorMessage = null;

        if (Passengers.Count == 0)
        {
            ErrorMessage = "Список пассажиров пуст.";
            return false;
        }

        foreach (PassengerItemViewModel p in Passengers)
        {
            if (string.IsNullOrWhiteSpace(p.FullName))
            {
                ErrorMessage = $"Пожалуйста, укажите ФИО для пассажира №{p.Index} (место {p.SeatNumber}).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(p.DocumentNumber))
            {
                ErrorMessage = $"Пожалуйста, укажите номер документа для пассажира №{p.Index} (место {p.SeatNumber}).";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Возвращается назад к схеме выбора мест салона W-04.
    /// </summary>
    [RelayCommand]
    public void BackToSeatMap()
    {
        BackRequested?.Invoke();
    }

    /// <summary>
    /// Формирует запрос на создание заказа и отправляет его в сервис заказов (M7).
    /// </summary>
    [RelayCommand]
    public void CreateOrder()
    {
        if (!Validate())
        {
            return;
        }

        IsSubmitting = true;
        ErrorMessage = null;

        var request = new CreateOrderRequest
        {
            FlightId = FlightId,
            Passengers = Passengers.Select(p => new PassengerOrderItemDto
            {
                FullName = p.FullName.Trim(),
                DocumentNumber = p.DocumentNumber.Trim(),
                ContactPhone = string.IsNullOrWhiteSpace(p.ContactPhone) ? null : p.ContactPhone.Trim(),
                ContactEmail = string.IsNullOrWhiteSpace(p.ContactEmail) ? null : p.ContactEmail.Trim(),
                SeatNumber = p.SeatNumber,
                HasBaggage = p.HasBaggage
            }).ToList()
        };

        Result<OrderDto> result = _orderTicketService.CreateOrderWithBooking(request);

        IsSubmitting = false;

        if (result.IsSuccess)
        {
            OrderCreated?.Invoke(result.Value);
        }
        else
        {
            ErrorMessage = $"Не удалось оформить заказ: {result.ErrorMessage}";
        }
    }
}
