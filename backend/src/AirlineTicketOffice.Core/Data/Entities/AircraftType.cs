namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность типа воздушного судна (справочник M3).
/// </summary>
public class AircraftType
{
    /// <summary>
    /// Уникальный идентификатор типа воздушного судна.
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
    /// Расширенная конфигурация схемы салона в формате JSON (буквы рядов, расположение проходов).
    /// </summary>
    public string? LayoutConfigJson { get; set; }

    /// <summary>
    /// Признак активности типа воздушного судна.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Коллекция бортов данного типа воздушного судна.
    /// </summary>
    public ICollection<Aircraft> Aircrafts { get; set; } = [];
}
