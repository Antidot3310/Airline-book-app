namespace AirlineTicketOffice.Core.Data.Entities;

/// <summary>
/// Сущность пассажира / клиента системы (M2).
/// </summary>
public class Client
{
    /// <summary>
    /// Уникальный идентификатор пассажира.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Фамилия, имя и отчество пассажира.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Номер документа, удостоверяющего личность (паспорт).
    /// </summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Контактный номер телефона пассажира.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Адрес электронной почты пассажира для отправки маршрутных квитанций.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Навигационная коллекция билетов, оформленных на данного пассажира.
    /// </summary>
    public ICollection<Ticket> Tickets { get; set; } = [];
}
