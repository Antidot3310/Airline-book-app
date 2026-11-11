namespace AirlineTicketOffice.Core.Contracts.Orders;

/// <summary>
/// DTO данных пассажира при оформлении бронирования и создании заказа (W-05).
/// </summary>
public class PassengerOrderItemDto
{
    /// <summary>
    /// Фамилия, имя и отчество пассажира.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Номер документа, удостоверяющего личность пассажира (паспорт).
    /// </summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Контактный номер телефона пассажира.
    /// </summary>
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Контактный адрес электронной почты пассажира.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Буквенно-цифровое обозначение выбранного места в салоне (например, "1A", "12C").
    /// </summary>
    public string SeatNumber { get; set; } = string.Empty;

    /// <summary>
    /// Признак включения платного багажного места (+1 500 руб.).
    /// </summary>
    public bool HasBaggage { get; set; }
}
