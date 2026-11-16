using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления отдельного посадочного места на интерактивной схеме салона W-04.
/// </summary>
public partial class SeatItemViewModel : ObservableObject
{
    private static readonly SolidColorBrush SelectedBrush = CreateFrozenBrush(0x1E, 0x88, 0xE5);
    private static readonly SolidColorBrush AvailableBrush = CreateFrozenBrush(0x4C, 0xAF, 0x50);
    private static readonly SolidColorBrush HeldBrush = CreateFrozenBrush(0xFF, 0x98, 0x00);
    private static readonly SolidColorBrush SoldBrush = CreateFrozenBrush(0x9E, 0x9E, 0x9E);
    private static readonly SolidColorBrush BlockedBrush = CreateFrozenBrush(0x42, 0x42, 0x42);
    private static readonly SolidColorBrush DefaultBrush = CreateFrozenBrush(0xE0, 0xE0, 0xE0);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Идентификатор места в базе данных.
    /// </summary>
    public int SeatId { get; set; }

    /// <summary>
    /// Полный буквенно-цифровой номер места (например, "1A", "12C").
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Номер ряда в салоне воздушного судна.
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Буквенный индекс места в ряду (A, B, C, D, E, F).
    /// </summary>
    public string Letter { get; set; } = string.Empty;

    /// <summary>
    /// Класс обслуживания (Бизнес или Эконом).
    /// </summary>
    public SeatClass Class { get; set; }

    /// <summary>
    /// Исходное базовое состояние места из ядра системы (Available, Held, Sold, Blocked).
    /// </summary>
    public SeatState BaseState { get; set; }

    /// <summary>
    /// Динамическая цена билета на данное место в рублях.
    /// </summary>
    public decimal Price { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Признак доступности места для клика/выбора пользователем.
    /// Доступно строго только тогда, когда в базе данных место имеет статус Available.
    /// </summary>
    public bool IsSelectable => BaseState == SeatState.Available;

    /// <summary>
    /// Текстовое наименование класса обслуживания.
    /// </summary>
    public string ClassText => Class == SeatClass.Business ? "Бизнес" : "Эконом";

    /// <summary>
    /// Текстовое описание текущего статуса места.
    /// </summary>
    public string StateText
    {
        get
        {
            if (IsSelected)
            {
                return "Выбрано вами";
            }

            return BaseState switch
            {
                SeatState.Available => "Свободно",
                SeatState.Held => "Удержано другим заказом",
                SeatState.Sold => "Занято / Продано",
                SeatState.Blocked => "Заблокировано",
                _ => "Недоступно"
            };
        }
    }

    /// <summary>
    /// Кисть фона кнопки кресла для привязки в XAML.
    /// </summary>
    public SolidColorBrush BackgroundBrush
    {
        get
        {
            if (IsSelected)
            {
                return SelectedBrush;
            }

            return BaseState switch
            {
                SeatState.Available => AvailableBrush,
                SeatState.Held => HeldBrush,
                SeatState.Sold => SoldBrush,
                SeatState.Blocked => BlockedBrush,
                _ => DefaultBrush
            };
        }
    }

    /// <summary>
    /// Цвет фона кнопки кресла в шестнадцатеричном формате HEX.
    /// </summary>
    public string BackgroundHex
    {
        get
        {
            if (IsSelected)
            {
                return "#1E88E5";
            }

            return BaseState switch
            {
                SeatState.Available => "#4CAF50",
                SeatState.Held => "#FF9800",
                SeatState.Sold => "#9E9E9E",
                SeatState.Blocked => "#424242",
                _ => "#E0E0E0"
            };
        }
    }

    /// <summary>
    /// Цвет текста внутри кресла.
    /// </summary>
    public string ForegroundHex => "#FFFFFF";

    /// <summary>
    /// Всплывающая подсказка (Tooltip) при наведении курсора на кресло.
    /// </summary>
    public string TooltipText => $"Место {SeatNumber} ({ClassText})\nЦена: {Price:N0} руб.\nСтатус: {StateText}";

    /// <summary>
    /// Уведомляет об изменении вычисляемых свойств при переключении выбора места.
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundBrush));
        OnPropertyChanged(nameof(BackgroundHex));
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(TooltipText));
    }
}
