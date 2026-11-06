namespace AirlineTicketOffice.Core.Contracts.Directory;

/// <summary>
/// DTO аэропорта (справочник M3).
/// </summary>
public class AirportDto
{
    /// <summary>
    /// Уникальный идентификатор аэропорта в системе.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 3-буквенный IATA-код аэропорта (например, SVO, LED, AER).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Город расположения аэропорта.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Официальное наименование аэропорта (например, Шереметьево).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Признак активности аэропорта в системе.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
