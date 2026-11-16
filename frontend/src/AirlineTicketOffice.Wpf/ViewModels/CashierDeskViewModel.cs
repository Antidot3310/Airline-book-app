using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clients;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления рабочего места кассира W-08.
/// Включает поиск и регистрацию клиентов (левая панель),
/// быстрый возврат и оформление продажи (центральная панель),
/// а также финансовые итоги текущей смены (правая панель).
/// </summary>
public partial class CashierDeskViewModel : ObservableObject, IDisposable
{
    private readonly IClientService _clientService;
    private readonly IOrderTicketService _orderTicketService;
    private readonly ISessionService _sessionService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;
    private readonly IServiceScopeFactory _scopeFactory;

    #region Левая панель: Клиенты и пассажиры

    [ObservableProperty]
    private string _clientSearchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ClientDto> _foundClients = [];

    [ObservableProperty]
    private ClientDto? _selectedClient;

    [ObservableProperty]
    private bool _isCreatingClient;

    [ObservableProperty]
    private string _editFullName = string.Empty;

    [ObservableProperty]
    private string _editDocumentNumber = string.Empty;

    [ObservableProperty]
    private string _editPhone = string.Empty;

    [ObservableProperty]
    private string _editEmail = string.Empty;

    #endregion

    #region Центральная панель: Продажи и быстрый возврат

    [ObservableProperty]
    private string _ticketNumberToRefund = string.Empty;

    [ObservableProperty]
    private ObservableCollection<OrderDto> _recentOrders = [];

    #endregion

    #region Правая панель: Итоги смены кассира

    [ObservableProperty]
    private int _ticketsSoldCount;

    [ObservableProperty]
    private decimal _cashTotal;

    [ObservableProperty]
    private decimal _cardTotal;

    [ObservableProperty]
    private decimal _refundsTotal;

    /// <summary>
    /// Итоговая чистая выручка кассы за смену (наличные + безналичные - возвраты).
    /// </summary>
    public decimal NetRevenue => (CashTotal + CardTotal) - RefundsTotal;

    #endregion

    #region Общие свойства состояния

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    #endregion

    /// <summary>
    /// Событие запроса перехода к оформлению новой продажи билетов в W-03 с возможной подстановкой выбранного клиента.
    /// </summary>
    public event Action<ClientDto?>? NewSaleRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CashierDeskViewModel"/>.
    /// </summary>
    public CashierDeskViewModel(
        IClientService clientService,
        IOrderTicketService orderTicketService,
        ISessionService sessionService,
        IVirtualClockService clockService,
        INavigationService navigationService,
        IServiceScopeFactory scopeFactory)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        // Подписываемся на виртуальное время для автоматического пересчёта смены
        _clockService.TimeChanged += OnVirtualTimeChanged;

