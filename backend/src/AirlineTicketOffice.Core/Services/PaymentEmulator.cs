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
/// Эмулятор внешнего платёжного шлюза (M14).
/// Имитирует обработку платежей по карте с настраиваемыми режимами работы,
/// ведёт журнал входящих запросов в памяти и поддерживает ручное разрешение зависших транзакций.
/// </summary>
public class PaymentEmulator : IPaymentEmulator
{
    private readonly AirlineDbContext _dbContext;
    private readonly IVirtualClockService _clockService;
    private readonly IAuditLogService _auditLogService;
    private readonly List<EmulatorLogEntryDto> _requestLogs = [];
    private readonly object _lock = new();
    private int _logIdCounter = 0;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="PaymentEmulator"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <param name="clockService">Сервис виртуального времени системы.</param>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если одна из зависимостей равна null.</exception>
    public PaymentEmulator(
        AirlineDbContext dbContext,
        IVirtualClockService clockService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public PaymentEmulatorMode Mode { get; set; } = PaymentEmulatorMode.AlwaysSuccess;

    /// <inheritdoc />
    public int DelayMilliseconds { get; set; } = 0;

    /// <inheritdoc />
    public EmulatorTransactionResult ProcessPayment(int orderId, decimal amount, CardDetailsDto card)
    {
        int delayMs = DelayMilliseconds;
        if (delayMs > 0)
        {
            Thread.Sleep(delayMs);
        }

        EmulatorTransactionResult result = BuildTransactionResult(Mode);
        string maskedCardNumber = MaskCardNumber(card?.CardNumber);
        DateTime requestTimestamp = _clockService.CurrentVirtualTime;

        lock (_lock)
        {
            _logIdCounter++;
            _requestLogs.Add(new EmulatorLogEntryDto
            {
                Id = _logIdCounter,
                Timestamp = requestTimestamp,
                OrderId = orderId,
                Amount = amount,
                MaskedCardNumber = maskedCardNumber,
                Status = result.Status,
                ExternalTransactionId = result.ExternalTransactionId,
                Message = result.Message
            });
        }

        return result;
    }

    /// <inheritdoc />
    public IReadOnlyList<EmulatorLogEntryDto> GetIncomingRequestsLog()
    {
        lock (_lock)
        {
            return _requestLogs.ToList();
        }
    }

    /// <inheritdoc />
    public void ClearLog()
    {
        lock (_lock)
        {
            _requestLogs.Clear();
        }
    }

    /// <inheritdoc />
    public Result ManuallyResolvePayment(int paymentId, bool markSuccess)
    {
        Payment? payment = _dbContext.Payments
            .Include(p => p.Order)
                .ThenInclude(o => o!.Tickets)
                    .ThenInclude(t => t.FlightSeat)
            .FirstOrDefault(p => p.Id == paymentId);

        if (payment is null)
        {
            return Result.Failure($"Платёж с ID {paymentId} не найден.");
        }

        if (payment.Status != PaymentStatus.Indeterminate)
        {
            return Result.Failure(
                $"Ручное подтверждение возможно только для платежей со статусом Indeterminate (текущий: {payment.Status}).");
        }

        if (markSuccess && payment.Order is not null && payment.Order.Status != OrderStatus.Created)
        {
            return Result.Failure(
                $"Невозможно подтвердить платёж: заказ {payment.Order.OrderNumber} уже находится в статусе {payment.Order.Status}.");
        }

        using IDbContextTransaction transaction = _dbContext.Database.BeginTransaction();

        if (markSuccess)
        {
            ApplyManualSuccess(payment);
            _auditLogService.Log(
                "ManualPaymentResolve",
                "Payment",
                payment.Id,
                $"Платёж {payment.Id} по заказу {payment.Order?.OrderNumber} подтверждён оператором вручную (Успех). Выпущены билеты.");
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            _auditLogService.Log(
                "ManualPaymentResolve",
                "Payment",
                payment.Id,
                $"Платёж {payment.Id} по заказу {payment.Order?.OrderNumber} отклонён оператором вручную.");
        }

        _dbContext.SaveChanges();
        transaction.Commit();

        return Result.Success();
    }

    private static void ApplyManualSuccess(Payment payment)
    {
        payment.Status = PaymentStatus.Success;
        if (string.IsNullOrWhiteSpace(payment.ExternalTransactionId))
        {
            payment.ExternalTransactionId = $"TXN-MANUAL-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        }

        if (payment.Order is null)
        {
            return;
        }

        payment.Order.Status = OrderStatus.Paid;

        foreach (Ticket ticket in payment.Order.Tickets)
        {
            ticket.Status = TicketStatus.Paid;

            if (ticket.FlightSeat is not null)
            {
                ticket.FlightSeat.State = SeatState.Sold;
                ticket.FlightSeat.HoldUntilTime = null;
            }
        }
    }

    private static EmulatorTransactionResult BuildTransactionResult(PaymentEmulatorMode mode)
    {
        PaymentEmulatorMode effectiveMode = mode == PaymentEmulatorMode.Random
            ? (Random.Shared.Next(2) == 0 ? PaymentEmulatorMode.AlwaysSuccess : PaymentEmulatorMode.AlwaysReject)
            : mode;

        return effectiveMode switch
        {
            PaymentEmulatorMode.AlwaysSuccess => new EmulatorTransactionResult
            {
                Status = PaymentStatus.Success,
                ExternalTransactionId = $"TXN-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                Message = "Платёж успешно одобрен платёжным шлюзом."
            },
            PaymentEmulatorMode.AlwaysReject => new EmulatorTransactionResult
            {
                Status = PaymentStatus.Failed,
                ExternalTransactionId = null,
                Message = "Платёж отклонён банком-эмитентом: недостаточно средств или отказ карты."
            },
            PaymentEmulatorMode.Timeout => new EmulatorTransactionResult
            {
                Status = PaymentStatus.Indeterminate,
                ExternalTransactionId = null,
                Message = "Превышено время ожидания ответа от платёжного шлюза (Таймаут)."
            },
            _ => new EmulatorTransactionResult
            {
                Status = PaymentStatus.Failed,
                ExternalTransactionId = null,
                Message = "Неизвестный режим работы эмулятора."
            }
        };
    }

    private static string MaskCardNumber(string? cardNumber)
    {
        string trimmed = cardNumber?.Trim() ?? string.Empty;
        return trimmed.Length >= 4
            ? $"**** **** **** {trimmed[^4..]}"
            : "****";
    }
}
