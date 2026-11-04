using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Clock.Events;

/// <summary>
/// Аргументы события изменения статуса рейса.
/// </summary>
public class FlightStatusChangedEventArgs : EventArgs
{
    /// <summary>
    /// Идентификатор рейса, изменившего статус.
    /// </summary>
    public int FlightId { get; }

    /// <summary>
    /// Новый статус рейса (Scheduled, Departed, Cancelled).
    /// </summary>
    public FlightStatus NewStatus { get; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="FlightStatusChangedEventArgs"/>.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <param name="newStatus">Новый статус рейса.</param>
    public FlightStatusChangedEventArgs(int flightId, FlightStatus newStatus)
    {
        FlightId = flightId;
        NewStatus = newStatus;
    }
}
