namespace AirlineTicketOffice.Core.Contracts.Clock.Events;

/// <summary>
/// Аргументы события истечения срока действия удерживаемых броней (Holds).
/// </summary>
public class HoldsExpiredEventArgs : EventArgs
{
    /// <summary>
    /// Количество аннулированных просроченных заказов.
    /// </summary>
    public int ExpiredOrdersCount { get; }

    /// <summary>
    /// Количество освобождённых мест на рейсах.
    /// </summary>
    public int ReleasedSeatsCount { get; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="HoldsExpiredEventArgs"/>.
    /// </summary>
    /// <param name="expiredOrdersCount">Количество просроченных заказов.</param>
    /// <param name="releasedSeatsCount">Количество освобождённых мест.</param>
    public HoldsExpiredEventArgs(int expiredOrdersCount, int releasedSeatsCount)
    {
        ExpiredOrdersCount = expiredOrdersCount;
        ReleasedSeatsCount = releasedSeatsCount;
    }
}
