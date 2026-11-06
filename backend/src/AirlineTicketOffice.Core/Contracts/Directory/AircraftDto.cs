using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Directory;

/// <summary>
/// DTO конкретного борта / воздушного судна (справочник M3).
/// </summary>
public class AircraftDto
{
    /// <summary>
    /// Уникальный идентификатор воздушного судна в системе.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Бортовой номер воздушного судна (например, RA-89014).
    /// </summary>
    public string TailNumber { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор связанного типа воздушного судна.
    /// </summary>
    public int AircraftTypeId { get; set; }

    /// <summary>
    /// Наименование модели типа воздушного судна.
    /// </summary>
    public string AircraftTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Эксплуатационный статус борта (InService, Maintenance).
    /// </summary>
    public AircraftStatus Status { get; set; } = AircraftStatus.InService;

    /// <summary>
    /// Признак активности борта в системе.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
