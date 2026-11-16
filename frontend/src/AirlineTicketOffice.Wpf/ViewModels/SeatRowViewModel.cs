using System.Collections.Generic;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления ряда кресел в салоне самолёта для схемы W-04.
/// Разделяет ряд на левый и правый блоки кресел с проходом по центру.
/// </summary>
public class SeatRowViewModel
{
    /// <summary>
    /// Номер ряда в салоне (1, 2, 3 ...).
    /// </summary>
    public int RowNumber { get; set; }

    /// <summary>
    /// Класс обслуживания кресел данного ряда (Бизнес или Эконом).
    /// </summary>
    public SeatClass Class { get; set; }

    /// <summary>
    /// Название класса обслуживания в верхнем регистре для заголовка секции.
    /// </summary>
    public string ClassName => Class == SeatClass.Business ? "БИЗНЕС-КЛАСС" : "ЭКОНОМ-КЛАСС";

    /// <summary>
    /// Признак первого ряда секции (для отображения разделителя секций).
    /// </summary>
    public bool IsFirstRowOfClass { get; set; }

    /// <summary>
    /// Кресла левого блока ряда (например, A, B, C или A, C).
    /// </summary>
    public IReadOnlyList<SeatItemViewModel> LeftSeats { get; set; } = [];

    /// <summary>
    /// Кресла правого блока ряда (например, D, E, F).
    /// </summary>
    public IReadOnlyList<SeatItemViewModel> RightSeats { get; set; } = [];
}
