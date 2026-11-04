namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Статус выполнения рейса.
/// </summary>
public enum FlightStatus
{
    /// <summary>
    /// Запланирован по расписанию.
    /// </summary>
    Scheduled,

    /// <summary>
    /// Вылетел (рейс отправлен).
    /// </summary>
    Departed,

    /// <summary>
    /// Отменён.
    /// </summary>
    Cancelled
}
