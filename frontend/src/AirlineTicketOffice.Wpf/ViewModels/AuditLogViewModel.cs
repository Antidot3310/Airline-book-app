using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Журнал аудита операций» W-15.
/// Обеспечивает просмотр сквозной истории системных событий и действий пользователей (M11)
/// в строгом режиме «только чтение»: фильтрация по диапазону виртуальных дат, роли инициатора
/// и ключевому слову в описании события. Все данные получаются через <c>IAuditLogService</c>.
/// </summary>
public partial class AuditLogViewModel : ObservableObject, IDisposable
{
    private readonly IAuditLogService _auditLogService;
    private readonly IVirtualClockService _clockService;
    private readonly ISessionService _sessionService;

    #region Права сессии и виртуальное время

    /// <summary>
    /// Признак наличия у текущей сессии права <see cref="SessionPermissions.ViewAuditLogs"/>.
    /// </summary>
    public bool CanViewAuditLogs { get; }

    /// <summary>
    /// Признак режима отказа в доступе: у сессии нет права просмотра журнала аудита.
    /// </summary>
    public bool IsAccessDenied => !CanViewAuditLogs;

    /// <summary>
    /// Пояснение для баннера отказа в доступе.
    /// </summary>
    public string AccessDeniedMessage =>
        "Недостаточно прав: журнал аудита доступен только администратору. " +
        "Смените роль через кнопку «Сменить роль» в верхней панели.";

    /// <summary>
    /// Текущее виртуальное время ядра для шапки раздела.
    /// </summary>
    [ObservableProperty]
    private string _virtualNowText = string.Empty;

    #endregion

    #region Фильтры журнала

    /// <summary>
    /// Допустимые варианты фильтра по роли инициатора операции.
    /// </summary>
    public ObservableCollection<AuditRoleFilterOption> RoleFilters { get; }

    /// <summary>
    /// Выбранный вариант фильтра по роли инициатора.
    /// </summary>
    [ObservableProperty]
    private AuditRoleFilterOption? _selectedRoleFilter;

    /// <summary>
    /// Начальная дата диапазона виртуального времени.
    /// </summary>
    [ObservableProperty]
    private DateTime? _fromDate;

    /// <summary>
    /// Конечная дата диапазона виртуального времени.
    /// </summary>
    [ObservableProperty]
    private DateTime? _toDate;

    /// <summary>
    /// Ключевое слово для поиска по типу действия, сущности, роли и описанию события.
    /// </summary>
    [ObservableProperty]
    private string _keyword = string.Empty;

    /// <summary>
    /// Пояснение к поиску по ключевому слову.
    /// </summary>
    public string KeywordHint =>
        "Поиск выполняется по типу действия, сущности, роли и тексту описания события.";

    #endregion

    #region Данные журнала и сводка

    /// <summary>
    /// Презентационные строки журнала аудита, удовлетворяющие фильтрам.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AuditLogRowViewModel> _logs = [];

    /// <summary>
    /// Количество найденных записей журнала.
    /// </summary>
    [ObservableProperty]
    private int _totalLogsCount;

    /// <summary>
    /// Количество действий администратора в выборке.
    /// </summary>
    [ObservableProperty]
    private int _administratorActionsCount;

    /// <summary>
    /// Количество действий кассира в выборке.
    /// </summary>
    [ObservableProperty]
    private int _cashierActionsCount;

    /// <summary>
    /// Количество действий пассажира в выборке.
    /// </summary>
    [ObservableProperty]
    private int _passengerActionsCount;

    /// <summary>
    /// Количество событий системных процессов в выборке.
    /// </summary>
    [ObservableProperty]
    private int _systemActionsCount;

    /// <summary>
    /// Признак наличия записей в выборке.
    /// </summary>
    public bool HasLogs => Logs.Count > 0;

    /// <summary>
    /// Признак отсутствия записей в выборке.
    /// </summary>
    public bool HasNoLogs => !HasLogs;

    /// <summary>
    /// Подпись количества найденных записей.
    /// </summary>
    public string TotalLogsText => $"Найдено записей: {TotalLogsCount}";

    partial void OnLogsChanged(ObservableCollection<AuditLogRowViewModel> value)
    {
        OnPropertyChanged(nameof(HasLogs));
        OnPropertyChanged(nameof(HasNoLogs));
    }

