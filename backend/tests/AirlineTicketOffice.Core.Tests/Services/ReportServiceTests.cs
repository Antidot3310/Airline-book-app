using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса формирования аналитической отчётности <see cref="ReportService"/> (M10).
/// </summary>
public class ReportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly AirlineDbContext _dbContext;

    public ReportServiceTests()
    {
        ServiceCollection services = new ServiceCollection();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<AirlineDbContext>();
        _dbContext.Database.EnsureCreated();
        DbInitializer.Initialize(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private ReportService CreateReportService()
    {
        return new ReportService(_dbContext);
    }

    [Fact]
    public void GetFlightLoadReport_ReturnsCorrectCapacitiesAndPercentages()
    {
        // Arrange (Подготовка)
        ReportService service = CreateReportService();

        // Рейс B2-701 вылетает 2026-11-01 13:00 (MSQ -> SVO)
        Flight flight = _dbContext.Flights
            .Include(f => f.Seats)
            .First(f => f.FlightNumber == "B2-701");

        FlightSeat businessSeat = flight.Seats.First(s => s.SeatClass == SeatClass.Business);
        FlightSeat economySeat = flight.Seats.First(s => s.SeatClass == SeatClass.Economy);

        Client client = new Client
        {
            FullName = "Иванов Иван Иванович",
            DocumentNumber = "MC1234567"
        };
        _dbContext.Clients.Add(client);
        _dbContext.SaveChanges();

        Order order = new Order
        {
            OrderNumber = "ORD-TEST-LOAD",
            Channel = OrderChannel.Cashier,
            CreatedRole = UserRole.Cashier,
            Status = OrderStatus.Paid,
            TotalAmount = 25000m,
            CreatedAt = new DateTime(2026, 11, 1, 9, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 9, 15, 0)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        Ticket ticket1 = new Ticket
        {
            TicketNumber = "TKT-LOAD-01",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = businessSeat.Id,
            Price = 15000m,
            Status = TicketStatus.Paid
        };

        Ticket ticket2 = new Ticket
        {
            TicketNumber = "TKT-LOAD-02",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = economySeat.Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };

        _dbContext.Tickets.AddRange(ticket1, ticket2);
        _dbContext.SaveChanges();

        DateTime fromDate = new DateTime(2026, 11, 1, 12, 0, 0);
        DateTime toDate = new DateTime(2026, 11, 1, 14, 0, 0);

        // Act (Действие)
        FlightLoadReportDto report = service.GetFlightLoadReport(fromDate, toDate);

        // Assert (Проверка)
        Assert.NotNull(report);
        Assert.Equal(1, report.TotalFlights);

        FlightLoadItemDto item = report.Items.First(i => i.FlightId == flight.Id);
        Assert.Equal("B2-701", item.FlightNumber);
        Assert.Equal("MSQ -> SVO", item.Route);
        Assert.Equal(flight.Seats.Count, item.TotalSeats);
        Assert.Equal(2, item.SoldSeats);

        // Бизнес-класс
        int expectedBusinessTotal = flight.Seats.Count(s => s.SeatClass == SeatClass.Business);
        Assert.Equal(expectedBusinessTotal, item.BusinessSeatsTotal);
        Assert.Equal(1, item.BusinessSeatsSold);
        double expectedBusinessLoad = Math.Round(1.0 / expectedBusinessTotal * 100.0, 1);
        Assert.Equal(expectedBusinessLoad, item.BusinessLoadPercentage);

        // Эконом-класс
        int expectedEconomyTotal = flight.Seats.Count(s => s.SeatClass == SeatClass.Economy);
        Assert.Equal(expectedEconomyTotal, item.EconomySeatsTotal);
        Assert.Equal(1, item.EconomySeatsSold);
        double expectedEconomyLoad = Math.Round(1.0 / expectedEconomyTotal * 100.0, 1);
        Assert.Equal(expectedEconomyLoad, item.EconomyLoadPercentage);

        // Общие итоги
        Assert.Equal(flight.Seats.Count, report.TotalCapacity);
        Assert.Equal(2, report.TotalSoldSeats);
        double expectedOverallLoad = Math.Round(2.0 / flight.Seats.Count * 100.0, 1);
        Assert.Equal(expectedOverallLoad, report.AverageLoadPercentage);
    }

    [Fact]
    public void GetRevenueByRoutesReport_CalculatesGrossNetRevenueAndRefunds()
    {
        // Arrange (Подготовка)
        ReportService service = CreateReportService();

        Flight flight = _dbContext.Flights
            .Include(f => f.Seats)
            .First(f => f.FlightNumber == "B2-701"); // MSQ -> SVO, 2026-11-01

        FlightSeat seat1 = flight.Seats.First();
        FlightSeat seat2 = flight.Seats.Skip(1).First();

        Client client = new Client
        {
            FullName = "Петров Петр Петрович",
            DocumentNumber = "AB7654321"
        };
        _dbContext.Clients.Add(client);
        _dbContext.SaveChanges();

        Order order = new Order
        {
            OrderNumber = "ORD-TEST-REV",
            Channel = OrderChannel.Cashier,
            CreatedRole = UserRole.Cashier,
            Status = OrderStatus.PartiallyRefunded,
            TotalAmount = 25000m,
            CreatedAt = new DateTime(2026, 11, 1, 9, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 9, 15, 0)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        Ticket ticketPaid = new Ticket
        {
            TicketNumber = "TKT-REV-PAID",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = seat1.Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };

        Ticket ticketRefunded = new Ticket
        {
            TicketNumber = "TKT-REV-REF",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = seat2.Id,
            Price = 15000m,
            Status = TicketStatus.Refunded
        };

        _dbContext.Tickets.AddRange(ticketPaid, ticketRefunded);
        _dbContext.SaveChanges();

        Refund refund = new Refund
        {
            TicketId = ticketRefunded.Id,
            RefundPercent = 90,
            Amount = 13500m, // 90% от 15 000
            Reason = RefundReason.Voluntary,
            CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0)
        };
        _dbContext.Refunds.Add(refund);
        _dbContext.SaveChanges();

        DateTime fromDate = new DateTime(2026, 11, 1, 0, 0, 0);
        DateTime toDate = new DateTime(2026, 11, 1, 23, 59, 59);

        // Act (Действие)
        RouteRevenueReportDto report = service.GetRevenueByRoutesReport(fromDate, toDate);

        // Assert (Проверка)
        Assert.NotNull(report);
        Assert.Equal(2, report.TotalTicketsSold);
        Assert.Equal(25000m, report.TotalGrossRevenue);
        Assert.Equal(13500m, report.TotalRefundedAmount);
        Assert.Equal(11500m, report.TotalNetRevenue);

        RouteRevenueItemDto item = Assert.Single(report.Items);
        Assert.Equal("MSQ -> SVO", item.Route);
        Assert.Equal("Минск", item.OriginCity);
        Assert.Equal("Москва", item.DestinationCity);
        Assert.Equal(2, item.TicketsSold);
        Assert.Equal(25000m, item.GrossRevenue);
        Assert.Equal(13500m, item.RefundedAmount);
        Assert.Equal(11500m, item.NetRevenue);
    }

    [Fact]
    public void GetSalesByChannelsReport_GroupsSalesBySelfServiceAndCashier()
    {
        // Arrange (Подготовка)
        ReportService service = CreateReportService();

        Flight flight = _dbContext.Flights
            .Include(f => f.Seats)
            .First(f => f.FlightNumber == "B2-701");

        Client client = new Client
        {
            FullName = "Сидоров Сидор Сидорович",
            DocumentNumber = "KH9876543"
        };
        _dbContext.Clients.Add(client);
        _dbContext.SaveChanges();

        // Заказ 1: Касса, 20 000 руб, 2 билета
        Order orderCashier = new Order
        {
            OrderNumber = "ORD-CH-CASHIER",
            Channel = OrderChannel.Cashier,
            CreatedRole = UserRole.Cashier,
            Status = OrderStatus.Paid,
            TotalAmount = 20000m,
            CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 10, 15, 0)
        };
        _dbContext.Orders.Add(orderCashier);
        _dbContext.SaveChanges();

        Ticket t1 = new Ticket
        {
            TicketNumber = "TKT-CH-1",
            OrderId = orderCashier.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(0).Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };
        Ticket t2 = new Ticket
        {
            TicketNumber = "TKT-CH-2",
            OrderId = orderCashier.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(1).Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };
        _dbContext.Tickets.AddRange(t1, t2);

        // Заказ 2: Самообслуживание, 30 000 руб, 3 билета
        Order orderSelf = new Order
        {
            OrderNumber = "ORD-CH-SELF",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            Status = OrderStatus.Paid,
            TotalAmount = 30000m,
            CreatedAt = new DateTime(2026, 11, 1, 11, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 11, 15, 0)
        };
        _dbContext.Orders.Add(orderSelf);
        _dbContext.SaveChanges();

        Ticket t3 = new Ticket
        {
            TicketNumber = "TKT-CH-3",
            OrderId = orderSelf.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(2).Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };
        Ticket t4 = new Ticket
        {
            TicketNumber = "TKT-CH-4",
            OrderId = orderSelf.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(3).Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };
        Ticket t5 = new Ticket
        {
            TicketNumber = "TKT-CH-5",
            OrderId = orderSelf.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(4).Id,
            Price = 10000m,
            Status = TicketStatus.Paid
        };
        _dbContext.Tickets.AddRange(t3, t4, t5);
        _dbContext.SaveChanges();

        DateTime fromDate = new DateTime(2026, 11, 1, 0, 0, 0);
        DateTime toDate = new DateTime(2026, 11, 1, 23, 59, 59);

        // Act (Действие)
        ChannelSalesReportDto report = service.GetSalesByChannelsReport(fromDate, toDate);

        // Assert (Проверка)
        Assert.NotNull(report);
        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(5, report.TotalTickets);
        Assert.Equal(50000m, report.TotalRevenue);
        Assert.Equal(2, report.Items.Count);

        ChannelSalesItemDto cashierItem = report.Items.First(i => i.Channel == OrderChannel.Cashier);
        Assert.Equal("Касса", cashierItem.ChannelName);
        Assert.Equal(1, cashierItem.OrdersCount);
        Assert.Equal(2, cashierItem.TicketsCount);
        Assert.Equal(20000m, cashierItem.TotalSalesAmount);
        Assert.Equal(40.0, cashierItem.PercentageOfTotalRevenue);

        ChannelSalesItemDto selfItem = report.Items.First(i => i.Channel == OrderChannel.SelfService);
        Assert.Equal("Самообслуживание", selfItem.ChannelName);
        Assert.Equal(1, selfItem.OrdersCount);
        Assert.Equal(3, selfItem.TicketsCount);
        Assert.Equal(30000m, selfItem.TotalSalesAmount);
        Assert.Equal(60.0, selfItem.PercentageOfTotalRevenue);
    }

    [Fact]
    public void GetRefundsStatisticsReport_DifferentiatesVoluntaryAndFlightCancellationRefunds()
    {
        // Arrange (Подготовка)
        ReportService service = CreateReportService();

        Flight flight = _dbContext.Flights
            .Include(f => f.Seats)
            .First(f => f.FlightNumber == "B2-701");

        Client client = new Client
        {
            FullName = "Ковалёв Алексей Сергеевич",
            DocumentNumber = "MP4455667"
        };
        _dbContext.Clients.Add(client);
        _dbContext.SaveChanges();

        Order order = new Order
        {
            OrderNumber = "ORD-REF-STAT",
            Channel = OrderChannel.Cashier,
            CreatedRole = UserRole.Cashier,
            Status = OrderStatus.Refunded,
            TotalAmount = 30000m,
            CreatedAt = new DateTime(2026, 11, 1, 8, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 8, 15, 0)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        // Билет 1: 10 000 руб, добровольный возврат (90% = 9000 руб, штраф 1000 руб)
        Ticket ticketVoluntary = new Ticket
        {
            TicketNumber = "TKT-REF-VOL",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(0).Id,
            Price = 10000m,
            Status = TicketStatus.Refunded
        };

        // Билет 2: 20 000 руб, вынужденный возврат из-за отмены (100% = 20000 руб, штраф 0 руб)
        Ticket ticketCancelled = new Ticket
        {
            TicketNumber = "TKT-REF-CAN",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.ElementAt(1).Id,
            Price = 20000m,
            Status = TicketStatus.Refunded
        };

        _dbContext.Tickets.AddRange(ticketVoluntary, ticketCancelled);
        _dbContext.SaveChanges();

        Refund refundVoluntary = new Refund
        {
            TicketId = ticketVoluntary.Id,
            RefundPercent = 90,
            Amount = 9000m,
            Reason = RefundReason.Voluntary,
            CreatedAt = new DateTime(2026, 11, 1, 10, 30, 0)
        };

        Refund refundCancelled = new Refund
        {
            TicketId = ticketCancelled.Id,
            RefundPercent = 100,
            Amount = 20000m,
            Reason = RefundReason.FlightCancelled,
            CreatedAt = new DateTime(2026, 11, 1, 12, 0, 0)
        };

        _dbContext.Refunds.AddRange(refundVoluntary, refundCancelled);
        _dbContext.SaveChanges();

        DateTime fromDate = new DateTime(2026, 11, 1, 0, 0, 0);
        DateTime toDate = new DateTime(2026, 11, 1, 23, 59, 59);

        // Act (Действие)
        RefundStatisticsReportDto report = service.GetRefundsStatisticsReport(fromDate, toDate);

        // Assert (Проверка)
        Assert.NotNull(report);
        Assert.Equal(2, report.TotalRefundsCount);
        Assert.Equal(29000m, report.TotalRefundAmount);
        Assert.Equal(1000m, report.TotalPenaltyAmount);
        Assert.Equal(2, report.Items.Count);

        RefundStatisticsItemDto voluntaryItem = report.Items.First(i => i.Reason == RefundReason.Voluntary);
        Assert.Equal("Добровольный возврат", voluntaryItem.ReasonName);
        Assert.Equal(1, voluntaryItem.RefundsCount);
        Assert.Equal(9000m, voluntaryItem.TotalRefundAmount);
        Assert.Equal(1000m, voluntaryItem.TotalPenaltyAmount);

        RefundStatisticsItemDto cancelledItem = report.Items.First(i => i.Reason == RefundReason.FlightCancelled);
        Assert.Equal("Отмена рейса", cancelledItem.ReasonName);
        Assert.Equal(1, cancelledItem.RefundsCount);
        Assert.Equal(20000m, cancelledItem.TotalRefundAmount);
        Assert.Equal(0m, cancelledItem.TotalPenaltyAmount);
    }

    [Fact]
    public void GetFlightLoadReport_NoFlightsInPeriod_ReturnsEmptyReport()
    {
        // Arrange (Подготовка)
        ReportService service = CreateReportService();
        DateTime fromDate = new DateTime(2020, 1, 1);
        DateTime toDate = new DateTime(2020, 1, 31);

        // Act (Действие)
        FlightLoadReportDto report = service.GetFlightLoadReport(fromDate, toDate);

        // Assert (Проверка)
        Assert.NotNull(report);
        Assert.Equal(0, report.TotalFlights);
        Assert.Equal(0, report.TotalCapacity);
        Assert.Equal(0, report.TotalSoldSeats);
        Assert.Equal(0.0, report.AverageLoadPercentage);
        Assert.Empty(report.Items);
    }

    [Fact]
    public void GetRefundsStatisticsReport_NoRefundsInPeriod_ReturnsEmptyReport()
    {
        // Arrange (Подготовка)
        ReportService service = CreateReportService();
        DateTime fromDate = new DateTime(2020, 1, 1);
        DateTime toDate = new DateTime(2020, 1, 31);

        // Act (Действие)
        RefundStatisticsReportDto report = service.GetRefundsStatisticsReport(fromDate, toDate);

        // Assert (Проверка)
        Assert.NotNull(report);
        Assert.Equal(0, report.TotalRefundsCount);
        Assert.Equal(0m, report.TotalRefundAmount);
        Assert.Equal(0m, report.TotalPenaltyAmount);
        Assert.Empty(report.Items);
    }
}
