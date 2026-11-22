using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Управление тарифами» W-13.
/// Обеспечивает ведение базовой тарифной сетки направлений (цены эконом-класса и сбора за багаж),
/// создание ставок для новых направлений через <c>ITariffCalculator.SetBaseTariff</c>,
/// интерактивный проверочный калькулятор динамического ценообразования M5
/// и справочное отображение правил возврата и провоза багажа.
/// </summary>
public partial class TariffsAdminViewModel : ObservableObject, IDisposable
{
    private readonly ITariffCalculator _tariffCalculator;
    private readonly IDirectoryService _directoryService;
    private readonly IVirtualClockService _clockService;
    private readonly ISessionService _sessionService;

    private bool _isSynchronizing;

    #region Права сессии и виртуальное время

    /// <summary>
    /// Признак наличия у текущей сессии права <see cref="SessionPermissions.ManageTariffs"/>.
    /// </summary>
    public bool CanManageTariffs { get; }

    /// <summary>
    /// Признак режима только для чтения: у сессии нет права управления тарифами.
    /// </summary>
    public bool IsReadOnlyMode => !CanManageTariffs;

    /// <summary>
    /// Текущее виртуальное время ядра для отображения и расчёта срочности.
    /// </summary>
    [ObservableProperty]
    private string _virtualNowText = string.Empty;

    #endregion

    #region Тарифная сетка и фильтрация

    [ObservableProperty]
    private ObservableCollection<TariffRowViewModel> _tariffs = [];

    [ObservableProperty]
    private ObservableCollection<TariffRowViewModel> _filteredTariffs = [];

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private TariffRowViewModel? _selectedTariff;

    /// <summary>
    /// Признак отсутствия тарифов, подходящих под текстовый фильтр.
    /// </summary>
    public bool HasNoTariffs => FilteredTariffs.Count == 0;

    /// <summary>
    /// Признак наличия тарифов, подходящих под текстовый фильтр.
    /// </summary>
    public bool HasTariffs => !HasNoTariffs;

    #endregion

    #region Форма редактирования тарифа

    /// <summary>
    /// Активные аэропорты для выбора направления.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AirportDto> _airports = [];

    [ObservableProperty]
    private AirportDto? _editOriginAirport;

    [ObservableProperty]
    private AirportDto? _editDestinationAirport;

    [ObservableProperty]
    private string _editBasePriceText = string.Empty;

    [ObservableProperty]
    private string _editBaggageFeeText = "0";

    /// <summary>
    /// Заголовок редактируемого направления с городами и IATA-кодами.
    /// </summary>
    public string EditRouteTitle
    {
        get
        {
            if (EditOriginAirport is null || EditDestinationAirport is null)
            {
                return "Выберите направление тарифа";
            }

            return $"{EditOriginAirport.City} ({EditOriginAirport.Code}) ➔ " +
                   $"{EditDestinationAirport.City} ({EditDestinationAirport.Code})";
        }
    }

    /// <summary>
    /// Пояснение состояния выбранного направления: существующий тариф или новая ставка.
    /// </summary>
    public string EditRouteStatusText
    {
        get
        {
            if (EditOriginAirport is null || EditDestinationAirport is null)
            {
                return "Выберите аэропорт вылета и назначения.";
            }

            if (EditOriginAirport.Id == EditDestinationAirport.Id)
            {
                return "Аэропорты вылета и назначения должны различаться.";
            }

            bool existing = Tariffs.Any(t =>
                t.OriginAirportId == EditOriginAirport.Id &&
                t.DestinationAirportId == EditDestinationAirport.Id);

            return existing
                ? "Действующий тариф направления — изменения применятся к текущей ставке."
                : "Для этого направления тариф не задан — при сохранении будет создана новая ставка.";
        }
    }

