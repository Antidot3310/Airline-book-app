using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность состояния виртуальных часов системы (M12).
/// В таблице базы данных всегда содержится ровно одна запись-одиночка (Singleton, Id = 1).
/// </summary>
public class SystemTimeState
{
    /// <summary>
    /// Уникальный идентификатор состояния (всегда равен 1).
    /// </summary>
    public int Id { get; set; } = 1;

    /// <summary>
    /// Текущее виртуальное время системы.
    /// </summary>
    public DateTime CurrentVirtualTime { get; set; }

    /// <summary>
    /// Режим работы системных часов (RealTime, Accelerated, Manual).
    /// </summary>
    public ClockMode Mode { get; set; } = ClockMode.Manual;

    /// <summary>
    /// Коэффициент масштабирования времени для ускоренного режима.
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;
}
