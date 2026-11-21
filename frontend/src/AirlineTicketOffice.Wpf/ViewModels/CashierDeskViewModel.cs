using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clients;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления рабочего места кассира W-08.
/// Левая панель — поиск и регистрация клиентов, центральная — оформление продажи
/// и быстрый возврат билета, правая — финансовые итоги текущей смены.
/// Работает исключительно через контракты <c>AirlineTicketOffice.Core.Contracts.*</c>:
/// прямой доступ к базе данных и подмена DTO ядра отсутствуют.
/// </summary>
public partial class CashierDeskViewModel : ObservableObject, IDisposable
{
    private const int RecentOrdersLimit = 25;

    private readonly IClientService _clientService;
    private readonly IOrderTicketService _orderTicketService;
    private readonly ISessionService _sessionService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;
    private readonly IReportService _reportService;

    private DateTime _lastSummaryDate = DateTime.MinValue;
    private DateTime _lastOrdersMinute = DateTime.MinValue;

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
    private ObservableCollection<CashierOrderRowViewModel> _recentOrders = [];

    #endregion

    #region Правая панель: Итоги смены кассира

    [ObservableProperty]
    private int _ticketsSoldCount;

    [ObservableProperty]
    private int _cashierTicketsSoldCount;

    [ObservableProperty]
    private decimal _cashierSalesTotal;

    [ObservableProperty]
    private decimal _selfServiceSalesTotal;

    [ObservableProperty]
    private decimal _refundsTotal;

    [ObservableProperty]
    private int _pendingOrdersCount;

    [ObservableProperty]
    private string _nearestHoldText = "Активных броней нет.";

    /// <summary>
    /// Итоговая чистая выручка за смену: продажи через кассу и самообслуживание минус возвраты.
    /// </summary>
    public decimal NetRevenue => (CashierSalesTotal + SelfServiceSalesTotal) - RefundsTotal;

    /// <summary>
    /// Признак наличия неоплаченных заказов с активным удержанием мест.
    /// </summary>
    public bool HasPendingOrders => PendingOrdersCount > 0;

    #endregion

    #region Общие свойства состояния

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Текущая роль активного сеанса в человекочитаемом виде.
    /// </summary>
    public string CurrentRoleTitle => _sessionService.CurrentRole switch
    {
        UserRole.Cashier => "Кассир",
        UserRole.Administrator => "Администратор",
        _ => "Пассажир"
    };

    /// <summary>
    /// Признак права на управление карточками клиентов.
    /// </summary>
    public bool CanManageClients => _sessionService.HasPermission(SessionPermissions.ManageClients);

    /// <summary>
    /// Признак права на оформление нового бронирования.
    /// </summary>
    public bool CanCreateBooking => _sessionService.HasPermission(SessionPermissions.CreateBooking);

    /// <summary>
    /// Признак права на оформление возврата билета.
    /// </summary>
    public bool CanRefundTickets => _sessionService.HasPermission(SessionPermissions.RefundTicket);

    #endregion