    /// <summary>
    /// Справочная стоимость бизнес-класса по введённой базовой цене (× 2.0).
    /// </summary>
    public decimal EditBusinessBasePrice =>
        TryParseMoney(EditBasePriceText, out decimal basePrice)
            ? basePrice * TariffRowViewModel.BusinessClassMultiplier
            : 0m;

    /// <summary>
    /// Справочная стоимость эконом-класса с багажом по введённым значениям.
    /// </summary>
    public decimal EditEconomyWithBaggagePrice =>
        TryParseMoney(EditBasePriceText, out decimal basePrice) &&
        TryParseMoney(EditBaggageFeeText, out decimal baggageFee)
            ? basePrice + baggageFee
            : 0m;

    /// <summary>
    /// Справочная стоимость бизнес-класса с багажом по введённым значениям.
    /// </summary>
    public decimal EditBusinessWithBaggagePrice =>
        TryParseMoney(EditBasePriceText, out decimal basePrice) &&
        TryParseMoney(EditBaggageFeeText, out decimal baggageFee)
            ? (basePrice * TariffRowViewModel.BusinessClassMultiplier) + baggageFee
            : 0m;

    /// <summary>
    /// Признак возможности сохранить тариф: есть право, выбран корректный маршрут и валидны суммы.
    /// </summary>
    public bool CanSaveTariff =>
        CanManageTariffs
        && EditOriginAirport is not null
        && EditDestinationAirport is not null
        && EditOriginAirport.Id != EditDestinationAirport.Id
        && TryParseMoney(EditBasePriceText, out decimal basePrice)
        && basePrice > 0m
        && TryParseMoney(EditBaggageFeeText, out decimal baggageFee)
        && baggageFee >= 0m;

    /// <summary>
    /// Подпись справочной бизнес-ставки в форме редактирования.
    /// </summary>
    public string EditBusinessBasePriceText => $"Бизнес-ставка: {EditBusinessBasePrice:N0} ₽";

    /// <summary>
    /// Подпись справочной стоимости эконом-класса с багажом.
    /// </summary>
    public string EditEconomyWithBaggageText => $"Эконом + багаж: {EditEconomyWithBaggagePrice:N0} ₽";

    /// <summary>
    /// Подпись справочной стоимости бизнес-класса с багажом.
    /// </summary>
    public string EditBusinessWithBaggageText => $"Бизнес + багаж: {EditBusinessWithBaggagePrice:N0} ₽";

    #endregion

    #region Интерактивный калькулятор M5

    [ObservableProperty]
    private AirportDto? _calcOriginAirport;

    [ObservableProperty]
    private AirportDto? _calcDestinationAirport;

    [ObservableProperty]
    private SeatClass _calcSeatClass = SeatClass.Economy;

    [ObservableProperty]
    private DateTime _calcDepartureDate;

    [ObservableProperty]
    private bool _calcHasBaggage;

    [ObservableProperty]
    private decimal _calcBasePrice;

    [ObservableProperty]
    private decimal _calcClassMultiplier = 1.0m;

    [ObservableProperty]
    private double _calcDaysUntilDeparture;

    [ObservableProperty]
    private decimal _calcDaysMultiplier = 1.0m;

    [ObservableProperty]
    private decimal _calcBaggageCost;

    [ObservableProperty]
    private decimal _calculatedFinalPrice;

    [ObservableProperty]
    private string _formulaExplanation = string.Empty;

    /// <summary>
    /// Русскоязычная подпись выбранного класса обслуживания в калькуляторе.
    /// </summary>
    public string CalcSeatClassText => CalcSeatClass == SeatClass.Business ? "Бизнес-класс" : "Эконом-класс";

    /// <summary>
    /// Подпись кнопки выбора эконом-класса с коэффициентом ядра.
    /// </summary>
    public string EconomyClassButtonText => "Эконом (× 1,0)";

    /// <summary>
    /// Подпись кнопки выбора бизнес-класса с коэффициентом ядра.
    /// </summary>
    public string BusinessClassButtonText => "Бизнес (× 2,0)";

