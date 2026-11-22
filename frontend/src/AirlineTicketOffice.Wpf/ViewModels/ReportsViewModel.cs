using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Аналитические отчёты» W-14.
/// Формирует отчёты по загрузке рейсов, выручке направлений, продажам по каналам
/// и статистике возвратов (M10), проверяет право <see cref="SessionPermissions.ViewReports"/>
/// и экспортирует сформированные данные в печатную форму W-10.
/// </summary>
public partial class ReportsViewModel : ObservableObject, IDisposable
{
    private readonly IReportService _reportService;
    private readonly IDocumentService _documentService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;

    #region Права сессии и виртуальное время

    /// <summary>
    /// Признак наличия у текущей сессии права <see cref="SessionPermissions.ViewReports"/>.
    /// </summary>
    public bool CanViewReports { get; }

    /// <summary>
    /// Признак режима отказа в доступе: у сессии нет права просмотра отчётов.
    /// </summary>
    public bool IsAccessDenied => !CanViewReports;

    /// <summary>
    /// Пояснение для баннера отказа в доступе.
    /// </summary>
    public string AccessDeniedMessage =>
        "Недостаточно прав: просмотр аналитических отчётов доступен только администратору. " +
        "Смените роль через кнопку «Сменить роль» в верхней панели.";

    /// <summary>
    /// Текущее виртуальное время ядра для шапки раздела.
    /// </summary>
    [ObservableProperty]
    private string _virtualNowText = string.Empty;

    #endregion

    #region Период отчёта и выбор типа

    /// <summary>
    /// Начальная дата периода отчёта.
    /// </summary>
    [ObservableProperty]
    private DateTime? _fromDate;

    /// <summary>
    /// Конечная дата периода отчёта.
    /// </summary>
    [ObservableProperty]
    private DateTime? _toDate;

    /// <summary>
    /// Индекс выбранного типа отчёта (0 — загрузка рейсов, 1 — выручка, 2 — каналы, 3 — возвраты).
    /// </summary>
    [ObservableProperty]
    private int _selectedReportType;

    /// <summary>
    /// Признак выбранного отчёта по загрузке рейсов.
    /// </summary>
    public bool IsTypeFlightLoad => SelectedReportType == 0;

    /// <summary>
    /// Признак выбранного отчёта по выручке направлений.
    /// </summary>
    public bool IsTypeRouteRevenue => SelectedReportType == 1;

    /// <summary>
    /// Признак выбранного отчёта по продажам в разрезе каналов.
    /// </summary>
    public bool IsTypeChannelSales => SelectedReportType == 2;

    /// <summary>
    /// Признак выбранного отчёта по статистике возвратов.
    /// </summary>
    public bool IsTypeRefundStats => SelectedReportType == 3;

    /// <summary>
    /// Название текущего выбранного отчёта для шапки и печатной формы.
    /// </summary>
    public string ReportTitle => SelectedReportType switch
    {
        0 => "Аналитический отчёт по загрузке рейсов",
        1 => "Аналитический отчёт по выручке по направлениям",
        2 => "Аналитический отчёт по продажам по каналам",
        3 => "Аналитический отчёт по статистике возвратов",
        _ => "Аналитический отчёт"
    };

    /// <summary>
    /// Краткое описание выбранного отчёта.
    /// </summary>
    public string ReportSubtitle => SelectedReportType switch
    {
        0 => "Занятость кресел по рейсам и классам обслуживания за период вылетов.",
        1 => "Валовая выручка, возвраты и чистая выручка в разрезе направлений.",
        2 => "Сравнение канала кассы и онлайн-самообслуживания пассажиров.",
        3 => "Добровольные возвраты и вынужденные возвраты по отмене рейсов.",
        _ => string.Empty
    };

    partial void OnSelectedReportTypeChanged(int value)
    {
        OnPropertyChanged(nameof(IsTypeFlightLoad));
        OnPropertyChanged(nameof(IsTypeRouteRevenue));
        OnPropertyChanged(nameof(IsTypeChannelSales));
        OnPropertyChanged(nameof(IsTypeRefundStats));
        OnPropertyChanged(nameof(ReportTitle));
        OnPropertyChanged(nameof(ReportSubtitle));
    }

    #endregion

    #region Данные сформированных отчётов

    /// <summary>
    /// DTO отчёта по загрузке рейсов, полученный из ядра.
    /// </summary>
    [ObservableProperty]
    private FlightLoadReportDto? _flightLoadReport;

