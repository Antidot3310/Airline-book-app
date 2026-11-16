using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Журнал аудита операций» W-15.
/// Обеспечивает просмотр сквозной истории системных событий и действий пользователей (M11)
/// в строгом режиме «только чтение» с фильтрацией по дате, роли и типу действия.
/// </summary>
public partial class AuditLogViewModel : ObservableObject
{
    private readonly IAuditLogService _auditLogService;
    private readonly IVirtualClockService _clockService;

    [ObservableProperty]
    private DateTime? _fromDate;

    [ObservableProperty]
    private DateTime? _toDate;

    [ObservableProperty]
    private int _selectedRoleIndex;

    [ObservableProperty]
    private string _actionFilter = string.Empty;

    [ObservableProperty]
    private ObservableCollection<AuditLogDto> _logs = [];

    [ObservableProperty]
    private int _totalLogsCount;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="AuditLogViewModel"/>.
    /// </summary>
    public AuditLogViewModel(
        IAuditLogService auditLogService,
        IVirtualClockService clockService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));

        DateTime current = _clockService.CurrentVirtualTime.Date;
        FromDate = current.AddDays(-7);
        ToDate = current.AddDays(1);

        RefreshLogs();
    }

    /// <summary>
    /// Загружает журнал аудита с учётом выбранных критериев фильтрации.
    /// </summary>
    [RelayCommand]
    public void RefreshLogs()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            UserRole? role = SelectedRoleIndex switch
            {
                1 => UserRole.Administrator,
                2 => UserRole.Cashier,
                3 => UserRole.Passenger,
                4 => UserRole.System,
                _ => null
            };

            string? action = string.IsNullOrWhiteSpace(ActionFilter) ? null : ActionFilter.Trim();

            IReadOnlyList<AuditLogDto> list = _auditLogService.GetLogs(FromDate, ToDate, role, action);
            Logs = new ObservableCollection<AuditLogDto>(list);
            TotalLogsCount = list.Count;

            if (list.Count == 0)
            {
                StatusMessage = "Записи аудита по указанным критериям не найдены.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки журнала аудита: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Сбрасывает критерии фильтрации к значениям по умолчанию.
    /// </summary>
    [RelayCommand]
    public void ResetFilters()
    {
        DateTime current = _clockService.CurrentVirtualTime.Date;
        FromDate = current.AddDays(-7);
        ToDate = current.AddDays(1);
        SelectedRoleIndex = 0;
        ActionFilter = string.Empty;

        RefreshLogs();
    }
}
