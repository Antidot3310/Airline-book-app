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
using RefundsStatisticsReportDto = AirlineTicketOffice.Core.Contracts.Reports.RefundStatisticsReportDto;
using SalesByChannelsReportDto = AirlineTicketOffice.Core.Contracts.Reports.ChannelSalesReportDto;

namespace AirlineTicketOffice.Core.Tests.Integration;

/// <summary>
/// Полный сквозной приёмочный тест на 10 шагов эталонного сценария демонстрации
/// автоматизированного рабочего места «Авиакасса» (docs/01_OVERVIEW.md, Раздел 4).
/// </summary>
public class EndToEndDemoScenarioTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public EndToEndDemoScenarioTests()
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
    public void TenStepDemoScenario_ExecutesCompleteBusinessLifecycle_PassesSuccessfully()
    {
        using ServiceProvider provider = CreateConfiguredServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IServiceProvider sp = scope.ServiceProvider;

        // Разрешение сервисов ядра
        ISessionService sessionService = sp.GetRequiredService<ISessionService>();
        IVirtualClockService clockService = sp.GetRequiredService<IVirtualClockService>();
        IDirectoryService directoryService = sp.GetRequiredService<IDirectoryService>();
        ITariffCalculator tariffCalculator = sp.GetRequiredService<ITariffCalculator>();
        IFlightService flightService = sp.GetRequiredService<IFlightService>();
        IClientService clientService = sp.GetRequiredService<IClientService>();
        IBookingService bookingService = sp.GetRequiredService<IBookingService>();
        IOrderTicketService orderTicketService = sp.GetRequiredService<IOrderTicketService>();
        IPaymentService paymentService = sp.GetRequiredService<IPaymentService>();
        IPaymentEmulator paymentEmulator = sp.GetRequiredService<IPaymentEmulator>();
        IRefundService refundService = sp.GetRequiredService<IRefundService>();
        IReportService reportService = sp.GetRequiredService<IReportService>();
        IDocumentService documentService = sp.GetRequiredService<IDocumentService>();
        IAuditLogService auditLogService = sp.GetRequiredService<IAuditLogService>();

        DateTime t0 = clockService.CurrentVirtualTime;

        // =========================================================================
        // ШАГ 1. Администратор: вход, создание нового рейса и фиксация тарифа
        // =========================================================================
        Result sessionAdmin = sessionService.OpenSession(UserRole.Administrator, "Администратор Системы", "admin@airline.local");
        Assert.True(sessionAdmin.IsSuccess);
        Assert.Equal(UserRole.Administrator, sessionService.CurrentRole);

        AirportDto svo = directoryService.GetAllAirports().First(a => a.Code == "SVO");
        AirportDto aer = directoryService.GetAllAirports().First(a => a.Code == "AER");
        AircraftDto aircraft = directoryService.GetAllAircrafts().First(a => a.TailNumber == "RA-89014" && a.Status == AircraftStatus.InService);

        Result tariffResult = tariffCalculator.SetBaseTariff(svo.Id, aer.Id, 6000m, 1500m);
        Assert.True(tariffResult.IsSuccess);

        DateTime flightDeparture = t0.AddDays(3); // Вылет через 72 часа
        DateTime flightArrival = flightDeparture.AddHours(4);

        CreateFlightRequest createFlightReq = new CreateFlightRequest
        {
            FlightNumber = "SU-101",
            OriginAirportId = svo.Id,
            DestinationAirportId = aer.Id,
            AircraftId = aircraft.Id,
            DepartureTime = flightDeparture,
            ArrivalTime = flightArrival
        };

        Result<int> createFlightRes = flightService.CreateFlight(createFlightReq);
        Assert.True(createFlightRes.IsSuccess);
        Result<FlightDetailsDto> getFlightRes = flightService.GetFlightById(createFlightRes.Value);
        Assert.True(getFlightRes.IsSuccess);
        FlightDetailsDto flight = getFlightRes.Value;
        Assert.Equal("SU-101", flight.FlightNumber);

        Result<SeatMapDto> initialSeatMap = bookingService.GetSeatMap(flight.Id);
        Assert.True(initialSeatMap.IsSuccess);
        Assert.Equal(87, initialSeatMap.Value.TotalSeats);
        Assert.Equal(87, initialSeatMap.Value.AvailableSeats);
        Assert.All(initialSeatMap.Value.Seats, s => Assert.Equal(SeatState.Available, s.State));

        // =========================================================================
        // ШАГ 2. Пассажир: поиск рейса, бронирование места 1A с багажом (таймер 15 мин)
        // =========================================================================
        Result sessionPassenger = sessionService.OpenSession(UserRole.Passenger, "Иван Иванов", "ivan.ivanov@mail.ru");
        Assert.True(sessionPassenger.IsSuccess);

        Result<ClientDto> passengerClientRes = clientService.GetOrCreateClient(
            "Иван", "Иванов", "1111 222333", "ivan.ivanov@mail.ru", "+79991112233");
        Assert.True(passengerClientRes.IsSuccess);
        ClientDto passengerClient = passengerClientRes.Value;

        CreateOrderRequest passengerOrderReq = new CreateOrderRequest
        {
            FlightId = flight.Id,
            ClientId = passengerClient.Id,
            BookingChannel = BookingChannel.SelfService,
            Passengers = new List<PassengerOrderItemDto>
            {
                new PassengerOrderItemDto
                {
                    FirstName = "Иван",
                    LastName = "Иванов",
                    DocumentNumber = "1111 222333",
                    SeatNumber = "1A",
                    HasBaggage = true
                }
            }
        };

        Result<OrderDto> passengerOrderRes = orderTicketService.CreateOrderWithBooking(passengerOrderReq);
        Assert.True(passengerOrderRes.IsSuccess);
        OrderDto passengerOrder1 = passengerOrderRes.Value;
        Assert.Equal(OrderStatus.Booked, passengerOrder1.Status);
        Assert.True(passengerOrder1.ExpiresAt > clockService.CurrentVirtualTime);

        // Проверяем статус места 1A на схеме салона
        Result<SeatMapDto> seatMapStep2 = bookingService.GetSeatMap(flight.Id);
        SeatItemDto seat1A = seatMapStep2.Value.Seats.First(s => s.SeatNumber == "1A");
        Assert.Equal(SeatState.Booked, seat1A.State);

        // =========================================================================
        // ШАГ 3. Кассир: попытка занять 1A (ошибка двойной продажи), продажа 1C за наличные
        // =========================================================================
        Result sessionCashier = sessionService.OpenSession(UserRole.Cashier, "Анна Кассир", "cashier1@airline.local");
        Assert.True(sessionCashier.IsSuccess);

        Result<ClientDto> cashierClientRes = clientService.GetOrCreateClient(
            "Пётр", "Петров", "2222 333444", "petr@mail.ru", "+79992223344");
        Assert.True(cashierClientRes.IsSuccess);
        ClientDto cashierClient = cashierClientRes.Value;

        // Попытка забронировать уже удерживаемое место 1A
        CreateOrderRequest conflictOrderReq = new CreateOrderRequest
        {
            FlightId = flight.Id,
            ClientId = cashierClient.Id,
            BookingChannel = BookingChannel.Cashier,
            Passengers = new List<PassengerOrderItemDto>
            {
                new PassengerOrderItemDto
                {
                    FirstName = "Пётр",
                    LastName = "Петров",
                    DocumentNumber = "2222 333444",
                    SeatNumber = "1A",
                    HasBaggage = false
                }
            }
        };
        Result<OrderDto> conflictRes = orderTicketService.CreateOrderWithBooking(conflictOrderReq);
        Assert.True(conflictRes.IsFailure); // Защита от двойной продажи сработала!

        // Кассир выбирает соседнее свободное место 1C
        CreateOrderRequest cashierOrderReq = new CreateOrderRequest
        {
            FlightId = flight.Id,
            ClientId = cashierClient.Id,
            BookingChannel = BookingChannel.Cashier,
            Passengers = new List<PassengerOrderItemDto>
            {
                new PassengerOrderItemDto
                {
                    FirstName = "Пётр",
                    LastName = "Петров",
                    DocumentNumber = "2222 333444",
                    SeatNumber = "1C",
                    HasBaggage = false
                }
            }
        };
        Result<OrderDto> cashierOrderRes = orderTicketService.CreateOrderWithBooking(cashierOrderReq);
        Assert.True(cashierOrderRes.IsSuccess);
        OrderDto cashierOrder = cashierOrderRes.Value;

        // Продажа за наличные со сдачей
        decimal cashGiven = cashierOrder.TotalAmount + 500m;
        Result<PaymentResultDto> cashPayRes = paymentService.PayWithCash(cashierOrder.Id, cashGiven);
        Assert.True(cashPayRes.IsSuccess);
        Assert.Equal(500m, cashPayRes.Value.ChangeAmount);

        // Печать кассового чека кассира
        PrintDocumentDto cashReceipt = documentService.GeneratePaymentReceipt(cashPayRes.Value.PaymentId);
        Assert.NotNull(cashReceipt);
        Assert.Contains("Кассовый чек", cashReceipt.Title);
        Assert.Contains(cashierOrder.TotalAmount.ToString("N2"), cashReceipt.FormattedContent);

        TicketDto cashierTicket = cashierOrderRes.Value.Tickets.First();

        // =========================================================================
        // ШАГ 4. Эмулятор: сдвиг времени на +16 минут -> аннулирование брони 1A
        // =========================================================================
        clockService.AdvanceTime(TimeSpan.FromMinutes(16));

        Result<OrderDto> expiredOrderCheck = orderTicketService.GetOrderById(passengerOrder1.Id);
        Assert.True(expiredOrderCheck.IsSuccess);
        Assert.Equal(OrderStatus.Expired, expiredOrderCheck.Value.Status);

        Result<SeatMapDto> seatMapStep4 = bookingService.GetSeatMap(flight.Id);
        SeatItemDto seat1AAfterExpiry = seatMapStep4.Value.Seats.First(s => s.SeatNumber == "1A");
        Assert.Equal(SeatState.Available, seat1AAfterExpiry.State); // Место освободилось!

        // =========================================================================
        // ШАГ 5. Пассажир: повторная бронь, отказ шлюза, затем успешная оплата картой
        // =========================================================================
        sessionService.OpenSession(UserRole.Passenger, "Иван Иванов", "ivan.ivanov@mail.ru");

        Result<OrderDto> rebookedOrderRes = orderTicketService.CreateOrderWithBooking(passengerOrderReq);
        Assert.True(rebookedOrderRes.IsSuccess);
        OrderDto passengerOrder2 = rebookedOrderRes.Value;
        Assert.Equal(OrderStatus.Booked, passengerOrder2.Status);

        CardDetailsDto testCard = new CardDetailsDto
        {
            CardNumber = "4276 1234 5678 9012",
            CardHolder = "IVAN IVANOV",
            ExpiryMonth = "12",
            ExpiryYear = "29",
            Cvv = "123"
        };

        // 5.1 Имитация сбоя платежного шлюза
        paymentEmulator.SetResponseMode(GatewayResponseMode.AlwaysFail);
        Result<PaymentResultDto> failPaymentRes = paymentService.PayWithCard(passengerOrder2.Id, testCard);
        Assert.True(failPaymentRes.IsFailure);

        // Бронь остаётся активной
        Result<OrderDto> orderStillBooked = orderTicketService.GetOrderById(passengerOrder2.Id);
        Assert.Equal(OrderStatus.Booked, orderStillBooked.Value.Status);

        // 5.2 Переключение шлюза на Успех и повторная оплата
        paymentEmulator.SetResponseMode(GatewayResponseMode.AlwaysSuccess);
        Result<PaymentResultDto> successPaymentRes = paymentService.PayWithCard(passengerOrder2.Id, testCard);
        Assert.True(successPaymentRes.IsSuccess);
        Assert.Equal(PaymentStatus.Success, successPaymentRes.Value.Status);

        Result<OrderDto> orderPaidCheck = orderTicketService.GetOrderById(passengerOrder2.Id);
        Assert.Equal(OrderStatus.Paid, orderPaidCheck.Value.Status);
        TicketDto passengerTicket = orderPaidCheck.Value.Tickets.First(t => t.SeatNumber == "1A");
        Assert.Equal(TicketStatus.Paid, passengerTicket.Status);

        // Формирование маршрутной квитанции
        PrintDocumentDto ticketReceipt = documentService.GenerateTicketDocument(passengerTicket.Id);
        Assert.NotNull(ticketReceipt);
        Assert.Contains("Маршрутная квитанция", ticketReceipt.Title);
        Assert.Contains("1A", ticketReceipt.FormattedContent);

        // =========================================================================
        // ШАГ 6. Пассажир: возврат билета за 24+ часа -> возврат 90%
        // =========================================================================
        Result<RefundCalculationDto> calcStep6 = refundService.CalculateRefund(passengerTicket.Id);
        Assert.True(calcStep6.IsSuccess);
        Assert.Equal(90, calcStep6.Value.RefundPercent);
        Assert.Equal(passengerTicket.Price * 0.9m, calcStep6.Value.RefundAmount);
        Assert.Equal(passengerTicket.Price * 0.1m, calcStep6.Value.PenaltyAmount);

        Result<RefundResultDto> refundStep6 = refundService.ProcessRefund(passengerTicket.Id);
        Assert.True(refundStep6.IsSuccess);
        Assert.Equal(90, refundStep6.Value.RefundPercent);

        PrintDocumentDto refundDocStep6 = documentService.GenerateRefundReceipt(refundStep6.Value.RefundId);
        Assert.NotNull(refundDocStep6);
        Assert.Contains("Квитанция о возврате", refundDocStep6.Title);

        // Место 1A вновь свободно
        Result<SeatMapDto> seatMapStep6 = bookingService.GetSeatMap(flight.Id);
        Assert.Equal(SeatState.Available, seatMapStep6.Value.Seats.First(s => s.SeatNumber == "1A").State);

        // =========================================================================
        // ШАГ 7. Эмулятор: сдвиг времени до менее 24 часов до вылета
        // =========================================================================
        DateTime lessThan24HoursTime = flightDeparture.AddHours(-10); // За 10 часов до вылета
        TimeSpan shiftStep7 = lessThan24HoursTime - clockService.CurrentVirtualTime;
        clockService.AdvanceTime(shiftStep7);
        Assert.True((flightDeparture - clockService.CurrentVirtualTime).TotalHours < 24);

        // =========================================================================
        // ШАГ 8. Кассир: возврат билета клиента 1C менее чем за 24 часа -> 50%
        // =========================================================================
        sessionService.OpenSession(UserRole.Cashier, "Анна Кассир", "cashier1@airline.local");

        Result<RefundCalculationDto> calcStep8 = refundService.CalculateRefund(cashierTicket.Id);
        Assert.True(calcStep8.IsSuccess);
        Assert.Equal(50, calcStep8.Value.RefundPercent);
        Assert.Equal(cashierTicket.Price * 0.5m, calcStep8.Value.RefundAmount);
        Assert.Equal(cashierTicket.Price * 0.5m, calcStep8.Value.PenaltyAmount);

        Result<RefundResultDto> refundStep8 = refundService.ProcessRefund(cashierTicket.Id);
        Assert.True(refundStep8.IsSuccess);
        Assert.Equal(50, refundStep8.Value.RefundPercent);

        PrintDocumentDto refundDocStep8 = documentService.GenerateRefundReceipt(refundStep8.Value.RefundId);
        Assert.NotNull(refundDocStep8);
        Assert.Contains("Квитанция о возврате", refundDocStep8.Title);

        // =========================================================================
        // ШАГ 9. Администратор: отмена рейса -> массовый 100% возврат всем билетам
        // =========================================================================
        // Оформляем покупку нового билета 2A перед отменой
        sessionService.OpenSession(UserRole.Passenger, "Сергей Смирнов", "sergey@mail.ru");
        Result<ClientDto> clientStep9 = clientService.GetOrCreateClient(
            "Сергей", "Смирнов", "3333 444555", "sergey@mail.ru", "+79993334455");

        CreateOrderRequest orderReqStep9 = new CreateOrderRequest
        {
            FlightId = flight.Id,
            ClientId = clientStep9.Value.Id,
            BookingChannel = BookingChannel.SelfService,
            Passengers = new List<PassengerOrderItemDto>
            {
                new PassengerOrderItemDto
                {
                    FirstName = "Сергей",
                    LastName = "Смирнов",
                    DocumentNumber = "3333 444555",
                    SeatNumber = "2A",
                    HasBaggage = false
                }
            }
        };

        Result<OrderDto> orderStep9 = orderTicketService.CreateOrderWithBooking(orderReqStep9);
        paymentService.PayWithCard(orderStep9.Value.Id, testCard);

        // Администратор отменяет рейс с массовым вынужденным возвратом
        sessionService.OpenSession(UserRole.Administrator, "Администратор", "admin@airline.local");
        Result<MassRefundResultDto> massRefundRes = refundService.ProcessFlightCancellationRefund(flight.Id);
        Assert.True(massRefundRes.IsSuccess);
        Assert.Equal(100, massRefundRes.Value.RefundPercent);
        Assert.NotEmpty(massRefundRes.Value.Refunds);
        Assert.All(massRefundRes.Value.Refunds, r => Assert.Equal(100, r.RefundPercent));

        // Проверяем, что все места рейса заблокированы
        Result<SeatMapDto> seatMapStep9 = bookingService.GetSeatMap(flight.Id);
        Assert.True(seatMapStep9.IsSuccess);
        Assert.All(seatMapStep9.Value.Seats, s => Assert.Equal(SeatState.Blocked, s.State));

        // =========================================================================
        // ШАГ 10. Администратор: формирование отчётов и верификация журнала аудита
        // =========================================================================
        DateTime reportStart = t0.Date;
        DateTime reportEnd = flightDeparture.AddDays(1);

        FlightLoadReportDto loadReport = reportService.GetFlightLoadReport(reportStart, reportEnd);
        Assert.NotNull(loadReport);
        Assert.NotEmpty(loadReport.Items);

        RouteRevenueReportDto revenueReport = reportService.GetRevenueByRoutesReport(reportStart, reportEnd);
        Assert.NotNull(revenueReport);
        Assert.NotEmpty(revenueReport.Items);

        SalesByChannelsReportDto channelsReport = reportService.GetSalesByChannelsReport(reportStart, reportEnd);
        Assert.NotNull(channelsReport);
        Assert.True(channelsReport.SelfServiceTicketsCount > 0);
        Assert.True(channelsReport.CashierTicketsCount > 0);

        RefundsStatisticsReportDto refundStats = reportService.GetRefundsStatisticsReport(reportStart, reportEnd);
        Assert.NotNull(refundStats);
        Assert.True(refundStats.VoluntaryRefundsCount >= 2);
        Assert.True(refundStats.FlightCancellationRefundsCount >= 1);

        // Проверка неизменяемости и хронологической полноты аудита
        IReadOnlyList<AuditLogDto> auditLogs = auditLogService.GetLogs(null, null, null, null);
        Assert.NotEmpty(auditLogs);
        Assert.Contains(auditLogs, l => l.ActionType == "OpenSession");
        Assert.Contains(auditLogs, l => l.ActionType == "CreateFlight");
        Assert.Contains(auditLogs, l => l.ActionType == "CreateOrder");
        Assert.Contains(auditLogs, l => l.ActionType == "PaymentSuccess");
        Assert.Contains(auditLogs, l => l.ActionType == "PaymentFailed");
        Assert.Contains(auditLogs, l => l.ActionType == "ExpireHold" || l.ActionType == "ExpireOrders");
        Assert.Contains(auditLogs, l => l.ActionType == "ProcessRefund");
        Assert.Contains(auditLogs, l => l.ActionType == "MassRefund" || l.ActionType == "CancelFlight");
    }
}
