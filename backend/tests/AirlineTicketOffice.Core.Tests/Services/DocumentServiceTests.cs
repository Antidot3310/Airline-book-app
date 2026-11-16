using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Documents;
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
/// Модульные тесты для сервиса формирования документов и чеков <see cref="DocumentService"/> (M15).
/// </summary>
public class DocumentServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;
    private readonly VirtualClockService _clockService;

    public DocumentServiceTests()
    {
        ServiceCollection services = new ServiceCollection();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<AirlineDbContext>();
        _dbContext.Database.EnsureCreated();
        DbInitializer.Initialize(_dbContext);

        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        _clockService = new VirtualClockService(_scopeFactory);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private DocumentService CreateDocumentService()
    {
        return new DocumentService(_dbContext, _clockService);
    }

    [Fact]
    public void GenerateTicketDocument_WhenTicketExists_ReturnsFormattedDocument()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        Flight flight = _dbContext.Flights
            .Include(f => f.Seats)
            .First(f => f.FlightNumber == "B2-701");

        FlightSeat seat = flight.Seats.First(s => s.SeatClass == SeatClass.Business);

        Client client = new Client
        {
            FullName = "Иванов Иван Иванович",
            DocumentNumber = "MC9988776",
            ContactPhone = "+7 (999) 111-22-33",
            ContactEmail = "ivanov@test.ru"
        };
        _dbContext.Clients.Add(client);
        _dbContext.SaveChanges();

        Order order = new Order
        {
            OrderNumber = "ORD-DOC-TKT",
            Channel = OrderChannel.Cashier,
            CreatedRole = UserRole.Cashier,
            Status = OrderStatus.Paid,
            TotalAmount = 15000m,
            CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 10, 15, 0)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        Ticket ticket = new Ticket
        {
            TicketNumber = "TKT-DOC-1001",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = seat.Id,
            Price = 15000m,
            HasBaggage = true,
            Status = TicketStatus.Paid
        };
        _dbContext.Tickets.Add(ticket);
        _dbContext.SaveChanges();

        // Act (Действие)
        PrintDocumentDto doc = service.GenerateTicketDocument(ticket.Id);

        // Assert (Проверка)
        Assert.NotNull(doc);
        Assert.Equal("Маршрутная квитанция электронного билета", doc.Title);
        Assert.Equal("TKT-DOC-1001", doc.DocumentNumber);
        Assert.Contains("Иванов Иван Иванович", doc.FormattedContent);
        Assert.Contains("MC9988776", doc.FormattedContent);
        Assert.Contains("B2-701", doc.FormattedContent);
        Assert.Contains("MSQ", doc.FormattedContent);
        Assert.Contains("SVO", doc.FormattedContent);
        Assert.Contains(seat.SeatNumber, doc.FormattedContent);
        Assert.Contains("Бизнес-класс", doc.FormattedContent);
        Assert.Contains(ticket.Price.ToString("N2"), doc.FormattedContent);
        Assert.Contains("Оплачен", doc.FormattedContent);
        Assert.Contains("ORD-DOC-TKT", doc.FormattedContent);
    }

    [Fact]
    public void GenerateTicketDocument_WhenTicketNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        // Act & Assert (Действие и проверка)
        Assert.Throws<KeyNotFoundException>(() => service.GenerateTicketDocument(999999));
    }

    [Fact]
    public void GeneratePaymentReceipt_WhenPaymentExists_ReturnsFormattedReceipt()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        Order order = new Order
        {
            OrderNumber = "ORD-DOC-PAY",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            Status = OrderStatus.Paid,
            TotalAmount = 25000m,
            CreatedAt = new DateTime(2026, 11, 1, 11, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 11, 15, 0)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        Payment payment = new Payment
        {
            OrderId = order.Id,
            Amount = 25000m,
            PaymentMethod = PaymentMethod.Card,
            Status = PaymentStatus.Success,
            ExternalTransactionId = "TXN-TEST-998877",
            CreatedAt = new DateTime(2026, 11, 1, 11, 5, 0)
        };
        _dbContext.Payments.Add(payment);
        _dbContext.SaveChanges();

        // Act (Действие)
        PrintDocumentDto receipt = service.GeneratePaymentReceipt(payment.Id);

        // Assert (Проверка)
        Assert.NotNull(receipt);
        Assert.Equal("Кассовый чек", receipt.Title);
        Assert.Equal($"CHK-{payment.Id:D6}", receipt.DocumentNumber);
        Assert.Contains("ORD-DOC-PAY", receipt.FormattedContent);
        Assert.Contains("Банковская карта", receipt.FormattedContent);
        Assert.Contains("Успешно оплачено", receipt.FormattedContent);
        Assert.Contains(payment.Amount.ToString("N2"), receipt.FormattedContent);
        Assert.Contains("TXN-TEST-998877", receipt.FormattedContent);
        Assert.Contains("7701234567", receipt.FormattedContent); // ИНН
    }

    [Fact]
    public void GeneratePaymentReceipt_WhenPaymentNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        // Act & Assert (Действие и проверка)
        Assert.Throws<KeyNotFoundException>(() => service.GeneratePaymentReceipt(999999));
    }

    [Fact]
    public void GenerateRefundReceipt_WhenRefundExists_ReturnsFormattedReceipt()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        Flight flight = _dbContext.Flights
            .Include(f => f.Seats)
            .First(f => f.FlightNumber == "B2-701");

        Client client = new Client
        {
            FullName = "Смирнова Ольга Павловна",
            DocumentNumber = "RR1122334"
        };
        _dbContext.Clients.Add(client);
        _dbContext.SaveChanges();

        Order order = new Order
        {
            OrderNumber = "ORD-DOC-REF",
            Channel = OrderChannel.Cashier,
            CreatedRole = UserRole.Cashier,
            Status = OrderStatus.Refunded,
            TotalAmount = 20000m,
            CreatedAt = new DateTime(2026, 11, 1, 12, 0, 0),
            HoldExpiresAt = new DateTime(2026, 11, 1, 12, 15, 0)
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        Ticket ticket = new Ticket
        {
            TicketNumber = "TKT-REF-DOC",
            OrderId = order.Id,
            ClientId = client.Id,
            FlightId = flight.Id,
            FlightSeatId = flight.Seats.First().Id,
            Price = 20000m,
            Status = TicketStatus.Refunded
        };
        _dbContext.Tickets.Add(ticket);
        _dbContext.SaveChanges();

        Refund refund = new Refund
        {
            TicketId = ticket.Id,
            RefundPercent = 90,
            Amount = 18000m,
            Reason = RefundReason.Voluntary,
            CreatedAt = new DateTime(2026, 11, 1, 13, 0, 0)
        };
        _dbContext.Refunds.Add(refund);
        _dbContext.SaveChanges();

        // Act (Действие)
        PrintDocumentDto receipt = service.GenerateRefundReceipt(refund.Id);

        // Assert (Проверка)
        Assert.NotNull(receipt);
        Assert.Equal("Квитанция о возврате билета", receipt.Title);
        Assert.Equal($"REF-{refund.Id:D6}", receipt.DocumentNumber);
        Assert.Contains("TKT-REF-DOC", receipt.FormattedContent);
        Assert.Contains("Смирнова Ольга Павловна", receipt.FormattedContent);
        Assert.Contains(ticket.Price.ToString("N2"), receipt.FormattedContent); // Исходная стоимость
        Assert.Contains("90%", receipt.FormattedContent); // Процент
        Assert.Contains(refund.Amount.ToString("N2"), receipt.FormattedContent); // Выплата
        Assert.Contains((ticket.Price - refund.Amount).ToString("N2"), receipt.FormattedContent); // Штраф
        Assert.Contains("Добровольный возврат пассажира", receipt.FormattedContent);
    }

    [Fact]
    public void GenerateRefundReceipt_WhenRefundNotFound_ThrowsKeyNotFoundException()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        // Act & Assert (Действие и проверка)
        Assert.Throws<KeyNotFoundException>(() => service.GenerateRefundReceipt(999999));
    }

    [Fact]
    public void GenerateReportDocument_WithFlightLoadReport_ReturnsStructuredReport()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        FlightLoadReportDto loadReport = new FlightLoadReportDto
        {
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 2),
            TotalFlights = 1,
            TotalCapacity = 189,
            TotalSoldSeats = 45,
            AverageLoadPercentage = 23.8,
            Items =
            [
                new FlightLoadItemDto
                {
                    FlightId = 1,
                    FlightNumber = "B2-701",
                    Route = "MSQ -> SVO",
                    DepartureTime = new DateTime(2026, 11, 1, 13, 0, 0),
                    AircraftModel = "B737",
                    TotalSeats = 189,
                    SoldSeats = 45,
                    LoadPercentage = 23.8,
                    BusinessSeatsTotal = 12,
                    BusinessSeatsSold = 6,
                    BusinessLoadPercentage = 50.0,
                    EconomySeatsTotal = 177,
                    EconomySeatsSold = 39,
                    EconomyLoadPercentage = 22.0
                }
            ]
        };

        // Act (Действие)
        PrintDocumentDto doc = service.GenerateReportDocument("Отчёт по загрузке рейсов", loadReport);

        // Assert (Проверка)
        Assert.NotNull(doc);
        Assert.Equal("Отчёт по загрузке рейсов", doc.Title);
        Assert.StartsWith("REP-", doc.DocumentNumber);
        Assert.Contains("ОТЧЁТ ПО ЗАГРУЗКЕ РЕЙСОВ", doc.FormattedContent);
        Assert.Contains("B2-701", doc.FormattedContent);
        Assert.Contains("MSQ -> SVO", doc.FormattedContent);
        Assert.Contains("СВОДНЫЕ ИТОГИ", doc.FormattedContent);
        Assert.Contains($"{loadReport.AverageLoadPercentage:F1}%", doc.FormattedContent);
    }

    [Fact]
    public void GenerateReportDocument_WithRouteRevenueReport_ReturnsStructuredReport()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        RouteRevenueReportDto revenueReport = new RouteRevenueReportDto
        {
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 2),
            TotalTicketsSold = 10,
            TotalGrossRevenue = 100000m,
            TotalRefundedAmount = 15000m,
            TotalNetRevenue = 85000m,
            Items =
            [
                new RouteRevenueItemDto
                {
                    Route = "MSQ -> SVO",
                    OriginCity = "Минск",
                    DestinationCity = "Москва",
                    TicketsSold = 10,
                    GrossRevenue = 100000m,
                    RefundedAmount = 15000m,
                    NetRevenue = 85000m
                }
            ]
        };

        // Act (Действие)
        PrintDocumentDto doc = service.GenerateReportDocument("Отчёт по выручке по маршрутам", revenueReport);

        // Assert (Проверка)
        Assert.NotNull(doc);
        Assert.Equal("Отчёт по выручке по маршрутам", doc.Title);
        Assert.Contains("MSQ -> SVO", doc.FormattedContent);
        Assert.Contains(revenueReport.TotalGrossRevenue.ToString("N2"), doc.FormattedContent);
        Assert.Contains(revenueReport.TotalNetRevenue.ToString("N2"), doc.FormattedContent);
    }

    [Fact]
    public void GenerateReportDocument_WithChannelSalesReport_ReturnsStructuredReport()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        ChannelSalesReportDto salesReport = new ChannelSalesReportDto
        {
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 2),
            TotalOrders = 5,
            TotalTickets = 8,
            TotalRevenue = 75000m,
            Items =
            [
                new ChannelSalesItemDto
                {
                    Channel = OrderChannel.Cashier,
                    ChannelName = "Касса",
                    OrdersCount = 2,
                    TicketsCount = 3,
                    TotalSalesAmount = 30000m,
                    PercentageOfTotalRevenue = 40.0
                },
                new ChannelSalesItemDto
                {
                    Channel = OrderChannel.SelfService,
                    ChannelName = "Самообслуживание",
                    OrdersCount = 3,
                    TicketsCount = 5,
                    TotalSalesAmount = 45000m,
                    PercentageOfTotalRevenue = 60.0
                }
            ]
        };

        // Act (Действие)
        PrintDocumentDto doc = service.GenerateReportDocument("Продажи по каналам", salesReport);

        // Assert (Проверка)
        Assert.NotNull(doc);
        Assert.Equal("Продажи по каналам", doc.Title);
        Assert.Contains("Касса", doc.FormattedContent);
        Assert.Contains("Самообслуживание", doc.FormattedContent);
        Assert.Contains(salesReport.TotalRevenue.ToString("N2"), doc.FormattedContent);
    }

    [Fact]
    public void GenerateReportDocument_WithRefundStatisticsReport_ReturnsStructuredReport()
    {
        // Arrange (Подготовка)
        DocumentService service = CreateDocumentService();

        RefundStatisticsReportDto refundReport = new RefundStatisticsReportDto
        {
            FromDate = new DateTime(2026, 11, 1),
            ToDate = new DateTime(2026, 11, 2),
            TotalRefundsCount = 2,
            TotalRefundAmount = 28000m,
            TotalPenaltyAmount = 2000m,
            Items =
            [
                new RefundStatisticsItemDto
                {
                    Reason = RefundReason.Voluntary,
                    ReasonName = "Добровольный возврат",
                    RefundsCount = 1,
                    TotalRefundAmount = 18000m,
                    TotalPenaltyAmount = 2000m
                },
                new RefundStatisticsItemDto
                {
                    Reason = RefundReason.FlightCancelled,
                    ReasonName = "Отмена рейса",
                    RefundsCount = 1,
                    TotalRefundAmount = 10000m,
                    TotalPenaltyAmount = 0m
                }
            ]
        };

        // Act (Действие)
        PrintDocumentDto doc = service.GenerateReportDocument("Статистика возвратов", refundReport);

        // Assert (Проверка)
        Assert.NotNull(doc);
        Assert.Equal("Статистика возвратов", doc.Title);
        Assert.Contains("Добровольный возврат", doc.FormattedContent);
        Assert.Contains("Отмена рейса", doc.FormattedContent);
        Assert.Contains(refundReport.TotalRefundAmount.ToString("N2"), doc.FormattedContent);
        Assert.Contains(refundReport.TotalPenaltyAmount.ToString("N2"), doc.FormattedContent);
    }
}
