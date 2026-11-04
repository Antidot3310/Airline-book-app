namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Причина возврата авиабилета.
/// </summary>
public enum RefundReason
{
    /// <summary>
    /// Добровольный возврат по инициативе пассажира.
    /// </summary>
    Voluntary,

    /// <summary>
    /// Вынужденный возврат в связи с отменой рейса авиакомпанией.
    /// </summary>
    FlightCancelled
}
