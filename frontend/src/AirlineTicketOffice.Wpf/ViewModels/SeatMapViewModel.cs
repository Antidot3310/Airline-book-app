using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AirlineTicketOffice.Core.Contracts.Booking;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления интерактивной схемы мест воздушного судна W-04.
/// Обеспечивает отображение салона, выбор кресел по количеству пассажиров,
/// расчёт суммы и отправку команды удержания мест в сервис бронирования (M6).
/// </summary>
public partial class SeatMapViewModel : ObservableObject
{
    private readonly IBookingService _bookingService;
    private readonly IVirtualClockService _clockService;

    [ObservableProperty]
    private int _flightId;

    [ObservableProperty]
    private string _flightNumber = string.Empty;

    [ObservableProperty]
    private string _aircraftModel = string.Empty;

    [ObservableProperty]
    private string _routeInfo = string.Empty;

    [ObservableProperty]
    private string _departureTimeText = string.Empty;

    [ObservableProperty]
    private int _requiredPassengersCount = 1;

    [ObservableProperty]
    private ObservableCollection<SeatItemViewModel> _seats = [];

    [ObservableProperty]
    private ObservableCollection<SeatRowViewModel> _rows = [];

    [ObservableProperty]
    private ObservableCollection<SeatItemViewModel> _selectedSeats = [];

    [ObservableProperty]
    private string _selectedSeatsSummary = string.Empty;

    [ObservableProperty]
    private decimal _totalPrice;

    [ObservableProperty]
    private bool _canProceed;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isHolding;

    /// <summary>
    /// Событие запроса возврата к поиску рейсов (W-03).
    /// </summary>
    public event Action? BackRequested;

    /// <summary>
    /// Событие успешного удержания мест для перехода к оформлению пассажиров (W-05).
    /// </summary>
    public event Action<HoldSeatsResultDto>? SeatsHeld;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SeatMapViewModel"/>.
    /// </summary>
    /// <param name="bookingService">Сервис управления бронированием и картой мест.</param>
    /// <param name="clockService">Сервис виртуального времени.</param>
    public SeatMapViewModel(
        IBookingService bookingService,
        IVirtualClockService clockService)
    {
        _bookingService = bookingService ?? throw new ArgumentNullException(nameof(bookingService));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
    }

    /// <summary>
    /// Инициализирует схему салона для выбранного рейса и заданного числа пассажиров.
    /// </summary>
    /// <param name="flight">Выбранный рейс из расписания.</param>
    /// <param name="passengersCount">Количество пассажиров, для которых необходимо выбрать места.</param>
    public void Initialize(FlightListItemDto flight, int passengersCount)
    {
        if (flight == null)
        {
            throw new ArgumentNullException(nameof(flight));
        }

        FlightId = flight.Id;
        FlightNumber = flight.FlightNumber;
        RouteInfo = flight.Route;
        DepartureTimeText = flight.DepartureTime.ToString("dd.MM.yyyy HH:mm");
        RequiredPassengersCount = Math.Clamp(passengersCount, 1, 5);

        SelectedSeats.Clear();
        StatusMessage = null;

        LoadSeatMap();
        UpdateSelectedSeatsSummary();
    }

    /// <summary>
    /// Загружает актуальную карту мест из сервиса бронирования.
    /// </summary>
    public void LoadSeatMap()
    {
        Result<SeatMapDto> mapResult = _bookingService.GetSeatMap(FlightId);

        if (mapResult.IsFailure)
        {
            StatusMessage = $"Не удалось загрузить схему салона: {mapResult.ErrorMessage}";
            return;
        }

        SeatMapDto map = mapResult.Value;
        AircraftModel = map.AircraftModel;

        var seatList = new List<SeatItemViewModel>();

        foreach (SeatItemDto item in map.Seats)
        {
            var (row, letter) = ParseSeatNumber(item.SeatNumber);

            var seatVm = new SeatItemViewModel
            {
                SeatId = item.SeatId,
                SeatNumber = item.SeatNumber,
                RowNumber = row,
                Letter = letter,
                Class = item.Class,
                BaseState = item.State,
                Price = item.Price,
                IsSelected = false
            };

            seatList.Add(seatVm);
        }

        Seats = new ObservableCollection<SeatItemViewModel>(seatList);

        // Формируем сгруппированные ряды с проходом по центру
        BuildRows(seatList);
    }

