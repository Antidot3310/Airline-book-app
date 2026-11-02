using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность борта / воздушного судна (справочник M3).
/// </summary>
public class Aircraft
{
    /// <summary>
    /// Уникальный идентификатор воздушного судна.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Уникальный бортовой номер воздушного судна (например, RA-89014).
    /// </summary>
    public string TailNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор связанного типа воздушного судна.
    /// </summary>
    public int AircraftTypeId { get; set; }

    /// <summary>
    /// Навигационное свойство связанного типа воздушного судна.
    /// </summary>
    public AircraftType? AircraftType { get; set; }

    /// <summary>
    /// Эксплуатационный статус борта.
    /// </summary>
    public AircraftStatus Status { get; set; } = AircraftStatus.InService;

    /// <summary>
    /// Признак активности борта.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
