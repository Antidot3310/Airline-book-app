using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Booking;
using AirlineTicketOffice.Core.Contracts.Clients;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Extensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineTicketOffice.Core.Tests.Integration;

/// <summary>
/// Сквозные интеграционные тесты ядра «Авиакасса» и проверки DI-контейнера (M1–M15).
/// </summary>
public class CoreIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public CoreIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private ServiceProvider CreateConfiguredServiceProvider()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddAirlineCore(options => options.UseSqlite(_connection));

        ServiceProvider provider = services.BuildServiceProvider();
        provider.InitializeAirlineDatabase();

        return provider;
    }

    [Fact]
    public void AddAirlineCore_ResolvesAllRegisteredServices()
    {
        // Arrange (Подготовка контейнера)
        using ServiceProvider provider = CreateConfiguredServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Act & Assert (Разрешение всех 15 ключевых интерфейсов M1–M15)
        Assert.NotNull(sp.GetRequiredService<AirlineDbContext>());         // M13
        Assert.NotNull(sp.GetRequiredService<IVirtualClockService>());      // M12
        Assert.NotNull(sp.GetRequiredService<ISessionService>());          // M1
        Assert.NotNull(sp.GetRequiredService<IAuditLogService>());         // M11
        Assert.NotNull(sp.GetRequiredService<IClientService>());           // M2
        Assert.NotNull(sp.GetRequiredService<IDirectoryService>());        // M3
        Assert.NotNull(sp.GetRequiredService<IFlightService>());           // M4
        Assert.NotNull(sp.GetRequiredService<ITariffCalculator>());        // M5
        Assert.NotNull(sp.GetRequiredService<IBookingService>());          // M6
        Assert.NotNull(sp.GetRequiredService<IOrderTicketService>());      // M7
        Assert.NotNull(sp.GetRequiredService<IPaymentService>());          // M8
        Assert.NotNull(sp.GetRequiredService<IPaymentEmulator>());         // M14
        Assert.NotNull(sp.GetRequiredService<IRefundService>());           // M9
        Assert.NotNull(sp.GetRequiredService<IReportService>());           // M10
        Assert.NotNull(sp.GetRequiredService<IDocumentService>());         // M15
    }

    [Fact]
    public void FullLifecycle_FromBookingToPaymentTicketAndRefund_E2EScenario()
    {
        // 1) Инициализация через DI и запуск БД
        using ServiceProvider provider = CreateConfiguredServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        ISessionService sessionService = sp.GetRequiredService<ISessionService>();
        IFlightService flightService = sp.GetRequiredService<IFlightService>();
        IDirectoryService directoryService = sp.GetRequiredService<IDirectoryService>();
        IBookingService bookingService = sp.GetRequiredService<IBookingService>();
        IOrderTicketService orderTicketService = sp.GetRequiredService<IOrderTicketService>();
        IPaymentService paymentService = sp.GetRequiredService<IPaymentService>();
        IDocumentService documentService = sp.GetRequiredService<IDocumentService>();
        IRefundService refundService = sp.GetRequiredService<IRefundService>();
        IReportService reportService = sp.GetRequiredService<IReportService>();
        IAuditLogService auditLogService = sp.GetRequiredService<IAuditLogService>();
        IVirtualClockService clockService = sp.GetRequiredService<IVirtualClockService>();

        // 2) M1 (Сессия кассира)
        sessionService.OpenSession(UserRole.Cashier);
        Assert.True(sessionService.IsSessionActive);
        Assert.Equal(UserRole.Cashier, sessionService.CurrentRole);
        Assert.True(sessionService.HasPermission(SessionPermissions.CreateBooking));
        Assert.True(sessionService.HasPermission(SessionPermissions.AcceptCardPayment));

        // 3) M4 (Поиск рейса SVO -> LED на 2026-11-02)
        IReadOnlyList<AirportDto> airports = directoryService.GetAllAirports();
        AirportDto svo = airports.First(a => a.Code == "SVO");
        AirportDto led = airports.First(a => a.Code == "LED");

        IReadOnlyList<FlightListItemDto> foundFlights = flightService.SearchFlights(
            svo.Id,
            led.Id,
            new DateTime(2026, 11, 2),
            passengersCount: 2);

        Assert.NotEmpty(foundFlights);
        FlightListItemDto flight = foundFlights.First();
        Assert.Equal("SU-101", flight.FlightNumber);
        Assert.True(flight.EconomySeatsAvailable > 0 && flight.BusinessSeatsAvailable > 0);

        // 4) M6 (Схема мест и удержание)
        Result<SeatMapDto> seatMapResult = bookingService.GetSeatMap(flight.Id);
        Assert.True(seatMapResult.IsSuccess);
        Assert.Contains(seatMapResult.Value.Seats, s => s.SeatNumber == "1A" && s.State == SeatState.Available);
        Assert.Contains(seatMapResult.Value.Seats, s => s.SeatNumber == "4A" && s.State == SeatState.Available);

        Result<HoldSeatsResultDto> holdResult = bookingService.HoldSeats(flight.Id, ["1A", "4A"]);
        Assert.True(holdResult.IsSuccess);
        Assert.Equal(2, holdResult.Value.SeatNumbers.Count);

        // 5) M7 (Оформление заказа на 2 пассажиров: 1 с багажом, 2 без багажа)
        CreateOrderRequest orderRequest = new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new PassengerOrderItemDto
                {
                    FullName = "Иванов Иван Иванович",
                    DocumentNumber = "1111 222333",
                    ContactPhone = "+7 999 111-22-33",
                    ContactEmail = "ivanov@test.ru",
                    SeatNumber = "1A",
                    HasBaggage = true // +1500 руб.
                },
                new PassengerOrderItemDto
                {
                    FullName = "Петров Петр Петрович",
                    DocumentNumber = "4444 555666",
                    ContactPhone = "+7 999 444-55-66",
                    ContactEmail = "petrov@test.ru",
                    SeatNumber = "4A",
                    HasBaggage = false
                }
            ]
        };

        Result<OrderDto> orderResult = orderTicketService.CreateOrderWithBooking(orderRequest);
        Assert.True(orderResult.IsSuccess);
        OrderDto order = orderResult.Value;
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(2, order.Tickets.Count);
        Assert.All(order.Tickets, t => Assert.Equal(TicketStatus.Booked, t.Status));

        TicketDto businessTicket = order.Tickets.First(t => t.SeatNumber == "1A");
        TicketDto economyTicket = order.Tickets.First(t => t.SeatNumber == "4A");
        Assert.True(businessTicket.HasBaggage);
        Assert.False(economyTicket.HasBaggage);

        // 6) M8 (Оплата заказа картой)
        CardDetailsDto card = new CardDetailsDto
        {
            CardNumber = "4276 1234 5678 9012",
            CardHolder = "IVAN IVANOV",
            ExpiryMonth = "12",
            ExpiryYear = "29",
            Cvv = "123"
        };

        Result<PaymentResultDto> payResult = paymentService.PayWithCard(order.Id, card);
        Assert.True(payResult.IsSuccess);
        PaymentResultDto payment = payResult.Value;
        Assert.Equal(PaymentStatus.Success, payment.Status);

        Result<OrderDto> paidOrderResult = orderTicketService.GetOrderById(order.Id);
        Assert.True(paidOrderResult.IsSuccess);
        OrderDto paidOrder = paidOrderResult.Value;
        Assert.Equal(OrderStatus.Paid, paidOrder.Status);
        Assert.All(paidOrder.Tickets, t => Assert.Equal(TicketStatus.Paid, t.Status));

        // 7) M15 (Печать документов: маршрутная квитанция и чек оплаты)
        PrintDocumentDto ticketDoc = documentService.GenerateTicketDocument(businessTicket.Id);
        Assert.NotNull(ticketDoc);
        Assert.Equal("Маршрутная квитанция электронного билета", ticketDoc.Title);
        Assert.Equal(businessTicket.TicketNumber, ticketDoc.DocumentNumber);
        Assert.Contains(businessTicket.PassengerName, ticketDoc.FormattedContent);
        Assert.Contains(flight.FlightNumber, ticketDoc.FormattedContent);
        Assert.Contains(businessTicket.SeatNumber, ticketDoc.FormattedContent);
        Assert.Contains(businessTicket.Price.ToString("N2"), ticketDoc.FormattedContent);

        PrintDocumentDto receiptDoc = documentService.GeneratePaymentReceipt(payment.PaymentId);
        Assert.NotNull(receiptDoc);
        Assert.Equal("Кассовый чек", receiptDoc.Title);
        Assert.Contains(payment.Amount.ToString("N2"), receiptDoc.FormattedContent);
        Assert.Contains("Банковская карта", receiptDoc.FormattedContent);
        Assert.Contains("Успешно оплачено", receiptDoc.FormattedContent);

        // 8) M9 (Добровольный возврат одного билета: эконом 4A)
        Result<RefundResultDto> refundResult = refundService.ProcessRefund(economyTicket.Id);
        Assert.True(refundResult.IsSuccess);
        RefundResultDto refund = refundResult.Value;
        Assert.Equal(90, refund.RefundPercent); // За 24+ часов до вылета -> возврат 90%
        Assert.Equal(OrderStatus.PartiallyRefunded, refund.UpdatedOrderStatus);

        // Проверяем освобождение возвращённого места для повторной продажи
        Result<SeatMapDto> seatMapAfterRefund = bookingService.GetSeatMap(flight.Id);
        Assert.True(seatMapAfterRefund.IsSuccess);
        SeatItemDto seat4A = seatMapAfterRefund.Value.Seats.First(s => s.SeatNumber == "4A");
        Assert.Equal(SeatState.Available, seat4A.State);

        // 9) M10 (Аналитический отчёт)
        FlightLoadReportDto loadReport = reportService.GetFlightLoadReport(
            new DateTime(2026, 11, 2, 0, 0, 0),
            new DateTime(2026, 11, 2, 23, 59, 59));
        Assert.NotNull(loadReport);
        Assert.NotEmpty(loadReport.Items);
        FlightLoadItemDto flightLoad = loadReport.Items.First(i => i.FlightId == flight.Id);
        Assert.Equal(1, flightLoad.SoldSeats); // 1 оплаченный билет остался (второй возвращён)

        RouteRevenueReportDto routeReport = reportService.GetRevenueByRoutesReport(
            new DateTime(2026, 11, 2, 0, 0, 0),
            new DateTime(2026, 11, 2, 23, 59, 59));
        Assert.NotNull(routeReport);
        RouteRevenueItemDto routeItem = routeReport.Items.First(i => i.Route == $"{svo.Code} -> {led.Code}");
        Assert.Equal(2, routeItem.TicketsSold);
        Assert.Equal(economyTicket.Price * 0.9m, routeItem.RefundedAmount);
        Assert.Equal(routeItem.GrossRevenue - routeItem.RefundedAmount, routeItem.NetRevenue);

        // 10) M11 (Журнал аудита)
        IReadOnlyList<AuditLogDto> logs = auditLogService.GetLogs(null, null, null, null);
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.ActionType == "OpenSession");
        Assert.Contains(logs, l => l.ActionType == "CreateOrder");
        Assert.Contains(logs, l => l.ActionType == "PaymentSuccess");
        Assert.Contains(logs, l => l.ActionType == "ProcessRefund");
    }
}