    partial void OnTotalLogsCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalLogsText));
    }

    #endregion

    #region Состояние и сообщения

    /// <summary>
    /// Информационное сообщение или текст ошибки загрузки журнала.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Признак выполнения запроса журнала аудита.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Текст-подсказка при отсутствии записей по фильтрам.
    /// </summary>
    public string EmptyStateMessage => "Записи аудита по указанным критериям не найдены. " +
                                       "Измените период, роль инициатора или ключевое слово.";

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="AuditLogViewModel"/>.
    /// </summary>
    /// <param name="auditLogService">Сервис журнала аудита M11.</param>
    /// <param name="clockService">Сервис виртуального системного времени M12.</param>
    /// <param name="sessionService">Сервис текущей сессии для проверки прав M1.</param>
    public AuditLogViewModel(
        IAuditLogService auditLogService,
        IVirtualClockService clockService,
        ISessionService sessionService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        CanViewAuditLogs = _sessionService.HasPermission(SessionPermissions.ViewAuditLogs);

        RoleFilters =
        [
            new AuditRoleFilterOption("Все роли", null),
            new AuditRoleFilterOption("Администратор", UserRole.Administrator),
            new AuditRoleFilterOption("Кассир", UserRole.Cashier),
            new AuditRoleFilterOption("Пассажир", UserRole.Passenger),
            new AuditRoleFilterOption("Системный процесс", UserRole.System)
        ];

        DateTime current = _clockService.CurrentVirtualTime.Date;
        FromDate = current.AddDays(-7);
        ToDate = current.AddDays(1);
        SelectedRoleFilter = RoleFilters[0];
        UpdateVirtualNowText(_clockService.CurrentVirtualTime);

        _clockService.TimeChanged += OnVirtualTimeChanged;

        if (CanViewAuditLogs)
        {
            RefreshLogs();
        }
        else
        {
            StatusMessage = AccessDeniedMessage;
        }
    }

    /// <summary>
    /// Загружает журнал аудита с учётом выбранных критериев фильтрации.
    /// Контракт <c>IAuditLogService.GetLogs</c> фильтрует тип действия на стороне ядра;
    /// для поиска по описанию события согласно W-15 выборка выполняется по диапазону дат и роли,
    /// а ключевое слово применяется к презентационным строкам во ViewModel.
    /// </summary>
    [RelayCommand]
    public void RefreshLogs()
    {
        StatusMessage = null;

        if (!CanViewAuditLogs)
        {
            Logs = [];
            ResetCounters();
            StatusMessage = AccessDeniedMessage;
            return;
        }

        if (!TryGetPeriod(out DateTime from, out DateTime to, out string? error))
        {
            Logs = [];
            ResetCounters();
            StatusMessage = error;
            return;
        }

        IsLoading = true;

        try
        {
            IReadOnlyList<AuditLogDto> entries =
                _auditLogService.GetLogs(from, to, SelectedRoleFilter?.Role, null);

            List<AuditLogRowViewModel> rows = entries
                .Select(entry => new AuditLogRowViewModel(entry))
                .Where(row => row.MatchesKeyword(Keyword))
                .ToList();

            Logs = new ObservableCollection<AuditLogRowViewModel>(rows);
            TotalLogsCount = rows.Count;
            AdministratorActionsCount = rows.Count(row => row.Role == UserRole.Administrator);
            CashierActionsCount = rows.Count(row => row.Role == UserRole.Cashier);
            PassengerActionsCount = rows.Count(row => row.Role == UserRole.Passenger);
            SystemActionsCount = rows.Count(row => row.Role == UserRole.System);

            if (rows.Count == 0)
            {
                StatusMessage = EmptyStateMessage;
            }
        }
        catch (Exception ex)
        {
            Logs = [];
            ResetCounters();
            StatusMessage = $"Ошибка загрузки журнала аудита: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Сбрасывает фильтры к значениям по умолчанию: последние 7 виртуальных суток,
    /// все роли и пустое ключевое слово.
    /// </summary>
    [RelayCommand]
    public void ResetFilters()
    {
        DateTime current = _clockService.CurrentVirtualTime.Date;
        FromDate = current.AddDays(-7);
        ToDate = current.AddDays(1);
        SelectedRoleFilter = RoleFilters.Count > 0 ? RoleFilters[0] : null;
        Keyword = string.Empty;

        RefreshLogs();
    }

    private bool TryGetPeriod(out DateTime from, out DateTime to, out string? error)
    {
        from = default;
        to = default;
        error = null;

        if (FromDate is null || ToDate is null)
        {
            error = "Укажите начальную и конечную даты диапазона аудита.";
            return false;
        }

        DateTime start = FromDate.Value.Date;
        DateTime end = ToDate.Value.Date;

        if (start > end)
        {
            error = "Начальная дата диапазона не может быть позже конечной.";
            return false;
        }

        if ((end - start).TotalDays > 3660d)
        {
            error = "Диапазон аудита не может превышать 10 лет.";
            return false;
        }

        from = start;
        to = end.AddDays(1).AddTicks(-1);
        return true;
    }

    private void ResetCounters()
    {
        TotalLogsCount = 0;
        AdministratorActionsCount = 0;
        CashierActionsCount = 0;
        PassengerActionsCount = 0;
        SystemActionsCount = 0;
    }

    #region Виртуальное время и освобождение ресурсов

    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            UpdateVirtualNowText(e.NewVirtualTime);
            RefreshLogs();
        });
    }

    private static void RunOnUiThread(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
            return;
        }

        action();
    }

    private void UpdateVirtualNowText(DateTime virtualNow)
    {
        VirtualNowText = $"Виртуальное время: {virtualNow:dd.MM.yyyy HH:mm}";
    }

    /// <summary>
    /// Отписывается от события изменения виртуального времени.
    /// </summary>
    public void Dispose()
    {
        _clockService.TimeChanged -= OnVirtualTimeChanged;
        GC.SuppressFinalize(this);
    }

    #endregion
}