    /// <summary>
    /// DTO отчёта по выручке направлений, полученный из ядра.
    /// </summary>
    [ObservableProperty]
    private RouteRevenueReportDto? _routeRevenueReport;

    /// <summary>
    /// DTO отчёта по продажам в разрезе каналов, полученный из ядра.
    /// </summary>
    [ObservableProperty]
    private ChannelSalesReportDto? _channelSalesReport;

    /// <summary>
    /// DTO отчёта по статистике возвратов, полученный из ядра.
    /// </summary>
    [ObservableProperty]
    private RefundStatisticsReportDto? _refundStatisticsReport;

    /// <summary>
    /// Презентационные строки отчёта по загрузке рейсов.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FlightLoadRowViewModel> _flightLoadRows = [];

    /// <summary>
    /// Презентационные строки отчёта по выручке направлений.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<RouteRevenueRowViewModel> _routeRevenueRows = [];

    /// <summary>
    /// Презентационные строки отчёта по продажам в разрезе каналов.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ChannelSalesRowViewModel> _channelSalesRows = [];

    /// <summary>
    /// Презентационные строки отчёта по статистике возвратов.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<RefundStatisticsRowViewModel> _refundStatisticsRows = [];

    #endregion

    #region Состояние и сообщения

    /// <summary>
    /// Признак наличия данных в сформированном отчёте.
    /// </summary>
    [ObservableProperty]
    private bool _hasReportData;

    /// <summary>
    /// Подпись периода сформированного отчёта.
    /// </summary>
    [ObservableProperty]
    private string _reportPeriodText = "Период не указан";

    /// <summary>
    /// Признак выполнения формирования отчёта.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Информационное сообщение или текст ошибки формирования отчёта.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Текст-подсказка при отсутствии данных за выбранный период.
    /// </summary>
    public string EmptyStateMessage => "За выбранный период данные для отчёта отсутствуют. " +
                                       "Измените период или сдвиньте виртуальное время в пульте эмулятора W-16.";

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ReportsViewModel"/>.
    /// </summary>
    /// <param name="reportService">Сервис аналитической отчётности M10.</param>
    /// <param name="documentService">Сервис формирования печатных форм M15.</param>
    /// <param name="clockService">Сервис виртуального системного времени M12.</param>
    /// <param name="navigationService">Сервис открытия окна предпросмотра W-10.</param>
    /// <param name="sessionService">Сервис текущей сессии для проверки прав M1.</param>
    public ReportsViewModel(
        IReportService reportService,
        IDocumentService documentService,
        IVirtualClockService clockService,
        INavigationService navigationService,
        ISessionService sessionService)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        CanViewReports = _sessionService.HasPermission(SessionPermissions.ViewReports);

        DateTime current = _clockService.CurrentVirtualTime.Date;
        FromDate = current.AddDays(-30);
        ToDate = current.AddDays(1);
        UpdateVirtualNowText(_clockService.CurrentVirtualTime);

        _clockService.TimeChanged += OnVirtualTimeChanged;

