using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Wpf.Services;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Аналитические отчёты» W-14.
/// Формирует отчёты по загрузке рейсов, выручке направлений, продажам по каналам
/// и статистике возвратов (M10), а также экспортирует их в печатные формы W-10.
/// </summary>
public partial class ReportsViewModel : ObservableObject
{
    private readonly IReportService _reportService;
    private readonly IDocumentService _documentService;
    private readonly IVirtualClockService _clockService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private DateTime _fromDate;

    [ObservableProperty]
    private DateTime _toDate;

    [ObservableProperty]
    private int _selectedReportType;

    public bool IsTypeFlightLoad => SelectedReportType == 0;
    public bool IsTypeRouteRevenue => SelectedReportType == 1;
    public bool IsTypeChannelSales => SelectedReportType == 2;
    public bool IsTypeRefundStats => SelectedReportType == 3;

    partial void OnSelectedReportTypeChanged(int value)
    {
        OnPropertyChanged(nameof(IsTypeFlightLoad));
        OnPropertyChanged(nameof(IsTypeRouteRevenue));
        OnPropertyChanged(nameof(IsTypeChannelSales));
        OnPropertyChanged(nameof(IsTypeRefundStats));
        OnPropertyChanged(nameof(ReportTitle));
    }

    #region Данные сформированных отчётов

    [ObservableProperty]
    private FlightLoadReportDto? _flightLoadReport;

    [ObservableProperty]
    private RouteRevenueReportDto? _routeRevenueReport;

    [ObservableProperty]
    private ChannelSalesReportDto? _channelSalesReport;

    [ObservableProperty]
    private RefundStatisticsReportDto? _refundStatisticsReport;

    [ObservableProperty]
    private bool _hasReportData;

    #endregion

    #region Состояние и сообщения

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    #endregion

    /// <summary>
    /// Название текущего выбранного отчёта.
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
    /// Инициализирует новый экземпляр <see cref="ReportsViewModel"/>.
    /// </summary>
    public ReportsViewModel(
        IReportService reportService,
        IDocumentService documentService,
        IVirtualClockService clockService,
        INavigationService navigationService)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

        DateTime current = _clockService.CurrentVirtualTime.Date;
        FromDate = current.AddDays(-30);
        ToDate = current.AddDays(1);

        GenerateReport();
    }

    /// <summary>
    /// Переключает тип отчёта (0..3).
    /// </summary>
    /// <param name="typeIndex">Индекс типа отчёта.</param>
    [RelayCommand]
    public void SelectReportType(object? typeIndex)
    {
        if (typeIndex != null && int.TryParse(typeIndex.ToString(), out int idx))
        {
            SelectedReportType = idx;
            GenerateReport();
        }
    }

    /// <summary>
    /// Формирует аналитический отчёт выбранного типа за указанный период.
    /// </summary>
    [RelayCommand]
    public void GenerateReport()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            DateTime start = FromDate.Date;
            DateTime end = ToDate.Date.AddDays(1).AddTicks(-1);

            switch (SelectedReportType)
            {
                case 0:
                    FlightLoadReport = _reportService.GetFlightLoadReport(start, end);
                    HasReportData = FlightLoadReport != null && FlightLoadReport.Items.Count > 0;
                    break;
                case 1:
                    RouteRevenueReport = _reportService.GetRevenueByRoutesReport(start, end);
                    HasReportData = RouteRevenueReport != null && RouteRevenueReport.Items.Count > 0;
                    break;
                case 2:
                    ChannelSalesReport = _reportService.GetSalesByChannelsReport(start, end);
                    HasReportData = ChannelSalesReport != null && ChannelSalesReport.Items.Count > 0;
                    break;
                case 3:
                    RefundStatisticsReport = _reportService.GetRefundsStatisticsReport(start, end);
                    HasReportData = RefundStatisticsReport != null && RefundStatisticsReport.Items.Count > 0;
                    break;
            }

            if (!HasReportData)
            {
                StatusMessage = "За выбранный период данные для отчёта отсутствуют.";
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
    /// Экспортирует текущий сформированный отчёт в текстовый макет W-10 для печати и сохранения.
    /// </summary>
    [RelayCommand]
    public void PrintReport()
    {
        object? data = SelectedReportType switch
        {
            0 => (object?)FlightLoadReport,
            1 => RouteRevenueReport,
            2 => ChannelSalesReport,
            3 => RefundStatisticsReport,
            _ => null
        };

        if (data == null)
        {
            StatusMessage = "Сначала сформируйте отчёт с данными.";
            return;
        }

        try
        {
            PrintDocumentDto doc = _documentService.GenerateReportDocument(ReportTitle, data);
            _navigationService.OpenDocumentPreview(doc);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Не удалось сформировать печатную форму: {ex.Message}";
        }
    }
}
