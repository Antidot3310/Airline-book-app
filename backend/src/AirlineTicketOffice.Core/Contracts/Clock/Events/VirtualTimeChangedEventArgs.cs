namespace AirlineTicketOffice.Core.Contracts.Clock.Events;

/// <summary>
/// Аргументы события изменения виртуального времени системы.
/// </summary>
public class VirtualTimeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Новое значение виртуального системного времени.
    /// </summary>
    public DateTime NewVirtualTime { get; }

    /// <summary>
    /// Предыдущее значение виртуального системного времени.
    /// </summary>
    public DateTime OldVirtualTime { get; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="VirtualTimeChangedEventArgs"/>.
    /// </summary>
    /// <param name="newVirtualTime">Новое виртуальное время.</param>
    /// <param name="oldVirtualTime">Предыдущее виртуальное время.</param>
    public VirtualTimeChangedEventArgs(DateTime newVirtualTime, DateTime oldVirtualTime)
    {
        NewVirtualTime = newVirtualTime;
        OldVirtualTime = oldVirtualTime;
    }
}
