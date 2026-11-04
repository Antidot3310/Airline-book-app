namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Канал оформления заказа на авиабилеты.
/// </summary>
public enum OrderChannel
{
    /// <summary>
    /// Самообслуживание (онлайн-интерфейс пассажира).
    /// </summary>
    SelfService,

    /// <summary>
    /// Касса продажи билетов (рабочее место кассира).
    /// </summary>
    Cashier
}
