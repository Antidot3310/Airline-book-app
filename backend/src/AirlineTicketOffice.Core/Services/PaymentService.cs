using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис проведения платежей по заказам и выпуска оплаченных билетов (M8).
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly AirlineDbContext _dbContext;
    private readonly IPaymentEmulator _paymentEmulator;
    private readonly IVirtualClockService _clockService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="PaymentService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <param name="paymentEmulator">Эмулятор внешнего платёжного шлюза.</param>
    /// <param name="clockService">Сервис виртуального времени системы.</param>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если одна из зависимостей равна null.</exception>
    public PaymentService(
        AirlineDbContext dbContext,
        IPaymentEmulator paymentEmulator,
        IVirtualClockService clockService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _paymentEmulator = paymentEmulator ?? throw new ArgumentNullException(nameof(paymentEmulator));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public Result<PaymentResultDto> PayWithCard(int orderId, CardDetailsDto card)
    {
        if (!IsCardValid(card))
        {
            return Result<PaymentResultDto>.Failure("Указаны некорректные реквизиты банковской карты.");
        }

        Result<Order> orderValidation = LoadAndValidateOrderForPayment(orderId);
        if (orderValidation.IsFailure)
        {
            return Result<PaymentResultDto>.Failure(orderValidation.ErrorMessage);
        }

        Order order = orderValidation.Value;

        EmulatorTransactionResult emulatorResult = _paymentEmulator.ProcessPayment(order.Id, order.TotalAmount, card);
        DateTime paymentTime = _clockService.CurrentVirtualTime;

        using IDbContextTransaction transaction = _dbContext.Database.BeginTransaction();

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            PaymentMethod = PaymentMethod.Card,
            Status = emulatorResult.Status,
            ExternalTransactionId = emulatorResult.ExternalTransactionId,
            CreatedAt = paymentTime
        };

        _dbContext.Payments.Add(payment);

        switch (emulatorResult.Status)
        {
            case PaymentStatus.Success:
                MarkOrderPaid(order);
                _dbContext.SaveChanges();
                _auditLogService.Log(
                    "PaymentSuccess",
                    "Payment",
                    payment.Id,
                    $"Заказ {order.OrderNumber} успешно оплачен картой на сумму {order.TotalAmount} руб. Транзакция: {payment.ExternalTransactionId}.");
                transaction.Commit();

                return Result<PaymentResultDto>.Success(
                    MapToPaymentResultDto(payment, order, emulatorResult.Message));

            case PaymentStatus.Failed:
                _dbContext.SaveChanges();
                _auditLogService.Log(
                    "PaymentFailed",
                    "Payment",
                    payment.Id,
                    $"Отказ при оплате картой заказа {order.OrderNumber}: {emulatorResult.Message}");
                transaction.Commit();

                return Result<PaymentResultDto>.Failure(emulatorResult.Message);

            case PaymentStatus.Indeterminate:
                _dbContext.SaveChanges();
                _auditLogService.Log(
                    "PaymentIndeterminate",
                    "Payment",
                    payment.Id,
                    $"Таймаут при оплате картой заказа {order.OrderNumber}. Статус Indeterminate.");
                transaction.Commit();

                return Result<PaymentResultDto>.Success(
                    MapToPaymentResultDto(
                        payment,
                        order,
                        $"{emulatorResult.Message} Бронь мест сохранена, ожидается проверка статуса платежа."));

            default:
                return Result<PaymentResultDto>.Failure(
                    $"Неизвестный статус ответа платёжного шлюза: {emulatorResult.Status}.");
        }
    }

    /// <inheritdoc />
    public Result<PaymentResultDto> PayWithCash(int orderId, decimal cashReceived)
    {
        Result<Order> orderValidation = LoadAndValidateOrderForPayment(orderId);
        if (orderValidation.IsFailure)
        {
            return Result<PaymentResultDto>.Failure(orderValidation.ErrorMessage);
        }

        Order order = orderValidation.Value;

        if (cashReceived < order.TotalAmount)
        {
            return Result<PaymentResultDto>.Failure(
                $"Внесённая сумма меньше стоимости заказа ({order.TotalAmount:N2} руб.).");
        }

        decimal changeAmount = Math.Round(cashReceived - order.TotalAmount, 2);

        using IDbContextTransaction transaction = _dbContext.Database.BeginTransaction();

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.TotalAmount,
            PaymentMethod = PaymentMethod.Cash,
            Status = PaymentStatus.Success,
            ExternalTransactionId = null,
            CreatedAt = _clockService.CurrentVirtualTime
        };

        _dbContext.Payments.Add(payment);
        MarkOrderPaid(order);
        _dbContext.SaveChanges();

        _auditLogService.Log(
            "PaymentSuccess",
            "Payment",
            payment.Id,
            $"Заказ {order.OrderNumber} оплачен наличными на сумму {order.TotalAmount:N2} руб. (внесено: {cashReceived:N2} руб., сдача: {changeAmount:N2} руб.).");

        transaction.Commit();

        return Result<PaymentResultDto>.Success(
            MapToPaymentResultDto(
                payment,
                order,
                $"Оплата наличными принята. Сдача: {changeAmount:N2} руб.",
                cashReceived,
                changeAmount));
    }

    /// <inheritdoc />
    public Result<PaymentStatus> CheckPaymentStatus(int paymentId)
    {
        return Result<PaymentStatus>.Failure("Метод будет реализован в Инкременте 27.");
    }

    private Result<Order> LoadAndValidateOrderForPayment(int orderId)
    {
        Order? order = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Payments)
            .FirstOrDefault(o => o.Id == orderId);

        if (order is null)
        {
            return Result<Order>.Failure($"Заказ с ID {orderId} не найден.");
        }

        if (order.Status == OrderStatus.Paid || order.Payments.Any(p => p.Status == PaymentStatus.Success))
        {
            return Result<Order>.Failure("Заказ уже оплачен.");
        }

        if (order.Payments.Any(p => p.Status == PaymentStatus.Indeterminate))
        {
            return Result<Order>.Failure(
                "По заказу уже есть платёж в статусе ожидания подтверждения (Indeterminate). Проверьте его статус.");
        }

        if (order.Status == OrderStatus.Expired)
        {
            return Result<Order>.Failure("Срок действия бронирования истёк.");
        }

        if (order.Status != OrderStatus.Created)
        {
            return Result<Order>.Failure($"Оплата невозможна: заказ находится в статусе {order.Status}.");
        }

        DateTime currentVirtualTime = _clockService.CurrentVirtualTime;
        if (order.HoldExpiresAt <= currentVirtualTime)
        {
            ExpireOrderOnPaymentAttempt(order);
            return Result<Order>.Failure("Срок действия бронирования истёк.");
        }

        return Result<Order>.Success(order);
    }

    private void ExpireOrderOnPaymentAttempt(Order order)
    {
        using IDbContextTransaction transaction = _dbContext.Database.BeginTransaction();

        order.Status = OrderStatus.Expired;
        int releasedSeatsCount = 0;

        foreach (Ticket ticket in order.Tickets)
        {
            if (ticket.Status == TicketStatus.Booked)
            {
                ticket.Status = TicketStatus.Cancelled;
            }

            if (ticket.FlightSeat is not null && ticket.FlightSeat.State == SeatState.Held)
            {
                ticket.FlightSeat.State = SeatState.Available;
                ticket.FlightSeat.HoldUntilTime = null;
                releasedSeatsCount++;
            }
        }

        _dbContext.SaveChanges();
        _auditLogService.Log(
            "ExpireHold",
            "Order",
            order.Id,
            $"Срок действия бронирования заказа {order.OrderNumber} истёк при попытке оплаты. Освобождено мест: {releasedSeatsCount}.");

        transaction.Commit();
    }

    private static void MarkOrderPaid(Order order)
    {
        order.Status = OrderStatus.Paid;

        foreach (Ticket ticket in order.Tickets)
        {
            ticket.Status = TicketStatus.Paid;

            if (ticket.FlightSeat is not null)
            {
                ticket.FlightSeat.State = SeatState.Sold;
                ticket.FlightSeat.HoldUntilTime = null;
            }
        }
    }

    private static bool IsCardValid(CardDetailsDto? card)
    {
        if (card is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(card.CardNumber)
            || string.IsNullOrWhiteSpace(card.CardHolder)
            || string.IsNullOrWhiteSpace(card.Cvv))
        {
            return false;
        }

        string normalizedNumber = card.CardNumber
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);

        return normalizedNumber.Length >= 12 && normalizedNumber.All(char.IsDigit);
    }

    private static PaymentResultDto MapToPaymentResultDto(
        Payment payment,
        Order order,
        string message,
        decimal? cashReceived = null,
        decimal changeAmount = 0m)
    {
        return new PaymentResultDto
        {
            PaymentId = payment.Id,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Amount = payment.Amount,
            CashReceived = cashReceived,
            ChangeAmount = changeAmount,
            PaymentMethod = payment.PaymentMethod,
            Status = payment.Status,
            ExternalTransactionId = payment.ExternalTransactionId,
            CreatedAt = payment.CreatedAt,
            Message = message
        };
    }
}