        // Первоначальная загрузка данных
        SearchClients();
        LoadRecentOrders();
        RefreshShiftSummary();
    }

    /// <summary>
    /// Обработчик тика виртуального времени.
    /// </summary>
    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        if (Application.Current?.Dispatcher != null)
        {
            Application.Current.Dispatcher.Invoke(RefreshShiftSummary);
        }
        else
        {
            RefreshShiftSummary();
        }
    }

    #region Команды левой панели (Клиенты)

    /// <summary>
    /// Выполняет поиск клиентов по ФИО или номеру паспорта.
    /// </summary>
    [RelayCommand]
    public void SearchClients()
    {
        StatusMessage = null;

        try
        {
            IReadOnlyList<ClientDto> clients = _clientService.SearchClients(ClientSearchQuery?.Trim() ?? string.Empty);
            FoundClients = new ObservableCollection<ClientDto>(clients);

            if (clients.Count == 0 && !string.IsNullOrWhiteSpace(ClientSearchQuery))
            {
                StatusMessage = $"Клиенты по запросу «{ClientSearchQuery}» не найдены. Вы можете создать нового.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка поиска клиентов: {ex.Message}";
        }
    }

    /// <summary>
    /// Выбирает клиента из списка для просмотра/редактирования или продажи.
    /// </summary>
    /// <param name="client">Выбранный клиент.</param>
    [RelayCommand]
    public void SelectClient(ClientDto? client)
    {
        SelectedClient = client;
        if (client != null)
        {
            EditFullName = client.FullName;
            EditDocumentNumber = client.DocumentNumber;
            EditPhone = client.ContactPhone ?? string.Empty;
            EditEmail = client.ContactEmail ?? string.Empty;
            IsCreatingClient = false;
        }
    }

    /// <summary>
    /// Переключает форму в режим создания нового клиента.
    /// </summary>
    [RelayCommand]
    public void StartNewClient()
    {
        SelectedClient = null;
        IsCreatingClient = true;
        EditFullName = string.Empty;
        EditDocumentNumber = string.Empty;
        EditPhone = string.Empty;
        EditEmail = string.Empty;
        StatusMessage = null;
    }

    /// <summary>
    /// Отменяет создание нового клиента и возвращает предыдущий выбор.
    /// </summary>
    [RelayCommand]
    public void CancelEditClient()
    {
        IsCreatingClient = false;
        if (SelectedClient != null)
        {
            SelectClient(SelectedClient);
        }
        else if (FoundClients.Count > 0)
        {
            SelectClient(FoundClients[0]);
        }
    }

    /// <summary>
    /// Сохраняет карточку нового или обновляемого клиента.
    /// </summary>
    [RelayCommand]
    public void SaveClient()
    {
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(EditFullName) || string.IsNullOrWhiteSpace(EditDocumentNumber))
        {
            StatusMessage = "Пожалуйста, заполните ФИО и номер документа пассажира.";
            return;
        }

        var clientDto = new ClientDto
        {
            Id = IsCreatingClient ? 0 : (SelectedClient?.Id ?? 0),
            FullName = EditFullName.Trim(),
            DocumentNumber = EditDocumentNumber.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(EditPhone) ? null : EditPhone.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(EditEmail) ? null : EditEmail.Trim()
        };

        Result<ClientDto> result = _clientService.CreateOrUpdateClient(clientDto);
        if (result.IsFailure)
        {
            StatusMessage = $"Ошибка сохранения: {result.ErrorMessage}";
            return;
        }

        ClientDto saved = result.Value;
        StatusMessage = $"Пассажир «{saved.FullName}» успешно сохранён в базе!";
        IsCreatingClient = false;

        SearchClients();
        SelectClient(saved);
    }

    #endregion

    #region Команды центральной панели (Оформление и возврат)

    /// <summary>
    /// Запускает пошаговое оформление продажи билета в мастере поиска рейсов W-03.
    /// </summary>
    [RelayCommand]
    public void StartNewSale()
    {
        NewSaleRequested?.Invoke(SelectedClient);
    }

    /// <summary>
    /// Открывает окно оформления возврата билета W-09 по введённому номеру билета.
    /// </summary>
    [RelayCommand]
    public void OpenRefundByNumber()
    {
        StatusMessage = null;

        if (string.IsNullOrWhiteSpace(TicketNumberToRefund))
        {
            StatusMessage = "Введите номер билета для оформления возврата (например, TKT-A1B2C3D4E5).";
            return;
        }

        Result<TicketDto> ticketResult = _orderTicketService.GetTicketByNumber(TicketNumberToRefund.Trim());
        if (ticketResult.IsFailure)
        {
            StatusMessage = ticketResult.ErrorMessage;
            return;
        }

        _navigationService.OpenRefundDialog(ticketResult.Value.Id);
        TicketNumberToRefund = string.Empty;
        RefreshShiftSummary();
    }

    /// <summary>
    /// Загружает список последних заказов кассира.
    /// </summary>
    public void LoadRecentOrders()
    {
        try
        {
            IReadOnlyList<OrderDto> orders = _orderTicketService.GetOrdersForCurrentSession();
            RecentOrders = new ObservableCollection<OrderDto>(orders);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки заказов: {ex.Message}";
        }
    }

    #endregion

    #region Команды правой панели (Итоги смены)

    /// <summary>
    /// Выполняет расчёт и обновление финансовых показателей смены кассира за текущий день виртуального времени.
    /// </summary>
    [RelayCommand]
    public void RefreshShiftSummary()
    {
        try
        {
            DateTime shiftStart = _clockService.CurrentVirtualTime.Date;
            DateTime shiftEnd = shiftStart.AddDays(1);

            using IServiceScope scope = _scopeFactory.CreateScope();
            AirlineDbContext db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            TicketsSoldCount = db.Tickets
                .Count(t => (t.Status == TicketStatus.Paid || t.Status == TicketStatus.Used || t.Status == TicketStatus.Refunded)
                            && t.Order != null && t.Order.CreatedAt >= shiftStart && t.Order.CreatedAt < shiftEnd);

            CashTotal = db.Payments
                .Where(p => p.Status == PaymentStatus.Success && p.PaymentMethod == PaymentMethod.Cash
                            && p.CreatedAt >= shiftStart && p.CreatedAt < shiftEnd)
                .Sum(p => (decimal?)p.Amount) ?? 0m;

            CardTotal = db.Payments
                .Where(p => p.Status == PaymentStatus.Success && p.PaymentMethod == PaymentMethod.Card
                            && p.CreatedAt >= shiftStart && p.CreatedAt < shiftEnd)
                .Sum(p => (decimal?)p.Amount) ?? 0m;

            RefundsTotal = db.Refunds
                .Where(r => r.CreatedAt >= shiftStart && r.CreatedAt < shiftEnd)
                .Sum(r => (decimal?)r.Amount) ?? 0m;

            OnPropertyChanged(nameof(NetRevenue));
            LoadRecentOrders();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка обновления итогов смены: {ex.Message}";
        }
    }

    #endregion

    /// <summary>
    /// Освобождает ресурсы и отписывается от событий виртуального времени.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;
        GC.SuppressFinalize(this);
    }
}
