namespace AirlineTicketOffice.Core.Contracts.Directory;

/// <summary>
/// DTO типа воздушного судна (справочник M3).
/// </summary>
public class AircraftTypeDto
{
    /// <summary>
    /// Уникальный идентификатор типа воздушного судна в системе.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Наименование модели воздушного судна (например, Airbus A320, Sukhoi Superjet 100).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Общее количество пассажирских мест в салоне.
    /// </summary>
    public int TotalSeats { get; set; }

    /// <summary>
    /// Количество рядов кресел бизнес-класса.
    /// </summary>
    public int BusinessRowsCount { get; set; }

    /// <summary>
    /// Количество рядов кресел эконом-класса.
    /// </summary>
    public int EconomyRowsCount { get; set; }

    /// <summary>
    /// Схема салона в формате JSON (расположение кресел и проходов).
    /// </summary>
    public string? LayoutConfigJson { get; set; }

    /// <summary>
    /// Признак активности типа воздушного судна в системе.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
