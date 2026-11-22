using CommunityToolkit.Mvvm.ComponentModel;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Вариант статусного фильтра расписания W-12.
/// Русскоязычная подпись статуса и счётчик рейсов формируются во ViewModel,
/// а не в XAML-представлении.
/// </summary>
public partial class FlightStatusFilterOption : ObservableObject
{
    /// <summary>
    /// Инициализирует новый экземпляр варианта статусного фильтра.
    /// </summary>
    /// <param name="title">Русскоязычная подпись фильтра.</param>
    /// <param name="status">Статус рейса; <c>null</c> означает «все рейсы».</param>
    public FlightStatusFilterOption(string title, FlightStatus? status)
    {
        Title = title;
        Status = status;
    }

    /// <summary>
    /// Русскоязычная подпись фильтра без счётчика.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Статус рейса, по которому выполняется фильтрация; <c>null</c> — без фильтра.
    /// </summary>
    public FlightStatus? Status { get; }

    /// <summary>
    /// Количество рейсов, попадающих в фильтр.
    /// </summary>
    [ObservableProperty]
    private int _count;

    /// <summary>
    /// Признак выбранного в данный момент фильтра.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Подпись фильтра со счётчиком «Запланированные (12)».
    /// </summary>
    public string DisplayTitle => Count > 0 ? $"{Title} ({Count})" : Title;

    partial void OnCountChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayTitle));
    }
}
