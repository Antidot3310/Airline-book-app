namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Эксплуатационный статус воздушного судна.
/// </summary>
public enum AircraftStatus
{
    /// <summary>
    /// В эксплуатации (готов к выполнению рейсов).
    /// </summary>
    InService,

    /// <summary>
    /// На техническом обслуживании или ремонте.
    /// </summary>
    Maintenance
}
