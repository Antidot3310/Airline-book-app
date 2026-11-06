using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Clock.Events;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис виртуального системного времени и планировщика задач (M12).
/// Является Singleton-сервисом в процессе приложения, управляющим текущей симуляцией времени.
/// </summary>
public class VirtualClockService : IVirtualClockService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly object _lock = new();

    private DateTime _currentVirtualTime;
    private ClockMode _mode = ClockMode.Manual;
    private double _speedMultiplier = 1.0;

    /// <inheritdoc />
    public DateTime CurrentVirtualTime
    {
        get
        {
            lock (_lock)
            {
                return _currentVirtualTime;
            }
        }
    }

    /// <inheritdoc />
    public ClockMode Mode
    {
        get
        {
            lock (_lock)
            {
                return _mode;
            }
        }
    }

    /// <inheritdoc />
    public double SpeedMultiplier
    {
        get
        {
            lock (_lock)
            {
                return _speedMultiplier;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<VirtualTimeChangedEventArgs>? TimeChanged;

    /// <inheritdoc />
    public event EventHandler<HoldsExpiredEventArgs>? HoldsExpired;

    /// <inheritdoc />
    public event EventHandler<FlightStatusChangedEventArgs>? FlightStatusChanged;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="VirtualClockService"/>,
    /// загружая сохранённое состояние времени из базы данных.
    /// </summary>
    /// <param name="scopeFactory">Фабрика областей видимости DI для работы со Scoped-контекстом базы данных.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="scopeFactory"/> равен null.</exception>
    public VirtualClockService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
        var state = dbContext.SystemTimeStates.FirstOrDefault();

        if (state != null)
        {
            _currentVirtualTime = state.CurrentVirtualTime;
            _mode = state.Mode;
            _speedMultiplier = state.SpeedMultiplier;
        }
        else
        {
            _currentVirtualTime = DateTime.Now;
            _mode = ClockMode.Manual;
            _speedMultiplier = 1.0;
        }
    }

    /// <inheritdoc />
    public void AdvanceTime(TimeSpan delta)
    {
        DateTime oldTime;
        DateTime newTime;

        lock (_lock)
        {
            oldTime = _currentVirtualTime;
            _currentVirtualTime = _currentVirtualTime.Add(delta);
            newTime = _currentVirtualTime;

            SaveStateToDb(_currentVirtualTime, _mode, _speedMultiplier);
        }

        OnTimeChanged(new VirtualTimeChangedEventArgs(newTime, oldTime));
        TriggerScheduledJobs();
    }

    /// <inheritdoc />
    public void SetMode(ClockMode mode, double speedMultiplier = 1.0)
    {
        lock (_lock)
        {
            _mode = mode;
            _speedMultiplier = speedMultiplier;

            SaveStateToDb(_currentVirtualTime, _mode, _speedMultiplier);
        }
    }

    /// <inheritdoc />
    public void ResetToRealTime()
    {
        DateTime oldTime;
        DateTime newTime;

        lock (_lock)
        {
            oldTime = _currentVirtualTime;
            _currentVirtualTime = DateTime.Now;
            _mode = ClockMode.RealTime;
            _speedMultiplier = 1.0;
            newTime = _currentVirtualTime;

            SaveStateToDb(_currentVirtualTime, _mode, _speedMultiplier);
        }

        OnTimeChanged(new VirtualTimeChangedEventArgs(newTime, oldTime));
        TriggerScheduledJobs();
    }

    /// <inheritdoc />
    public void TriggerScheduledJobs()
    {
        DateTime currentTime;
        lock (_lock)
        {
            currentTime = _currentVirtualTime;
        }

        int expiredOrdersCount = 0;
        int releasedSeatsCount = 0;
        var departedFlightEvents = new List<(int FlightId, FlightStatus Status)>();

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();

            // А) Снятие просроченных броней
            var expiredOrders = db.Orders
                .Include(o => o.Tickets)
                .Where(o => o.Status == OrderStatus.Created && o.HoldExpiresAt <= currentTime)
                .ToList();

            foreach (var order in expiredOrders)
            {
                order.Status = OrderStatus.Expired;
                expiredOrdersCount++;

                foreach (var ticket in order.Tickets.Where(t => t.Status == TicketStatus.Booked))
                {
                    ticket.Status = TicketStatus.Cancelled;
                }
            }

            var heldSeats = db.FlightSeats
                .Where(s => s.State == SeatState.Held && s.HoldUntilTime <= currentTime)
                .ToList();

            foreach (var seat in heldSeats)
            {
                seat.State = SeatState.Available;
                seat.HoldUntilTime = null;
                releasedSeatsCount++;
            }

            if (expiredOrdersCount > 0 || releasedSeatsCount > 0)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    VirtualTime = currentTime,
                    RealTime = DateTime.Now,
                    Role = UserRole.System,
                    ActionType = "ExpireHold",
                    EntityName = "Order",
                    Description = $"Сняты просроченные брони. Заказов: {expiredOrdersCount}, освобождено мест: {releasedSeatsCount}"
                });
            }

            // Б) Автоматическая смена статусов вылетевших рейсов
            var departedFlights = db.Flights
                .Include(f => f.Tickets)
                .Where(f => f.Status == FlightStatus.Scheduled && f.DepartureTime <= currentTime)
                .ToList();

            foreach (var flight in departedFlights)
            {
                flight.Status = FlightStatus.Departed;

                var paidTickets = flight.Tickets.Where(t => t.Status == TicketStatus.Paid).ToList();
                foreach (var ticket in paidTickets)
                {
                    ticket.Status = TicketStatus.Used;
                }

                db.AuditLogs.Add(new AuditLog
                {
                    VirtualTime = currentTime,
                    RealTime = DateTime.Now,
                    Role = UserRole.System,
                    ActionType = "FlightDeparted",
                    EntityName = "Flight",
                    EntityId = flight.Id,
                    Description = $"Рейс {flight.FlightNumber} переведён в статус 'Вылетел'. Использовано билетов: {paidTickets.Count}"
                });

                departedFlightEvents.Add((flight.Id, FlightStatus.Departed));
            }

            db.SaveChanges();
        }

        if (expiredOrdersCount > 0 || releasedSeatsCount > 0)
        {
            OnHoldsExpired(new HoldsExpiredEventArgs(expiredOrdersCount, releasedSeatsCount));
        }

        foreach (var (flightId, status) in departedFlightEvents)
        {
            OnFlightStatusChanged(new FlightStatusChangedEventArgs(flightId, status));
        }
    }

    /// <summary>
    /// Вызывает событие изменения виртуального времени <see cref="TimeChanged"/>.
    /// </summary>
    /// <param name="e">Аргументы события с новым и предыдущим временем.</param>
    protected virtual void OnTimeChanged(VirtualTimeChangedEventArgs e)
    {
        TimeChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Вызывает событие истечения срока броней <see cref="HoldsExpired"/>.
    /// </summary>
    /// <param name="e">Аргументы события с количеством отменённых заказов и мест.</param>
    protected virtual void OnHoldsExpired(HoldsExpiredEventArgs e)
    {
        HoldsExpired?.Invoke(this, e);
    }

    /// <summary>
    /// Вызывает событие изменения статуса рейса <see cref="FlightStatusChanged"/>.
    /// </summary>
    /// <param name="e">Аргументы события с идентификатором рейса и новым статусом.</param>
    protected virtual void OnFlightStatusChanged(FlightStatusChangedEventArgs e)
    {
        FlightStatusChanged?.Invoke(this, e);
    }

    private void SaveStateToDb(DateTime virtualTime, ClockMode mode, double speedMultiplier)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
        var state = dbContext.SystemTimeStates.FirstOrDefault();

        if (state == null)
        {
            state = new SystemTimeState
            {
                Id = 1,
                CurrentVirtualTime = virtualTime,
                Mode = mode,
                SpeedMultiplier = speedMultiplier
            };
            dbContext.SystemTimeStates.Add(state);
        }
        else
        {
            state.CurrentVirtualTime = virtualTime;
            state.Mode = mode;
            state.SpeedMultiplier = speedMultiplier;
        }

        dbContext.SaveChanges();
    }
}
