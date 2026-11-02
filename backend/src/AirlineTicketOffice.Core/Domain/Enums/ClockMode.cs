namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Режим работы виртуальных часов системы.
/// </summary>
public enum ClockMode
{
    /// <summary>
    /// Реальное астрономическое время хост-системы.
    /// </summary>
    RealTime,

    /// <summary>
    /// Ускоренное течение времени с заданным множителем скорости.
    /// </summary>
    Accelerated,

    /// <summary>
    /// Ручной режим установки виртуального времени.
    /// </summary>
    Manual
}
