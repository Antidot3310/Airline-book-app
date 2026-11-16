using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Мои билеты» W-07.
/// Обеспечивает поиск билетов по паспорту, фильтрацию по статусам (Предстоящие, Прошедшие, Брони, Возвраты),
/// вызов окна предпросмотра маршрутной квитанции/чека W-10 и переход к оплате броней.
/// </summary>
public partial class MyTicketsViewModel : ObservableObject
{
    private readonly IOrderTicketService _orderTicketService;
    private readonly IDocumentService _documentService;
    private readonly ISessionService _sessionService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string _documentNumberFilter = string.Empty;

    [ObservableProperty]
    private int _selectedTab;

    /// <summary>
    /// Признаки активной выбранной вкладки для привязки стилей кнопок.
    /// </summary>
    public bool IsTabAll => SelectedTab == 0;
    public bool IsTabUpcoming => SelectedTab == 1;
    public bool IsTabPast => SelectedTab == 2;
    public bool IsTabBooked => SelectedTab == 3;
    public bool IsTabRefunded => SelectedTab == 4;

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsTabAll));
        OnPropertyChanged(nameof(IsTabUpcoming));
        OnPropertyChanged(nameof(IsTabPast));
        OnPropertyChanged(nameof(IsTabBooked));
        OnPropertyChanged(nameof(IsTabRefunded));
    }

    [ObservableProperty]
    private ObservableCollection<TicketItemViewModel> _allTickets = [];

    [ObservableProperty]
    private ObservableCollection<TicketItemViewModel> _filteredTickets = [];

    [ObservableProperty]
    private TicketItemViewModel? _selectedTicket;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Признак наличия билетов для отображения после фильтрации.
    /// </summary>
    public bool HasTickets => FilteredTickets.Count > 0;

    /// <summary>
    /// Событие перехода к оплате неоплаченного забронированного заказа W-06.
    /// </summary>
    public event Action<OrderDto>? OrderPayRequested;

    /// <summary>
    /// Событие перехода к поиску рейсов W-03.
    /// </summary>
    public event Action? NavigateToSearchRequested;

    /// <summary>
    /// Событие перехода к оформлению возврата билета W-09.
    /// </summary>
    public event Action<TicketItemViewModel>? RefundRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MyTicketsViewModel"/>.
    /// </summary>
    public MyTicketsViewModel(
        IOrderTicketService orderTicketService,
        IDocumentService documentService,
        ISessionService sessionService,
        IVirtualClockService clockService,
        INavigationService navigationService)
    {
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        // Первоначальная загрузка билетов текущего сеанса
        LoadSessionTickets();
    }

    /// <summary>
    /// Загружает билеты, оформленные в рамках активного сеанса пользователя.
    /// </summary>
    [RelayCommand]
    public void LoadSessionTickets()
    {
        try
        {
            IReadOnlyList<OrderDto> sessionOrders = _orderTicketService.GetOrdersForCurrentSession();
            var tickets = sessionOrders.SelectMany(o => o.Tickets).ToList();

            if (tickets.Count > 0)
            {
                PopulateTickets(tickets);

                if (string.IsNullOrWhiteSpace(DocumentNumberFilter))
                {
                    DocumentNumberFilter = tickets[0].PassengerDocument;
                }
            }
            else
            {
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось загрузить билеты сессии: {ex.Message}";
        }
    }

    /// <summary>
    /// Выполняет поиск билетов по номеру документа (паспорту).
    /// </summary>
    [RelayCommand]
    public void SearchTickets()
    {
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(DocumentNumberFilter))
        {
            StatusMessage = "Пожалуйста, введите серию и номер паспорта пассажира.";
            return;
        }

        IsLoading = true;

        try
        {
            IReadOnlyList<TicketDto> foundTickets = _orderTicketService.GetTicketsByClientDocument(DocumentNumberFilter.Trim());
            PopulateTickets(foundTickets);

            if (foundTickets.Count == 0)
            {
                StatusMessage = $"Билеты по документу «{DocumentNumberFilter.Trim()}» не найдены.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка поиска: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Заполняет коллекцию билетов и применяет текущий фильтр.
    /// </summary>
    private void PopulateTickets(IEnumerable<TicketDto> tickets)
    {
        DateTime virtualTime = _clockService.CurrentVirtualTime;

        var items = tickets.Select(t => new TicketItemViewModel
        {
            Id = t.Id,
            TicketNumber = t.TicketNumber,
            OrderId = t.OrderId,
            OrderNumber = t.OrderNumber,
            PassengerName = t.PassengerName,
            PassengerDocument = t.PassengerDocument,
            FlightId = t.FlightId,
            FlightNumber = t.FlightNumber,
            OriginAirportCode = t.OriginAirportCode,
            DestinationAirportCode = t.DestinationAirportCode,
            DepartureTime = t.DepartureTime,
            SeatNumber = t.SeatNumber,
            SeatClass = t.SeatClass,
            HasBaggage = t.HasBaggage,
            Price = t.Price,
            Status = t.Status,
            CurrentVirtualTime = virtualTime
        }).ToList();

        AllTickets = new ObservableCollection<TicketItemViewModel>(items);
        ApplyFilter();
    }

    /// <summary>
    /// Фильтрует список билетов по выбранной вкладке (0 = Все, 1 = Предстоящие, 2 = Прошедшие, 3 = Брони, 4 = Возвраты).
    /// </summary>
    public void ApplyFilter()
    {
        IEnumerable<TicketItemViewModel> query = AllTickets;

        query = SelectedTab switch
        {
            1 => query.Where(t => t.IsUpcoming),
            2 => query.Where(t => t.IsPast),
            3 => query.Where(t => t.IsBooked),
            4 => query.Where(t => t.IsRefunded),
            _ => query
        };

        FilteredTickets = new ObservableCollection<TicketItemViewModel>(query.OrderByDescending(t => t.DepartureTime));
        OnPropertyChanged(nameof(HasTickets));
    }

    /// <summary>
    /// Переключает активную вкладку фильтра билетов.
    /// </summary>
    /// <param name="tabIndex">Индекс вкладки (0..4).</param>
    [RelayCommand]
    public void SelectTab(object? tabIndex)
    {
        if (tabIndex != null && int.TryParse(tabIndex.ToString(), out int index))
        {
            SelectedTab = index;
            ApplyFilter();
        }
    }

    /// <summary>
    /// Открывает окно предпросмотра маршрутной квитанции электронного билета (W-10).
    /// </summary>
    /// <param name="ticket">Выбранный авиабилет.</param>
    [RelayCommand]
    public void ViewTicketDocument(TicketItemViewModel? ticket)
    {
        if (ticket == null)
        {
            return;
        }

        try
        {
            PrintDocumentDto doc = _documentService.GenerateTicketDocument(ticket.Id);
            _navigationService.OpenDocumentPreview(doc);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось сформировать квитанцию: {ex.Message}";
        }
    }

    /// <summary>
    /// Открывает окно предпросмотра кассового чека оплаты заказа (W-10).
    /// </summary>
    /// <param name="ticket">Выбранный авиабилет.</param>
    [RelayCommand]
    public void ViewReceipt(TicketItemViewModel? ticket)
    {
        if (ticket == null)
        {
            return;
        }

        try
        {
            PrintDocumentDto doc = _documentService.GeneratePaymentReceiptForOrder(ticket.OrderId);
            _navigationService.OpenDocumentPreview(doc);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось сформировать чек оплаты: {ex.Message}";
        }
    }

    /// <summary>
    /// Инициирует оплату ранее забронированного билета через экран W-06.
    /// </summary>
    /// <param name="ticket">Выбранный забронированный билет.</param>
    [RelayCommand]
    public void PayBooking(TicketItemViewModel? ticket)
    {
        if (ticket == null)
        {
            return;
        }

        Result<OrderDto> orderResult = _orderTicketService.GetOrderById(ticket.OrderId);
        if (orderResult.IsSuccess)
        {
            OrderPayRequested?.Invoke(orderResult.Value);
        }
        else
        {
            StatusMessage = $"Не удалось получить данные заказа: {orderResult.ErrorMessage}";
        }
    }

    /// <summary>
    /// Инициирует возврат билета (W-09).
    /// </summary>
    /// <param name="ticket">Выбранный авиабилет для возврата.</param>
    [RelayCommand]
    public void RefundTicket(TicketItemViewModel? ticket)
    {
        if (ticket == null)
        {
            return;
        }

        _navigationService.OpenRefundDialog(ticket.Id);
        LoadSessionTickets();
        RefundRequested?.Invoke(ticket);
    }

    /// <summary>
    /// Переходит к поиску рейсов W-03 для покупки нового билета.
    /// </summary>
    [RelayCommand]
    public void NavigateToSearch()
    {
        NavigateToSearchRequested?.Invoke();
    }
}
