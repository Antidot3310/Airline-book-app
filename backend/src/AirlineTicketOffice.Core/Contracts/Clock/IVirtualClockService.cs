using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Clock;

/// <summary>
/// Интерфейс сервиса виртуального времени и планировщика фоновых регламентных задач (M12).
/// Управляет текущим виртуальным временем системы, режимами симуляции (Manual, RealTime, Accelerated)
/// и генерирует события для обновления бизнес-логики (протухание броней, вылеты рейсов).
/// </summary>
public interface IVirtualClockService
{
    /// <summary>
    /// Текущее виртуальное время системы.
    /// </summary>
    DateTime CurrentVirtualTime { get; }

    /// <summary>
    /// Текущий режим работы системных часов.
    /// </summary>
    ClockMode Mode { get; }

    /// <summary>
    /// Коэффициент масштабирования времени для ускоренного режима (по умолчанию 1.0).
    /// </summary>
    double SpeedMultiplier { get; }

    /// <summary>
    /// Смещает виртуальное время вперёд (или назад) на указанный интервал.
    /// </summary>
    /// <param name="delta">Интервал изменения времени.</param>
    void AdvanceTime(TimeSpan delta);

    /// <summary>
    /// Устанавливает режим работы виртуальных часов и множитель скорости.
    /// </summary>
    /// <param name="mode">Режим работы часов (Manual, RealTime, Accelerated).</param>
    /// <param name="speedMultiplier">Множитель скорости симуляции (по умолчанию 1.0).</param>
    void SetMode(ClockMode mode, double speedMultiplier = 1.0);

    /// <summary>
    /// Сбрасывает виртуальное время на текущее реальное астрономическое время хост-системы
    /// и переключает режим в RealTime со скоростью 1.0.
    /// </summary>
    void ResetToRealTime();

    /// <summary>
    /// Запускает внеплановую проверку регламентных задач (аннулирование просроченных броней, изменение статусов рейсов).
    /// </summary>
    void TriggerScheduledJobs();

    /// <summary>
    /// Событие, возникающее при любом изменении виртуального времени системы.
    /// </summary>
    event EventHandler<VirtualTimeChangedEventArgs>? TimeChanged;

    /// <summary>
    /// Событие, возникающее при аннулировании просроченных броней мест.
    /// </summary>
    event EventHandler<HoldsExpiredEventArgs>? HoldsExpired;

    /// <summary>
    /// Событие, возникающее при автоматическом изменении статуса рейса по расписанию.
    /// </summary>
    event EventHandler<FlightStatusChangedEventArgs>? FlightStatusChanged;
}