        if (CanViewReports)
        {
            GenerateReport();
        }
        else
        {
            StatusMessage = AccessDeniedMessage;
        }
    }

    /// <summary>
    /// Переключает тип отчёта и сразу формирует выбранный отчёт за текущий период.
    /// </summary>
    /// <param name="typeIndex">Индекс типа отчёта (0..3).</param>
    [RelayCommand]
    public void SelectReportType(object? typeIndex)
    {
        if (!CanViewReports || typeIndex is null)
        {
            return;
        }

        if (!int.TryParse(typeIndex.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
            || index is < 0 or > 3)
        {
            return;
        }

        SelectedReportType = index;
        GenerateReport();
    }

    /// <summary>
    /// Устанавливает период «последние N дней» относительно виртуального времени ядра.
    /// </summary>
    /// <param name="daysBack">Количество дней назад; 0 — с начала текущих виртуальных суток.</param>
    [RelayCommand]
    public void SetQuickPeriod(object? daysBack)
    {
        if (!CanViewReports)
        {
            return;
        }

        if (!int.TryParse(daysBack?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int days)
            || days < 0)
        {
            return;
        }

        DateTime today = _clockService.CurrentVirtualTime.Date;
        FromDate = today.AddDays(-days);
        ToDate = today.AddDays(1);
        GenerateReport();
    }

    /// <summary>
    /// Формирует аналитический отчёт выбранного типа за указанный период через <c>IReportService</c>.
    /// </summary>
    [RelayCommand]
    public void GenerateReport()
    {
        StatusMessage = null;

        if (!CanViewReports)
        {
            HasReportData = false;
            StatusMessage = AccessDeniedMessage;
            return;
        }

        if (!TryGetPeriod(out DateTime start, out DateTime end, out string? error))
        {
            HasReportData = false;
            StatusMessage = error;
            return;
        }

        IsLoading = true;

        try
        {
            switch (SelectedReportType)
            {
                case 0:
                {
                    FlightLoadReportDto report = _reportService.GetFlightLoadReport(start, end);
                    FlightLoadReport = report;
                    FlightLoadRows = new ObservableCollection<FlightLoadRowViewModel>(
                        report.Items.Select(item => new FlightLoadRowViewModel(item)));
                    HasReportData = FlightLoadRows.Count > 0;
                    break;
                }

                case 1:
                {
                    RouteRevenueReportDto report = _reportService.GetRevenueByRoutesReport(start, end);
                    RouteRevenueReport = report;
                    RouteRevenueRows = new ObservableCollection<RouteRevenueRowViewModel>(
                        report.Items.Select(item => new RouteRevenueRowViewModel(item)));
                    HasReportData = RouteRevenueRows.Count > 0;
                    break;
                }

                case 2:
                {
                    ChannelSalesReportDto report = _reportService.GetSalesByChannelsReport(start, end);
                    ChannelSalesReport = report;
                    ChannelSalesRows = new ObservableCollection<ChannelSalesRowViewModel>(
                        report.Items.Select(item => new ChannelSalesRowViewModel(item)));
                    HasReportData = ChannelSalesRows.Count > 0;
                    break;
                }

                case 3:
                {
                    RefundStatisticsReportDto report = _reportService.GetRefundsStatisticsReport(start, end);
                    RefundStatisticsReport = report;
                    RefundStatisticsRows = new ObservableCollection<RefundStatisticsRowViewModel>(
                        report.Items.Select(item => new RefundStatisticsRowViewModel(item)));
                    HasReportData = RefundStatisticsRows.Count > 0;
                    break;
                }
            }

            ReportPeriodText = $"Период: {start:dd.MM.yyyy} — {end.Date:dd.MM.yyyy}";

            if (!HasReportData)
            {
                StatusMessage = EmptyStateMessage;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка формирования отчёта: {ex.Message}";
            HasReportData = false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Экспортирует сформированный отчёт в текстовый макет W-10 для предпросмотра и печати.
    /// </summary>
    [RelayCommand]
    public void PrintReport()
    {
        if (!CanViewReports)
        {
            StatusMessage = AccessDeniedMessage;
            return;
        }

        if (!HasReportData)
        {
            StatusMessage = "Сначала сформируйте отчёт с данными за выбранный период.";
            return;
        }

        object? data = SelectedReportType switch
        {
            0 => FlightLoadReport,
            1 => RouteRevenueReport,
            2 => ChannelSalesReport,
            3 => RefundStatisticsReport,
            _ => null
        };

        if (data is null)
        {
            StatusMessage = "Сначала сформируйте отчёт с данными за выбранный период.";
            return;
        }

        try
        {
            PrintDocumentDto document = _documentService.GenerateReportDocument(ReportTitle, data);
            _navigationService.OpenDocumentPreview(document);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось сформировать печатную форму: {ex.Message}";
        }
    }

    private bool TryGetPeriod(out DateTime start, out DateTime end, out string? error)
    {
        start = default;
        end = default;
        error = null;

        if (FromDate is null || ToDate is null)
        {
            error = "Укажите начальную и конечную даты периода отчёта.";
            return false;
        }

        DateTime from = FromDate.Value.Date;
        DateTime to = ToDate.Value.Date;

        if (from > to)
        {
            error = "Начальная дата периода не может быть позже конечной.";
            return false;
        }

        if ((to - from).TotalDays > 3660d)
        {
            error = "Период отчёта не может превышать 10 лет.";
            return false;
        }

        start = from;
        end = to.AddDays(1).AddTicks(-1);
        return true;
    }

    #region Виртуальное время и освобождение ресурсов

    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        RunOnUiThread(() => UpdateVirtualNowText(e.NewVirtualTime));
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
