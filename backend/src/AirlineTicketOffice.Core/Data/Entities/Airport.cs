namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность аэропорта (справочник M3).
/// </summary>
public class Airport
{
    /// <summary>
    /// Уникальный идентификатор аэропорта.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 3-буквенный уникальный код IATA (например, SVO, LED, AER).
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Город расположения аэропорта (например, Москва, Санкт-Петербург).
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// Официальное наименование аэропорта (например, Шереметьево, Пулково).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Признак активности аэропорта (деактивация вместо физического удаления).
    /// </summary>
    public bool IsActive { get; set; } = true;
}
