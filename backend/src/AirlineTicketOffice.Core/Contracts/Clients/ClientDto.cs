namespace AirlineTicketOffice.Core.Contracts.Clients;

/// <summary>
/// DTO данных пассажира / клиента авиакомпании (M2).
/// </summary>
public class ClientDto
{
    /// <summary>
    /// Уникальный идентификатор пассажира в системе (0 при создании нового).
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
    /// Контактный адрес электронной почты пассажира для маршрутных квитанций.
    /// </summary>
    public string? ContactEmail { get; set; }
}
