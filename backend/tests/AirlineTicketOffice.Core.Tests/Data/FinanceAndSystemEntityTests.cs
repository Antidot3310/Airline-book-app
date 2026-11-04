using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Tests.Data;

/// <summary>
/// Модульные тесты для проверки сущностей платежей, возвратов, аудита и часов (Payment, Refund, AuditLog, SystemTimeState).
/// </summary>
public class FinanceAndSystemEntityTests
{
    [Fact]
    public void CreateFinanceAndSystemEntities_DefaultAndAssignedValues_SetsPropertiesAndRelationshipsCorrectly()
    {
        // Arrange
        DateTime virtualNow = new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc);
        DateTime realNow = DateTime.UtcNow;

        var order = new Order
        {
            Id = 101,
            OrderNumber = "ORD-2001",
            TotalAmount = 7500.00m,
            CreatedAt = virtualNow,
            HoldExpiresAt = virtualNow.AddMinutes(15)
        };

        var payment = new Payment
        {
            Id = 501,
            OrderId = order.Id,
            Order = order,
            Amount = 7500.00m,
            PaymentMethod = PaymentMethod.Card,
            Status = PaymentStatus.Success,
            ExternalTransactionId = "TXN-987654321",
            CreatedAt = virtualNow
        };

        var ticket = new Ticket
        {
            Id = 1001,
            TicketNumber = "TKT-30001",
            OrderId = order.Id,
            Price = 7500.00m,
            Status = TicketStatus.Paid
        };

        var refund = new Refund
        {
            Id = 701,
            TicketId = ticket.Id,
            Ticket = ticket,
            RefundPercent = 90,
            Amount = 6750.00m,
            Reason = RefundReason.Voluntary,
            CreatedAt = virtualNow.AddHours(2)
        };

        var auditLog = new AuditLog
        {
            Id = 901,
            VirtualTime = virtualNow,
            RealTime = realNow,
            Role = UserRole.Passenger,
            ActionType = "PayOrder",
            EntityName = "Order",
            EntityId = order.Id,
            Description = "Заказ успешно оплачен банковской картой"
        };

        var systemTimeState = new SystemTimeState
        {
            Id = 1,
            CurrentVirtualTime = virtualNow,
            Mode = ClockMode.Accelerated,
            SpeedMultiplier = 5.0
        };

        // Act
        order.Payments.Add(payment);
        ticket.Refund = refund;

        // Assert
        // Проверка Payment и связи с Order
        Assert.Equal(501, payment.Id);
        Assert.Equal(101, payment.OrderId);
        Assert.Same(order, payment.Order);
        Assert.Equal(7500.00m, payment.Amount);
        Assert.Equal(PaymentMethod.Card, payment.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, payment.Status);
        Assert.Equal("TXN-987654321", payment.ExternalTransactionId);
        Assert.Equal(virtualNow, payment.CreatedAt);
        Assert.Single(order.Payments);
        Assert.Same(payment, order.Payments.First());

        // Проверка Refund и связи с Ticket
        Assert.Equal(701, refund.Id);
        Assert.Equal(1001, refund.TicketId);
        Assert.Same(ticket, refund.Ticket);
        Assert.Equal(90, refund.RefundPercent);
        Assert.Equal(6750.00m, refund.Amount);
        Assert.Equal(RefundReason.Voluntary, refund.Reason);
        Assert.Equal(virtualNow.AddHours(2), refund.CreatedAt);
        Assert.Same(refund, ticket.Refund);

        // Проверка AuditLog
        Assert.Equal(901, auditLog.Id);
        Assert.Equal(virtualNow, auditLog.VirtualTime);
        Assert.Equal(realNow, auditLog.RealTime);
        Assert.Equal(UserRole.Passenger, auditLog.Role);
        Assert.Equal("PayOrder", auditLog.ActionType);
        Assert.Equal("Order", auditLog.EntityName);
        Assert.Equal(101, auditLog.EntityId);
        Assert.Equal("Заказ успешно оплачен банковской картой", auditLog.Description);

        // Проверка SystemTimeState
        Assert.Equal(1, systemTimeState.Id);
        Assert.Equal(virtualNow, systemTimeState.CurrentVirtualTime);
        Assert.Equal(ClockMode.Accelerated, systemTimeState.Mode);
        Assert.Equal(5.0, systemTimeState.SpeedMultiplier);
    }
}