    /// <summary>
    /// Обновляет схему салона, сохраняя ранее выбранные места (если они всё ещё свободны).
    /// </summary>
    public void RefreshSeatMap()
    {
        HashSet<string> previouslySelected = SelectedSeats.Select(s => s.SeatNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        LoadSeatMap();

        SelectedSeats.Clear();

        foreach (SeatItemViewModel seat in Seats)
        {
            if (previouslySelected.Contains(seat.SeatNumber) && seat.IsSelectable)
            {
                seat.IsSelected = true;
                SelectedSeats.Add(seat);
            }
        }

        UpdateSelectedSeatsSummary();
    }

    /// <summary>
    /// Переключает состояние выбора конкретного посадочного места при клике.
    /// </summary>
    /// <param name="seat">Выбранное место.</param>
    [RelayCommand]
    public void ToggleSeat(SeatItemViewModel? seat)
    {
        if (seat == null || !seat.IsSelectable)
        {
            return;
        }

        if (seat.IsSelected)
        {
            seat.IsSelected = false;
            SelectedSeats.Remove(seat);
            StatusMessage = null;
        }
        else
        {
            if (SelectedSeats.Count < RequiredPassengersCount)
            {
                seat.IsSelected = true;
                SelectedSeats.Add(seat);
                StatusMessage = null;
            }
            else
            {
                StatusMessage = $"Уже выбрано максимальное количество мест: {RequiredPassengersCount}. Для выбора другого места сначала снимите выбор с одного из текущих.";
                return;
            }
        }

        UpdateSelectedSeatsSummary();
    }

    /// <summary>
    /// Выполняет временное удержание выбранных мест в ядре системы на 15 минут.
    /// </summary>
    [RelayCommand]
    public void HoldSelectedSeats()
    {
        if (!CanProceed)
        {
            StatusMessage = $"Необходимо выбрать ровно {RequiredPassengersCount} мест.";
            return;
        }

        IsHolding = true;
        StatusMessage = "Выполняется удержание мест в системе...";

        List<string> seatNumbers = SelectedSeats
            .OrderBy(s => s.RowNumber)
            .ThenBy(s => s.Letter, StringComparer.OrdinalIgnoreCase)
            .Select(s => s.SeatNumber)
            .ToList();

        Result<HoldSeatsResultDto> holdResult = _bookingService.HoldSeats(FlightId, seatNumbers);

        IsHolding = false;

        if (holdResult.IsSuccess)
        {
            StatusMessage = $"Места успешно удержаны на 15 минут! Заказ готов к вводу данных пассажиров (W-05).";
            SeatsHeld?.Invoke(holdResult.Value);
        }
        else
        {
            StatusMessage = $"Не удалось удержать места: {holdResult.ErrorMessage}. Схема салона была обновлена.";
            RefreshSeatMap();
        }
    }

    /// <summary>
    /// Возвращается назад к результатам поиска рейсов (W-03).
    /// </summary>
    [RelayCommand]
    public void Back()
    {
        BackRequested?.Invoke();
    }

    /// <summary>
    /// Пересчитывает итоговую сумму, сводный текст и доступность перехода к следующему шагу.
    /// </summary>
    private void UpdateSelectedSeatsSummary()
    {
        TotalPrice = SelectedSeats.Sum(s => s.Price);
        CanProceed = SelectedSeats.Count == RequiredPassengersCount;

        if (SelectedSeats.Count == 0)
        {
            SelectedSeatsSummary = $"Выберите места на схеме салона (требуется мест: {RequiredPassengersCount})";
        }
        else
        {
            var seatDescriptions = SelectedSeats
                .OrderBy(s => s.RowNumber)
                .ThenBy(s => s.Letter, StringComparer.OrdinalIgnoreCase)
                .Select(s => $"{s.SeatNumber} ({s.ClassText})");

            SelectedSeatsSummary = $"Выбрано {SelectedSeats.Count} из {RequiredPassengersCount}: {string.Join(", ", seatDescriptions)}";
        }
    }

    /// <summary>
    /// Формирует структурированные ряды салона самолёта с разделением на левую и правую части прохода.
    /// </summary>
    private void BuildRows(List<SeatItemViewModel> allSeats)
    {
        var rowGroups = allSeats
            .GroupBy(s => s.RowNumber)
            .OrderBy(g => g.Key)
            .ToList();

        var rowsList = new List<SeatRowViewModel>();
        SeatClass? previousClass = null;

        foreach (var group in rowGroups)
        {
            SeatClass rowClass = group.First().Class;
            bool isFirstRow = previousClass == null || previousClass != rowClass;
            previousClass = rowClass;

            // Левый блок кресел: A, B, C (или A, C)
            var leftSeats = group
                .Where(s => s.Letter is "A" or "B" or "C")
                .OrderBy(s => s.Letter, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Правый блок кресел: D, E, F
            var rightSeats = group
                .Where(s => s.Letter is "D" or "E" or "F")
                .OrderBy(s => s.Letter, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rowVm = new SeatRowViewModel
            {
                RowNumber = group.Key,
                Class = rowClass,
                IsFirstRowOfClass = isFirstRow,
                LeftSeats = leftSeats,
                RightSeats = rightSeats
            };

            rowsList.Add(rowVm);
        }

        Rows = new ObservableCollection<SeatRowViewModel>(rowsList);
    }

    /// <summary>
    /// Разбирает номер места на ряд и букву (например, "12A" -> (12, "A")).
    /// </summary>
    private static (int Row, string Letter) ParseSeatNumber(string seatNumber)
    {
        if (string.IsNullOrWhiteSpace(seatNumber))
        {
            return (0, string.Empty);
        }

        int i = 0;
        while (i < seatNumber.Length && char.IsDigit(seatNumber[i]))
        {
            i++;
        }

        int row = 0;
        if (i > 0)
        {
            int.TryParse(seatNumber[..i], out row);
        }

        string letter = seatNumber[i..];
        return (row, letter);
    }
}
