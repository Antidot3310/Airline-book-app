using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления раздела «Управление тарифами» W-13.
/// Включает таблицу базовых тарифов направлений с быстрым изменением цен
/// и интерактивный проверочный калькулятор с разбором формулы динамического ценообразования (M5).
/// </summary>
public partial class TariffsAdminViewModel : ObservableObject
{
    private readonly ITariffCalculator _tariffCalculator;
    private readonly IDirectoryService _directoryService;
    private readonly IVirtualClockService _clockService;

    #region Левая панель: Таблица тарифов и редактирование

    [ObservableProperty]
    private ObservableCollection<TariffDto> _tariffs = [];

    [ObservableProperty]
    private TariffDto? _selectedTariff;

    [ObservableProperty]
    private decimal _editBasePrice;

    [ObservableProperty]
    private decimal _editBaggageFee;

    #endregion

    #region Правая панель: Проверочный калькулятор

    [ObservableProperty]
    private ObservableCollection<AirportDto> _airports = [];

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

    // Вычисленные поля разбора формулы
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
    public TariffsAdminViewModel(
        ITariffCalculator tariffCalculator,
        IDirectoryService directoryService,
        IVirtualClockService clockService)
    {
        _tariffCalculator = tariffCalculator ?? throw new ArgumentNullException(nameof(tariffCalculator));
        _directoryService = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));

        CalcDepartureDate = _clockService.CurrentVirtualTime.Date.AddDays(10);

        RefreshData();
    }

    /// <summary>
    /// Загружает список тарифов и аэропортов.
    /// </summary>
    [RelayCommand]
    public void RefreshData()
    {
        IsLoading = true;
        StatusMessage = null;

        try
        {
            IReadOnlyList<TariffDto> list = _tariffCalculator.GetAllTariffs();
            Tariffs = new ObservableCollection<TariffDto>(list);

            IReadOnlyList<AirportDto> airportList = _directoryService.GetAllAirports().Where(a => a.IsActive).ToList();
            Airports = new ObservableCollection<AirportDto>(airportList);

            if (SelectedTariff == null && Tariffs.Count > 0)
            {
                SelectTariff(Tariffs[0]);
            }

            if (CalcOriginAirport == null && Airports.Count > 0)
            {
                CalcOriginAirport = Airports[0];
            }

            if (CalcDestinationAirport == null && Airports.Count > 1)
            {
                CalcDestinationAirport = Airports[1];
            }

            RecalculatePrice();
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
    /// Выбирает тариф для редактирования.
    /// </summary>
    /// <param name="tariff">Выбранный тариф.</param>
    [RelayCommand]
    public void SelectTariff(TariffDto? tariff)
    {
        SelectedTariff = tariff;
        if (tariff != null)
        {
            EditBasePrice = tariff.BasePrice;
            EditBaggageFee = tariff.BaggageFee;

            // Синхронизируем калькулятор с выбранным маршрутом
            CalcOriginAirport = Airports.FirstOrDefault(a => a.Id == tariff.OriginAirportId) ?? CalcOriginAirport;
            CalcDestinationAirport = Airports.FirstOrDefault(a => a.Id == tariff.DestinationAirportId) ?? CalcDestinationAirport;
            RecalculatePrice();
        }
    }

    /// <summary>
    /// Сохраняет обновлённые параметры базовой цены и сбора за багаж.
    /// </summary>
    [RelayCommand]
    public void SaveTariff()
    {
        if (SelectedTariff == null)
        {
            return;
        }

        StatusMessage = null;

        if (EditBasePrice <= 0)
        {
            StatusMessage = "Базовая цена тарифа должна быть строго больше нуля.";
            return;
        }

        if (EditBaggageFee < 0)
        {
            StatusMessage = "Стоимость багажа не может быть отрицательной.";
            return;
        }

        Result result = _tariffCalculator.SetBaseTariff(
            SelectedTariff.OriginAirportId,
            SelectedTariff.DestinationAirportId,
            EditBasePrice,
            EditBaggageFee);

        if (result.IsFailure)
        {
            StatusMessage = $"Ошибка сохранения тарифа: {result.ErrorMessage}";
            return;
        }

        StatusMessage = $"Тариф для маршрута {SelectedTariff.OriginAirportCode} ➔ {SelectedTariff.DestinationAirportCode} успешно обновлён!";

        int selectedId = SelectedTariff.Id;
        Tariffs = new ObservableCollection<TariffDto>(_tariffCalculator.GetAllTariffs());
        SelectedTariff = Tariffs.FirstOrDefault(t => t.Id == selectedId);

        RecalculatePrice();
    }

    #region Интерактивный калькулятор

    partial void OnCalcOriginAirportChanged(AirportDto? value) => RecalculatePrice();
    partial void OnCalcDestinationAirportChanged(AirportDto? value) => RecalculatePrice();
    partial void OnCalcSeatClassChanged(SeatClass value) => RecalculatePrice();
    partial void OnCalcDepartureDateChanged(DateTime value) => RecalculatePrice();
    partial void OnCalcHasBaggageChanged(bool value) => RecalculatePrice();

    /// <summary>
    /// Переключает класс места в калькуляторе на эконом.
    /// </summary>
    [RelayCommand]
    public void SelectEconomyClass()
    {
        CalcSeatClass = SeatClass.Economy;
    }

    /// <summary>
    /// Переключает класс места в калькуляторе на бизнес.
    /// </summary>
    [RelayCommand]
    public void SelectBusinessClass()
    {
        CalcSeatClass = SeatClass.Business;
    }

    /// <summary>
    /// Вычисляет итоговую стоимость и детальный разбор формулы динамического тарифа.
    /// </summary>
    public void RecalculatePrice()
    {
        if (CalcOriginAirport == null || CalcDestinationAirport == null)
        {
            FormulaExplanation = "Выберите пункт вылета и назначения.";
            CalculatedFinalPrice = 0m;
            return;
        }

        if (CalcOriginAirport.Id == CalcDestinationAirport.Id)
        {
            FormulaExplanation = "Аэропорты вылета и назначения должны различаться.";
            CalculatedFinalPrice = 0m;
            return;
        }

        TariffDto? tariff = Tariffs.FirstOrDefault(t =>
            t.OriginAirportId == CalcOriginAirport.Id &&
            t.DestinationAirportId == CalcDestinationAirport.Id);

        if (tariff == null)
        {
            FormulaExplanation = $"Базовый тариф для маршрута {CalcOriginAirport.Code} ➔ {CalcDestinationAirport.Code} не зарегистрирован.";
            CalculatedFinalPrice = 0m;
            return;
        }

        CalcBasePrice = tariff.BasePrice;
        CalcClassMultiplier = CalcSeatClass == SeatClass.Business ? 2.0m : 1.0m;

        DateTime depTime = CalcDepartureDate.Date.AddHours(12);
        CalcDaysUntilDeparture = Math.Max(0.0, (depTime - _clockService.CurrentVirtualTime).TotalDays);

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
                depTime,
                CalcHasBaggage);

            string className = CalcSeatClass == SeatClass.Business ? "Бизнес" : "Эконом";
            string baggageText = CalcHasBaggage ? $"+ {CalcBaggageCost:N0} ₽ (багаж)" : "+ 0 ₽ (без багажа)";

            FormulaExplanation =
                $"Итог = Базовый тариф × Класс ({className}: {CalcClassMultiplier:F1}) × Срочность ({CalcDaysUntilDeparture:F1} дн.: {CalcDaysMultiplier:F1}) {baggageText}\n" +
                $"Расчёт: {CalcBasePrice:N0} × {CalcClassMultiplier:F1} × {CalcDaysMultiplier:F1} + {CalcBaggageCost:N0} = {CalculatedFinalPrice:N0} ₽";
        }
        catch (Exception ex)
        {
            FormulaExplanation = $"Ошибка расчёта: {ex.Message}";
            CalculatedFinalPrice = 0m;
        }
    }

    #endregion
}
