using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Вариант режима виртуальных часов системы в пульте эмулятора W-16.
/// Русскоязычное наименование и пояснение режима формируются во ViewModel.
/// </summary>
public sealed class EmulatorClockModeOption
{
    /// <summary>
    /// Инициализирует новый экземпляр варианта режима часов.
    /// </summary>
    /// <param name="mode">Режим виртуальных часов из доменной модели.</param>
    /// <param name="title">Русскоязычное наименование режима.</param>
    /// <param name="description">Пояснение поведения режима.</param>
    public EmulatorClockModeOption(ClockMode mode, string title, string description)
    {
        Mode = mode;
        Title = title;
        Description = description;
    }

    /// <summary>
    /// Режим виртуальных часов из доменной модели.
    /// </summary>
    public ClockMode Mode { get; }

    /// <summary>
    /// Русскоязычное наименование режима.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Пояснение поведения режима.
    /// </summary>
    public string Description { get; }
}