    /// <summary>
    /// Событие запроса перехода к мастеру оформления новой продажи (W-03…W-06).
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
        IReportService reportService)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
        _orderTicketService = orderTicketService ?? throw new ArgumentNullException(nameof(orderTicketService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));

        _clockService.TimeChanged += OnVirtualTimeChanged;

        SearchClients();
        RefreshShiftSummary();
    }

    /// <summary>
    /// Обработчик тика виртуального времени: пересчитывает итоги смены при смене даты
    /// и обновляет сроки броней при смене минуты, не выполняя тяжёлых отчётов на каждом тике.
    /// </summary>
    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        DateTime newTime = e.NewVirtualTime;
        bool shiftChanged = newTime.Date != _lastSummaryDate;
        bool minuteChanged = TruncateToMinute(newTime) != _lastOrdersMinute;

        void ApplyTimeChange()
        {
            if (shiftChanged)
            {
                RefreshShiftSummary();
            }
            else if (minuteChanged)
            {
                LoadRecentOrders();
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            ApplyTimeChange();
        }
        else
        {
            dispatcher.Invoke(ApplyTimeChange);
        }
    }

    private static DateTime TruncateToMinute(DateTime value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));

    partial void OnPendingOrdersCountChanged(int value) =>
        OnPropertyChanged(nameof(HasPendingOrders));

    partial void OnSelectedClientChanged(ClientDto? value)
    {
        if (value is not null)
        {
            LoadEditorFromClient(value);
        }
    }

    #region Команды левой панели (Клиенты)

    /// <summary>
    /// Выполняет поиск клиентов по ФИО или номеру паспорта через <see cref="IClientService.SearchClients"/>.
    /// </summary>
    [RelayCommand]
    public void SearchClients()
    {
        StatusMessage = null;

        try
        {
            ReloadClients();

            if (FoundClients.Count == 0 && !string.IsNullOrWhiteSpace(ClientSearchQuery))
            {
                StatusMessage = $"Клиенты по запросу «{ClientSearchQuery.Trim()}» не найдены. Вы можете создать нового.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка поиска клиентов: {ex.Message}";
        }
    }

    /// <summary>
    /// Выбирает клиента из списка для просмотра, редактирования или продажи.
    /// </summary>
    /// <param name="client">Выбранный клиент.</param>
    [RelayCommand]
    public void SelectClient(ClientDto? client)
    {
        SelectedClient = client;
    }

    /// <summary>
    /// Переключает форму в режим создания нового клиента.
    /// </summary>
    [RelayCommand]
    public void StartNewClient()
    {
        StatusMessage = null;
        SelectedClient = null;
        IsCreatingClient = true;
        EditFullName = string.Empty;
        EditDocumentNumber = string.Empty;
        EditPhone = string.Empty;
        EditEmail = string.Empty;
    }

    /// <summary>
    /// Отменяет создание нового клиента и восстанавливает карточку выбранного клиента.
    /// </summary>
    [RelayCommand]
    public void CancelEditClient()
    {
        IsCreatingClient = false;

        if (SelectedClient is not null)
        {
            LoadEditorFromClient(SelectedClient);
        }
        else if (FoundClients.Count > 0)
        {
            SelectedClient = FoundClients[0];
        }
        else
        {
            EditFullName = string.Empty;
            EditDocumentNumber = string.Empty;
            EditPhone = string.Empty;
            EditEmail = string.Empty;
        }
    }

    /// <summary>
    /// Сохраняет карточку нового или обновляемого клиента через
    /// <see cref="IClientService.CreateOrUpdateClient"/> с передачей <see cref="ClientDto"/>.
    /// </summary>
    [RelayCommand]
    public void SaveClient()
    {
        StatusMessage = null;

        if (!EnsurePermission(SessionPermissions.ManageClients, "сохранение карточки клиента"))
        {
            return;
        }

        string fullName = EditFullName.Trim();
        string documentNumber = EditDocumentNumber.Trim();
        string? phone = NormalizeOptional(EditPhone);
        string? email = NormalizeOptional(EditEmail);

        if (!TryValidateClient(fullName, documentNumber, phone, email, out string? validationError))
        {
            StatusMessage = validationError;
            return;
        }

        try
        {
            if (IsCreatingClient)
            {
                Result<ClientDto> existing = _clientService.FindByDocument(documentNumber);
                if (existing.IsSuccess)
                {
                    IsCreatingClient = false;
                    ReloadClients();
                    SelectedClient = FoundClients.FirstOrDefault(c => c.Id == existing.Value.Id) ?? existing.Value;
                    StatusMessage = $"Пассажир с документом {documentNumber} уже зарегистрирован: {existing.Value.FullName}.";
                    return;
                }
            }

            var clientDto = new ClientDto
            {
                Id = IsCreatingClient ? 0 : (SelectedClient?.Id ?? 0),
                FullName = fullName,
                DocumentNumber = documentNumber,
                ContactPhone = phone,
                ContactEmail = email
            };

            Result<ClientDto> result = _clientService.CreateOrUpdateClient(clientDto);
            if (result.IsFailure)
            {
                StatusMessage = $"Ошибка сохранения: {result.ErrorMessage}";
                return;
            }

            ClientDto saved = result.Value;
            IsCreatingClient = false;
            ReloadClients();
            SelectedClient = FoundClients.FirstOrDefault(c => c.Id == saved.Id) ?? saved;
            StatusMessage = $"Пассажир «{saved.FullName}» успешно сохранён в базе.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка сохранения клиента: {ex.Message}";
        }
    }

    #endregion

    #region Вспомогательные методы левой панели

    private void ReloadClients()
    {
        IReadOnlyList<ClientDto> clients = _clientService.SearchClients(ClientSearchQuery?.Trim() ?? string.Empty);
        FoundClients = new ObservableCollection<ClientDto>(clients);
    }

    private void LoadEditorFromClient(ClientDto client)
    {
        EditFullName = client.FullName;
        EditDocumentNumber = client.DocumentNumber;
        EditPhone = client.ContactPhone ?? string.Empty;
        EditEmail = client.ContactEmail ?? string.Empty;
        IsCreatingClient = false;
    }

    private bool EnsurePermission(string permission, string actionDescription)
    {
        if (_sessionService.HasPermission(permission))
        {
            return true;
        }

        StatusMessage = $"Недостаточно прав для действия «{actionDescription}». Текущая роль: {CurrentRoleTitle}.";
        return false;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryValidateClient(
        string fullName,
        string documentNumber,
        string? phone,
        string? email,
        out string? errorMessage)
    {
        errorMessage = null;

        if (fullName.Length < 3 || !fullName.Contains(' ', StringComparison.Ordinal))
        {
            errorMessage = "Укажите фамилию и имя пассажира (не менее двух слов).";
            return false;
        }

        if (documentNumber.Length < 4)
        {
            errorMessage = "Номер документа должен содержать не менее 4 символов.";
            return false;
        }

        if (phone is not null && phone.Count(char.IsDigit) < 10)
        {
            errorMessage = "Контактный телефон должен содержать не менее 10 цифр.";
            return false;
        }

        if (email is not null)
        {
            int atIndex = email.IndexOf('@', StringComparison.Ordinal);
            int dotIndex = email.LastIndexOf('.');
            bool hasValidEmailShape = atIndex > 0 && dotIndex > atIndex + 1 && dotIndex < email.Length - 1;
            if (!hasValidEmailShape)
            {
                errorMessage = "Укажите корректный адрес электронной почты.";
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Команды центральной панели (Оформление и возврат)

    /// <summary>
    /// Запускает пошаговое оформление продажи билетов в мастере W-03…W-06
    /// с передачей карточки выбранного клиента.
    /// </summary>
    [RelayCommand]
    public void StartNewSale()
    {
        StatusMessage = null;

        if (!EnsurePermission(SessionPermissions.CreateBooking, "оформление новой продажи"))
        {
            return;
        }

        NewSaleRequested?.Invoke(SelectedClient);
    }

    /// <summary>
    /// Открывает окно оформления возврата билета W-09 по введённому номеру билета.
    /// Поиск выполняется через <see cref="IOrderTicketService.GetTicketByNumber"/>.
    /// </summary>
    [RelayCommand]
    public void OpenRefundByNumber()
    {
        StatusMessage = null;

        if (!EnsurePermission(SessionPermissions.RefundTicket, "оформление возврата билета"))
        {
            return;
        }

        string ticketNumber = TicketNumberToRefund.Trim();
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            StatusMessage = "Введите номер билета для оформления возврата (например, TKT-A1B2C3D4E5).";
            return;
        }

        try
        {
            Result<TicketDto> ticketResult = _orderTicketService.GetTicketByNumber(ticketNumber);
            if (ticketResult.IsFailure)
            {
                StatusMessage = ticketResult.ErrorMessage;
                return;
            }

            _navigationService.OpenRefundDialog(ticketResult.Value.Id);
            TicketNumberToRefund = string.Empty;
            RefreshShiftSummary();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка оформления возврата: {ex.Message}";
        }
    }

    #endregion

    #region Команды правой панели (Итоги смены)

    /// <summary>
    /// Выполняет расчёт и обновление финансовых показателей смены кассира
    /// за текущий день виртуального времени на основе отчётов <see cref="IReportService"/>.
    /// </summary>
    [RelayCommand]
    public void RefreshShiftSummary()
    {
        try
        {
            DateTime shiftStart = _clockService.CurrentVirtualTime.Date;
            DateTime periodEnd = shiftStart.AddDays(1).AddTicks(-1);
            _lastSummaryDate = shiftStart;

            ChannelSalesReportDto salesReport = _reportService.GetSalesByChannelsReport(shiftStart, periodEnd);
            RefundStatisticsReportDto refundsReport = _reportService.GetRefundsStatisticsReport(shiftStart, periodEnd);

            ChannelSalesItemDto? cashierSales = salesReport.Items
                .FirstOrDefault(item => item.Channel == OrderChannel.Cashier);
            ChannelSalesItemDto? selfServiceSales = salesReport.Items
                .FirstOrDefault(item => item.Channel == OrderChannel.SelfService);

            TicketsSoldCount = salesReport.TotalTickets;
            CashierTicketsSoldCount = cashierSales?.TicketsCount ?? 0;
            CashierSalesTotal = cashierSales?.TotalSalesAmount ?? 0m;
            SelfServiceSalesTotal = selfServiceSales?.TotalSalesAmount ?? 0m;
            RefundsTotal = refundsReport.TotalRefundAmount;

            OnPropertyChanged(nameof(NetRevenue));

            LoadRecentOrders();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка обновления итогов смены: {ex.Message}";
        }
    }

    /// <summary>
    /// Загружает последние заказы текущей сессии и рассчитывает активные удержания мест
    /// по каноническому свойству <see cref="OrderDto.HoldExpiresAt"/>.
    /// </summary>
    public void LoadRecentOrders()
    {
        try
        {
            DateTime virtualNow = _clockService.CurrentVirtualTime;
            _lastOrdersMinute = TruncateToMinute(virtualNow);

            IReadOnlyList<OrderDto> orders = _orderTicketService.GetOrdersForCurrentSession();
            List<CashierOrderRowViewModel> rows = orders
                .OrderByDescending(order => order.CreatedAt)
                .Take(RecentOrdersLimit)
                .Select(order => new CashierOrderRowViewModel(order, virtualNow))
                .ToList();

            RecentOrders = new ObservableCollection<CashierOrderRowViewModel>(rows);

            List<CashierOrderRowViewModel> pending = rows
                .Where(row => row.IsAwaitingPayment)
                .ToList();
            PendingOrdersCount = pending.Count;

            CashierOrderRowViewModel? nearest = pending
                .Where(row => !row.IsHoldExpired)
                .OrderBy(row => row.HoldExpiresAt)
                .FirstOrDefault();

            NearestHoldText = pending.Count == 0
                ? "Активных броней нет."
                : nearest is null
                    ? "Срок всех неоплаченных броней истёк."
                    : $"Ближайшая бронь действует до {nearest.HoldExpiresAt:HH:mm}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки заказов смены: {ex.Message}";
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
