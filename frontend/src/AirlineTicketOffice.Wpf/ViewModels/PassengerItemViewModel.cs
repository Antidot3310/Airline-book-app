using System;
using CommunityToolkit.Mvvm.ComponentModel;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления строки одного пассажира в форме оформления бронирования W-05.
/// </summary>
public partial class PassengerItemViewModel : ObservableObject
{
    /// <summary>
    /// Порядковый номер пассажира в заказе (1, 2, ...).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Буквенно-цифровой номер закреплённого посадочного места (например, "1A", "4C").
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Класс обслуживания выбранного кресла (Бизнес или Эконом).
    /// </summary>
    public SeatClass SeatClass { get; set; }

    /// <summary>
    /// Базовая динамическая стоимость посадочного места без учёта багажа.
    /// </summary>
    public decimal BaseSeatPrice { get; set; }

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _documentNumber = string.Empty;

    [ObservableProperty]
    private string? _contactPhone;

    [ObservableProperty]
    private string? _contactEmail;

    [ObservableProperty]
    private bool _hasBaggage;

    /// <summary>
    /// Событие изменения данных билета (например, включение/выключение багажа) для пересчёта итогов.
    /// </summary>
    public event Action? BaggageChanged;

    /// <summary>
    /// Стоимость платного багажного места (1 500 руб. согласно доменным правилам).
    /// </summary>
    public decimal BaggagePrice => HasBaggage ? 1500m : 0m;

    /// <summary>
    /// Итоговая стоимость билета пассажира с учётом места и багажа.
    /// </summary>
    public decimal TicketPrice => BaseSeatPrice + BaggagePrice;

    /// <summary>
    /// Текстовое обозначение класса обслуживания.
    /// </summary>
    public string ClassText => SeatClass == SeatClass.Business ? "Бизнес-класс" : "Эконом-класс";

    /// <summary>
    /// Текстовый заголовок карточки пассажира.
    /// </summary>
    public string HeaderText => $"Пассажир №{Index} • Место {SeatNumber} ({ClassText})";

    /// <summary>
    /// Обработчик переключения чекбокса багажа.
    /// </summary>
    partial void OnHasBaggageChanged(bool value)
    {
        OnPropertyChanged(nameof(BaggagePrice));
        OnPropertyChanged(nameof(TicketPrice));
        BaggageChanged?.Invoke();
    }
}
