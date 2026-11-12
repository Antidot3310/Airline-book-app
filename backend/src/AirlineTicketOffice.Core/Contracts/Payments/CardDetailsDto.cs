namespace AirlineTicketOffice.Core.Contracts.Payments;

/// <summary>
/// Реквизиты банковской карты для проведения оплаты через эмулятор платёжного шлюза (M14).
/// </summary>
public class CardDetailsDto
{
    /// <summary>
    /// Номер банковской карты.
    /// </summary>
    public string CardNumber { get; set; } = string.Empty;

    /// <summary>
    /// Имя и фамилия держателя карты латиницей.
    /// </summary>
    public string CardHolder { get; set; } = string.Empty;

    /// <summary>
    /// Месяц окончания срока действия карты (MM).
    /// </summary>
    public string ExpiryMonth { get; set; } = string.Empty;

    /// <summary>
    /// Год окончания срока действия карты (YY или YYYY).
    /// </summary>
    public string ExpiryYear { get; set; } = string.Empty;

    /// <summary>
    /// Код безопасности карты (CVV/CVC).
    /// </summary>
    public string Cvv { get; set; } = string.Empty;
}
