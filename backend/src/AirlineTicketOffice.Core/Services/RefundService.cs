using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис расчёта и проведения возвратов авиабилетов (M9).
/// </summary>
public class RefundService : IRefundService
{
    private readonly AirlineDbContext _dbContext;
    private readonly IVirtualClockService _clockService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="RefundService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных авиакассы.</param>
    /// <param name="clockService">Сервис системного виртуального времени.</param>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    public RefundService(
        AirlineDbContext dbContext,
        IVirtualClockService clockService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public Result<RefundCalculationDto> CalculateRefund(int ticketId)
    {
        Ticket? ticket = _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Flight)
            .FirstOrDefault(t => t.Id == ticketId);

        if (ticket == null)
        {
            return Result<RefundCalculationDto>.Failure($"Билет с ID {ticketId} не найден.");
        }

        if (ticket.Flight == null)
        {
            return Result<RefundCalculationDto>.Failure($"Не найден рейс для билета с ID {ticketId}.");
        }

        bool isFlightDepartedOrPassed = ticket.Flight.Status != FlightStatus.Cancelled
            && (ticket.Flight.Status == FlightStatus.Departed || ticket.Flight.DepartureTime <= _clockService.CurrentVirtualTime);

        if (ticket.Status == TicketStatus.Used || isFlightDepartedOrPassed)
        {
            return Result<RefundCalculationDto>.Failure("Возврат билета невозможен: рейс уже вылетел.");
        }

        if (ticket.Status == TicketStatus.Refunded)
        {
            return Result<RefundCalculationDto>.Failure("Билет уже возвращён.");
        }

        if (ticket.Status == TicketStatus.Cancelled)
        {
            return Result<RefundCalculationDto>.Failure("Билет аннулирован и не подлежит возврату.");
        }

        if (ticket.Status != TicketStatus.Paid)
        {
            return Result<RefundCalculationDto>.Failure("Возврат возможен только для оплаченных билетов.");
        }

        int refundPercent;
        string reasonDescription;

        if (ticket.Flight.Status == FlightStatus.Cancelled)
        {
            refundPercent = 100;
            reasonDescription = "Вынужденный возврат (100% без удержаний) в связи с отменой рейса.";
        }
        else
        {
            TimeSpan timeUntilDeparture = ticket.Flight.DepartureTime - _clockService.CurrentVirtualTime;

            if (timeUntilDeparture >= TimeSpan.FromHours(24))
            {
                refundPercent = 90;
                reasonDescription = "Добровольный возврат за 24 часа и более до вылета (90%, штраф 10%).";
            }
            else
            {
                refundPercent = 50;
                reasonDescription = "Добровольный возврат менее чем за 24 часа до вылета (50%, штраф 50%).";
            }
        }

        decimal refundAmount = Math.Round(ticket.Price * refundPercent / 100m, 2, MidpointRounding.AwayFromZero);
        decimal penaltyAmount = ticket.Price - refundAmount;

        RefundCalculationDto dto = new RefundCalculationDto
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            OriginalPrice = ticket.Price,
            RefundPercent = refundPercent,
            RefundAmount = refundAmount,
            PenaltyAmount = penaltyAmount,
            ReasonDescription = reasonDescription,
            IsRefundAllowed = true
        };

        return Result<RefundCalculationDto>.Success(dto);
    }

    /// <inheritdoc />
    public Result<RefundResultDto> ProcessRefund(int ticketId)
    {
        Result<RefundCalculationDto> calcResult = CalculateRefund(ticketId);
        if (calcResult.IsFailure)
        {
            return Result<RefundResultDto>.Failure(calcResult.ErrorMessage);
        }

        RefundCalculationDto calc = calcResult.Value!;

        Ticket? ticket = _dbContext.Tickets
            .Include(t => t.Order)
                .ThenInclude(o => o!.Tickets)
            .Include(t => t.FlightSeat)
            .Include(t => t.Flight)
            .FirstOrDefault(t => t.Id == ticketId);

        if (ticket == null)
        {
            return Result<RefundResultDto>.Failure($"Билет с ID {ticketId} не найден.");
        }

        if (ticket.Status != TicketStatus.Paid)
        {
            return Result<RefundResultDto>.Failure("Возврат возможен только для оплаченных билетов.");
        }

        if (ticket.Order == null || (ticket.Order.Status != OrderStatus.Paid && ticket.Order.Status != OrderStatus.PartiallyRefunded))
        {
            return Result<RefundResultDto>.Failure($"Возврат невозможен: заказ находится в статусе {ticket.Order?.Status}.");
        }

        using IDbContextTransaction transaction = _dbContext.Database.BeginTransaction();

        Refund refund = new Refund
        {
            TicketId = ticket.Id,
            RefundPercent = calc.RefundPercent,
            Amount = calc.RefundAmount,
            Reason = ticket.Flight!.Status == FlightStatus.Cancelled
                ? RefundReason.FlightCancelled
                : RefundReason.Voluntary,
            CreatedAt = _clockService.CurrentVirtualTime
        };
        _dbContext.Refunds.Add(refund);

        ticket.Status = TicketStatus.Refunded;

        if (ticket.FlightSeat is not null)
        {
            ticket.FlightSeat.State = SeatState.Available;
            ticket.FlightSeat.HoldUntilTime = null;
        }

        bool allRefunded = ticket.Order.Tickets.All(t => t.Status == TicketStatus.Refunded);
        if (allRefunded)
        {
            ticket.Order.Status = OrderStatus.Refunded;
        }
        else
        {
            ticket.Order.Status = OrderStatus.PartiallyRefunded;
        }

        _dbContext.SaveChanges();

        _auditLogService.Log(
            "ProcessRefund",
            "Ticket",
            ticket.Id,
            $"Оформлен возврат билета {ticket.TicketNumber} на сумму {refund.Amount:N2} руб. ({refund.RefundPercent}%). Место {ticket.FlightSeat?.SeatNumber} освобождено. Статус заказа {ticket.Order.OrderNumber}: {ticket.Order.Status}.");

        transaction.Commit();

        RefundResultDto resultDto = new RefundResultDto
        {
            RefundId = refund.Id,
            TicketId = ticket.Id,
            TicketNumber = ticket.TicketNumber,
            OriginalPrice = ticket.Price,
            RefundPercent = refund.RefundPercent,
            RefundAmount = refund.Amount,
            PenaltyAmount = calc.PenaltyAmount,
            Reason = refund.Reason,
            UpdatedOrderStatus = ticket.Order.Status,
            ProcessedAt = refund.CreatedAt
        };

        return Result<RefundResultDto>.Success(resultDto);
    }

    /// <inheritdoc />
    public Result<MassRefundResultDto> ProcessFlightCancellationRefund(int flightId)
    {
        return Result<MassRefundResultDto>.Failure("Метод будет реализован в Инкременте 30.");
    }
}