    #endregion

    #region Справочные правила возврата и багажа

    /// <summary>
    /// Правила возврата билетов, заданные бизнес-логикой ядра M9 (только для чтения).
    /// </summary>
    public ObservableCollection<TariffRuleViewModel> RefundRules { get; }

    /// <summary>
    /// Правила тарификации багажа и классов обслуживания, заданные ядром M5 (только для чтения).
    /// </summary>
    public ObservableCollection<TariffRuleViewModel> BaggageRules { get; }

    #endregion

    #region Состояние и сообщения

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isLoading;

    #endregion

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TariffsAdminViewModel"/>.
    /// </summary>
    /// <param name="tariffCalculator">Калькулятор тарифов и тарифная сетка M5.</param>
    /// <param name="directoryService">Сервис справочника аэропортов M3.</param>
    /// <param name="clockService">Сервис виртуального системного времени M12.</param>
    /// <param name="sessionService">Сервис текущей сессии для проверки прав M1.</param>
    public TariffsAdminViewModel(
        ITariffCalculator tariffCalculator,
        IDirectoryService directoryService,
        IVirtualClockService clockService,
        ISessionService sessionService)
    {
        _tariffCalculator = tariffCalculator ?? throw new ArgumentNullException(nameof(tariffCalculator));
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));

        CanManageTariffs = _sessionService.HasPermission(SessionPermissions.ManageTariffs);

        RefundRules =
        [
            new TariffRuleViewModel(
                "Вынужденный возврат",
                "Отмена рейса авиакомпанией, в том числе массовая отмена рейса.",
                "100% без удержаний"),
            new TariffRuleViewModel(
                "Добровольный возврат ≥ 24 часов",
                "До вылета остаётся 24 часа и более.",
                "90% возврата, штраф 10%"),
            new TariffRuleViewModel(
                "Добровольный возврат < 24 часов",
                "До вылета остаётся менее 24 часов.",
                "50% возврата, штраф 50%"),
            new TariffRuleViewModel(
                "После вылета",
                "Рейс уже вылетел или билет использован.",
                "Возврат не выполняется")
        ];

        BaggageRules =
        [
            new TariffRuleViewModel(
                "Ручная кладь",
                "Включена в базовую стоимость билета эконом-класса.",
                "0 ₽"),
            new TariffRuleViewModel(
                "Багаж 23 кг",
                "Оплачивается отдельно по сбору за багаж BaggageFee выбранного направления.",
                "по тарифу"),
            new TariffRuleViewModel(
                "Бизнес-класс",
                "Множитель стоимости билета относительно базовой ставки эконома.",
                "× 2,0"),
            new TariffRuleViewModel(
                "Срочность покупки",
                "Более 30 дней — ×1,0; 15–30 дней — ×1,2; 7–15 дней — ×1,5; менее 7 дней — ×1,8.",
                "динамический")
        ];

        CalcDepartureDate = _clockService.CurrentVirtualTime.Date.AddDays(10);
        UpdateVirtualNowText(_clockService.CurrentVirtualTime);

        _clockService.TimeChanged += OnVirtualTimeChanged;

        RefreshData();
    }

    /// <summary>
    /// Перезагружает тарифную сетку и справочник аэропортов.
    /// </summary>
    [RelayCommand]
    public void RefreshData()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            LoadData();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки тарифов: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Загружает тарифы и активные аэропорты, сохраняя выбранное направление.
    /// </summary>
    private void LoadData()
    {
        UpdateVirtualNowText(_clockService.CurrentVirtualTime);

        IReadOnlyList<AirportDto> allAirports = _directoryService.GetAllAirports();
        Dictionary<int, AirportDto> airportLookup = allAirports.ToDictionary(a => a.Id);
        List<AirportDto> activeAirports = allAirports.Where(a => a.IsActive).ToList();

        int editOriginId = EditOriginAirport?.Id ?? 0;
        int editDestinationId = EditDestinationAirport?.Id ?? 0;
        int calcOriginId = CalcOriginAirport?.Id ?? 0;
        int calcDestinationId = CalcDestinationAirport?.Id ?? 0;
        int selectedTariffId = SelectedTariff?.Id ?? 0;
        bool isInitialEditRoute = EditOriginAirport is null && EditDestinationAirport is null;

        Airports = new ObservableCollection<AirportDto>(activeAirports);

        IReadOnlyList<TariffDto> tariffs = _tariffCalculator.GetAllTariffs();
        Tariffs = new ObservableCollection<TariffRowViewModel>(
            tariffs.Select(t => new TariffRowViewModel(
                t,
                airportLookup.GetValueOrDefault(t.OriginAirportId),
                airportLookup.GetValueOrDefault(t.DestinationAirportId))));

        ApplyFilter();

        _isSynchronizing = true;

        EditOriginAirport = activeAirports.FirstOrDefault(a => a.Id == editOriginId)
            ?? activeAirports.FirstOrDefault();
        EditDestinationAirport = activeAirports.FirstOrDefault(a => a.Id == editDestinationId)
            ?? activeAirports.FirstOrDefault(a => a.Id != EditOriginAirport?.Id);

        CalcOriginAirport = activeAirports.FirstOrDefault(a => a.Id == calcOriginId)
            ?? activeAirports.FirstOrDefault();
        CalcDestinationAirport = activeAirports.FirstOrDefault(a => a.Id == calcDestinationId)
            ?? activeAirports.FirstOrDefault(a => a.Id != CalcOriginAirport?.Id);

        _isSynchronizing = false;

        TariffRowViewModel? selected = Tariffs.FirstOrDefault(t => t.Id == selectedTariffId);
        if (selected is null && selectedTariffId == 0 && isInitialEditRoute)
        {
            selected = FilteredTariffs.FirstOrDefault() ?? Tariffs.FirstOrDefault();
        }

        if (selected is not null)
        {
            SelectedTariff = selected;
            ApplyTariffToEditForm(selected);
        }
        else
        {
            SelectedTariff = null;
            SynchronizeEditFormWithRoute();
        }

        RecalculatePrice();
    }

    /// <summary>
    /// Применяет текстовый фильтр к тарифной сетке.
    /// </summary>
    public void ApplyFilter()
    {
        if (_isSynchronizing)
        {
            return;
        }

        IEnumerable<TariffRowViewModel> query = Tariffs;
        string needle = FilterText.Trim().ToUpperInvariant();

        if (needle.Length > 0)
        {
            query = query.Where(t => t.MatchesSearch(needle));
        }

        FilteredTariffs = new ObservableCollection<TariffRowViewModel>(query);
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    partial void OnFilteredTariffsChanged(ObservableCollection<TariffRowViewModel> value)
    {
        OnPropertyChanged(nameof(HasNoTariffs));
        OnPropertyChanged(nameof(HasTariffs));
    }

    partial void OnSelectedTariffChanged(TariffRowViewModel? value)
    {
        if (_isSynchronizing || value is null)
        {
            return;
        }

        ApplyTariffToEditForm(value);
    }

    partial void OnEditOriginAirportChanged(AirportDto? value)
    {
        if (!_isSynchronizing)
        {
            SynchronizeEditFormWithRoute();
        }
    }

    partial void OnEditDestinationAirportChanged(AirportDto? value)
    {
        if (!_isSynchronizing)
        {
            SynchronizeEditFormWithRoute();
        }
    }

    partial void OnEditBasePriceTextChanged(string value)
    {
        if (!_isSynchronizing)
        {
            NotifyEditStateChanged();
        }
    }

    partial void OnEditBaggageFeeTextChanged(string value)
    {
        if (!_isSynchronizing)
        {
            NotifyEditStateChanged();
        }
    }

    /// <summary>
    /// Выбирает тариф из таблицы и загружает его значения в форму редактирования.
    /// </summary>
    /// <param name="row">Выбранная строка тарифной сетки.</param>
    [RelayCommand]
    public void SelectTariff(TariffRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (ReferenceEquals(SelectedTariff, row))
        {
            ApplyTariffToEditForm(row);
            return;
        }

        SelectedTariff = row;
    }

    /// <summary>
    /// Сбрасывает текстовый фильтр тарифной сетки.
    /// </summary>
    [RelayCommand]
    public void ResetFilters()
    {
        FilterText = string.Empty;
        ApplyFilter();
    }

    /// <summary>
    /// Синхронизирует форму редактирования с выбранным направлением:
    /// подставляет действующие значения либо готовит форму к созданию новой ставки.
    /// </summary>
    private void SynchronizeEditFormWithRoute()
    {
        TariffRowViewModel? existing = null;

        if (EditOriginAirport is not null &&
            EditDestinationAirport is not null &&
            EditOriginAirport.Id != EditDestinationAirport.Id)
        {
            existing = Tariffs.FirstOrDefault(t =>
                t.OriginAirportId == EditOriginAirport.Id &&
                t.DestinationAirportId == EditDestinationAirport.Id);
        }

        _isSynchronizing = true;
        SelectedTariff = existing;

        if (existing is not null)
        {
            EditBasePriceText = FormatMoney(existing.BasePrice);
            EditBaggageFeeText = FormatMoney(existing.BaggageFee);
        }
        else
        {
            EditBasePriceText = string.Empty;
            EditBaggageFeeText = "0";
        }

        CalcOriginAirport = EditOriginAirport;
        CalcDestinationAirport = EditDestinationAirport;
        _isSynchronizing = false;

        NotifyEditStateChanged();
        RecalculatePrice();
    }

    /// <summary>
    /// Переносит значения тарифа в поля формы редактирования без запуска рекурсивных синхронизаций.
    /// </summary>
    /// <param name="row">Строка тарифной сетки.</param>
    private void ApplyTariffToEditForm(TariffRowViewModel row)
    {
        _isSynchronizing = true;

        EditOriginAirport = Airports.FirstOrDefault(a => a.Id == row.OriginAirportId);
        EditDestinationAirport = Airports.FirstOrDefault(a => a.Id == row.DestinationAirportId);
        EditBasePriceText = FormatMoney(row.BasePrice);
        EditBaggageFeeText = FormatMoney(row.BaggageFee);

        _isSynchronizing = false;

        CalcOriginAirport = EditOriginAirport;
        CalcDestinationAirport = EditDestinationAirport;

        NotifyEditStateChanged();
        RecalculatePrice();
    }

    /// <summary>
    /// Уведомляет представление об изменении вычисляемых полей формы тарифа.
    /// </summary>
    private void NotifyEditStateChanged()
    {
        OnPropertyChanged(nameof(EditRouteTitle));
        OnPropertyChanged(nameof(EditRouteStatusText));
        OnPropertyChanged(nameof(EditBusinessBasePrice));
        OnPropertyChanged(nameof(EditEconomyWithBaggagePrice));
        OnPropertyChanged(nameof(EditBusinessWithBaggagePrice));
        OnPropertyChanged(nameof(CanSaveTariff));
        OnPropertyChanged(nameof(EditBusinessBasePriceText));
        OnPropertyChanged(nameof(EditEconomyWithBaggageText));
        OnPropertyChanged(nameof(EditBusinessWithBaggageText));
    }

    #region Сохранение тарифа

    /// <summary>
    /// Сохраняет базовую цену и сбор за багаж для выбранного направления.
    /// Для нового направления ядро создаёт ставку через <c>SetBaseTariff</c>.
    /// </summary>
    [RelayCommand]
    public void SaveTariff()
    {
        if (!EnsureManagePermission("изменение тарифов"))
        {
            return;
        }

        StatusMessage = null;

        if (EditOriginAirport is null || EditDestinationAirport is null)
        {
            StatusMessage = "Выберите аэропорты вылета и назначения.";
            return;
        }

        if (EditOriginAirport.Id == EditDestinationAirport.Id)
        {
            StatusMessage = "Аэропорты вылета и назначения должны различаться.";
            return;
        }

        if (!TryParseMoney(EditBasePriceText, out decimal basePrice))
        {
            StatusMessage = "Введите базовую цену числом (например, 7500 или 7500,50).";
            return;
        }

        if (basePrice <= 0m)
        {
            StatusMessage = "Базовая цена тарифа должна быть строго больше нуля.";
            return;
        }

        if (!TryParseMoney(EditBaggageFeeText, out decimal baggageFee))
        {
            StatusMessage = "Введите сбор за багаж числом (например, 1200 или 0).";
            return;
        }

        if (baggageFee < 0m)
        {
            StatusMessage = "Стоимость багажа не может быть отрицательной.";
            return;
        }

        int originId = EditOriginAirport.Id;
        int destinationId = EditDestinationAirport.Id;
        string route = $"{EditOriginAirport.Code} ➔ {EditDestinationAirport.Code}";

        Result result = _tariffCalculator.SetBaseTariff(originId, destinationId, basePrice, baggageFee);
        if (result.IsFailure)
        {
            StatusMessage = $"Ошибка сохранения тарифа: {result.ErrorMessage}";
            return;
        }

        RefreshData();

        TariffRowViewModel? saved = Tariffs.FirstOrDefault(t =>
            t.OriginAirportId == originId && t.DestinationAirportId == destinationId);
        if (saved is not null)
        {
            SelectTariff(saved);
        }

        StatusMessage =
            $"Тариф {route} сохранён: базовая цена {basePrice:N2} ₽, сбор за багаж {baggageFee:N2} ₽.";
        RecalculatePrice();
    }

    #endregion

    #region Интерактивный калькулятор

    /// <summary>
    /// Выбирает эконом-класс в проверочном калькуляторе.
    /// </summary>
    [RelayCommand]
    public void SelectEconomyClass() => CalcSeatClass = SeatClass.Economy;

    /// <summary>
    /// Выбирает бизнес-класс в проверочном калькуляторе.
    /// </summary>
    [RelayCommand]
    public void SelectBusinessClass() => CalcSeatClass = SeatClass.Business;

    partial void OnCalcOriginAirportChanged(AirportDto? value) => RecalculatePrice();

    partial void OnCalcDestinationAirportChanged(AirportDto? value) => RecalculatePrice();

    partial void OnCalcSeatClassChanged(SeatClass value)
    {
        OnPropertyChanged(nameof(CalcSeatClassText));
        RecalculatePrice();
    }

    partial void OnCalcDepartureDateChanged(DateTime value) => RecalculatePrice();

    partial void OnCalcHasBaggageChanged(bool value) => RecalculatePrice();

    /// <summary>
    /// Выполняет расчёт итоговой цены через ядро <c>ITariffCalculator</c>
    /// и формирует справочный разбор формулы ценообразования M5.
    /// </summary>
    public void RecalculatePrice()
    {
        if (_isSynchronizing)
        {
            return;
        }

        if (CalcOriginAirport is null || CalcDestinationAirport is null)
        {
            FormulaExplanation = "Выберите аэропорт вылета и назначения для расчёта.";
            CalculatedFinalPrice = 0m;
            return;
        }

        if (CalcOriginAirport.Id == CalcDestinationAirport.Id)
        {
            FormulaExplanation = "Аэропорты вылета и назначения должны различаться.";
            CalculatedFinalPrice = 0m;
            return;
        }

        TariffRowViewModel? tariff = Tariffs.FirstOrDefault(t =>
            t.OriginAirportId == CalcOriginAirport.Id &&
            t.DestinationAirportId == CalcDestinationAirport.Id);

        if (tariff is null)
        {
            FormulaExplanation =
                $"Базовый тариф для маршрута {CalcOriginAirport.Code} ➔ {CalcDestinationAirport.Code} не задан. " +
                "Сохраните ставку в левой панели, чтобы проверить расчёт.";
            CalculatedFinalPrice = 0m;
            return;
        }

        DateTime departure = CalcDepartureDate.Date.AddHours(12);
        DateTime virtualNow = _clockService.CurrentVirtualTime;

        if (departure <= virtualNow)
        {
            FormulaExplanation =
                $"Дата вылета должна быть позже текущего виртуального времени ({virtualNow:dd.MM.yyyy HH:mm}).";
            CalculatedFinalPrice = 0m;
            return;
        }

        CalcBasePrice = tariff.BasePrice;
        CalcClassMultiplier = CalcSeatClass == SeatClass.Business
            ? TariffRowViewModel.BusinessClassMultiplier
            : 1.0m;
        CalcDaysUntilDeparture = (departure - virtualNow).TotalDays;

        if (CalcDaysUntilDeparture > 30.0)
        {
            CalcDaysMultiplier = 1.0m;
        }
        else if (CalcDaysUntilDeparture >= 15.0)
        {
            CalcDaysMultiplier = 1.2m;
        }
        else if (CalcDaysUntilDeparture >= 7.0)
        {
            CalcDaysMultiplier = 1.5m;
        }
        else
        {
            CalcDaysMultiplier = 1.8m;
        }

        CalcBaggageCost = CalcHasBaggage ? tariff.BaggageFee : 0m;

        try
        {
            CalculatedFinalPrice = _tariffCalculator.CalculateTicketPrice(
                CalcOriginAirport.Id,
                CalcDestinationAirport.Id,
                CalcSeatClass,
                departure,
                CalcHasBaggage);

            string baggageText = CalcHasBaggage
                ? $"багаж +{CalcBaggageCost:N0} ₽"
                : "без багажа";

            FormulaExplanation =
                "Итог = базовая цена × класс × срочность + багаж\n" +
                $"{CalcBasePrice:N2} ₽ × {CalcClassMultiplier:F1} × {CalcDaysMultiplier:F1} + {CalcBaggageCost:N2} ₽ = {CalculatedFinalPrice:N2} ₽\n" +
                $"Класс: {CalcSeatClassText}; дней до вылета: {CalcDaysUntilDeparture:F1}; {baggageText}.";
        }
        catch (Exception ex)
        {
            FormulaExplanation = $"Ошибка расчёта тарифа: {ex.Message}";
            CalculatedFinalPrice = 0m;
        }
    }

    #endregion

    #region Виртуальное время и служебные методы

    private void OnVirtualTimeChanged(object? sender, VirtualTimeChangedEventArgs e)
    {
        RunOnUiThread(() =>
        {
            UpdateVirtualNowText(e.NewVirtualTime);
            RecalculatePrice();
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

    private bool EnsureManagePermission(string actionDescription)
    {
        if (CanManageTariffs)
        {
            return true;
        }

        StatusMessage = $"Недостаточно прав: {actionDescription} доступно только администратору.";
        return false;
    }

    private static string FormatMoney(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static bool TryParseMoney(string? text, out decimal value)
    {
        value = 0m;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string normalized = text.Trim()
            .Replace("₽", string.Empty, StringComparison.Ordinal)
            .Replace("руб.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal);

        const NumberStyles styles = NumberStyles.Number | NumberStyles.AllowCurrencySymbol;
        return decimal.TryParse(normalized, styles, CultureInfo.CurrentCulture, out value)
            || decimal.TryParse(normalized, styles, CultureInfo.InvariantCulture, out value);
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
