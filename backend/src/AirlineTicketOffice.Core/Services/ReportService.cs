using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис формирования аналитической и управленческой отчётности (M10).
/// </summary>
public class ReportService : IReportService
{
    private readonly AirlineDbContext _dbContext;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса отчётов.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных «Авиакасса».</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="dbContext"/> равен null.</exception>
    public ReportService(AirlineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Формирует отчёт по загрузке рейсов за указанный период вылетов.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по загрузке рейсов с деталями по классам обслуживания.</returns>
    public FlightLoadReportDto GetFlightLoadReport(DateTime fromDate, DateTime toDate)
    {
        List<Flight> flights = _dbContext.Flights
            .AsNoTracking()
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .Include(f => f.Aircraft)
                .ThenInclude(a => a!.AircraftType)
            .Include(f => f.Seats)
            .Include(f => f.Tickets)
            .Where(f => f.DepartureTime >= fromDate && f.DepartureTime <= toDate)
            .OrderBy(f => f.DepartureTime)
            .ToList();

        List<FlightLoadItemDto> items = [];

        foreach (Flight flight in flights)
        {
            int totalSeats = flight.Seats.Count;
            List<Ticket> soldTickets = flight.Tickets
                .Where(t => t.Status is TicketStatus.Paid or TicketStatus.Used)
                .ToList();
            int soldSeats = soldTickets.Count;

            HashSet<int> businessSeatIds = flight.Seats
                .Where(s => s.SeatClass == SeatClass.Business)
                .Select(s => s.Id)
                .ToHashSet();

            HashSet<int> economySeatIds = flight.Seats
                .Where(s => s.SeatClass == SeatClass.Economy)
                .Select(s => s.Id)
                .ToHashSet();

            int businessSeatsTotal = businessSeatIds.Count;
            int businessSeatsSold = soldTickets.Count(t => businessSeatIds.Contains(t.FlightSeatId));
            double businessLoadPercentage = businessSeatsTotal > 0
                ? Math.Round((double)businessSeatsSold / businessSeatsTotal * 100.0, 1)
                : 0.0;

            int economySeatsTotal = economySeatIds.Count;
            int economySeatsSold = soldTickets.Count(t => economySeatIds.Contains(t.FlightSeatId));
            double economyLoadPercentage = economySeatsTotal > 0
                ? Math.Round((double)economySeatsSold / economySeatsTotal * 100.0, 1)
                : 0.0;

            double loadPercentage = totalSeats > 0
                ? Math.Round((double)soldSeats / totalSeats * 100.0, 1)
                : 0.0;

            string originCode = flight.OriginAirport?.Code ?? string.Empty;
            string destCode = flight.DestinationAirport?.Code ?? string.Empty;
            string route = !string.IsNullOrEmpty(originCode) && !string.IsNullOrEmpty(destCode)
                ? $"{originCode} -> {destCode}"
                : string.Empty;

            string aircraftModel = flight.Aircraft?.AircraftType?.Name ?? string.Empty;

            items.Add(new FlightLoadItemDto
            {
                FlightId = flight.Id,
                FlightNumber = flight.FlightNumber,
                Route = route,
                DepartureTime = flight.DepartureTime,
                AircraftModel = aircraftModel,
                TotalSeats = totalSeats,
                SoldSeats = soldSeats,
                LoadPercentage = loadPercentage,
                BusinessSeatsTotal = businessSeatsTotal,
                BusinessSeatsSold = businessSeatsSold,
                BusinessLoadPercentage = businessLoadPercentage,
                EconomySeatsTotal = economySeatsTotal,
                EconomySeatsSold = economySeatsSold,
                EconomyLoadPercentage = economyLoadPercentage
            });
        }

        int reportTotalCapacity = items.Sum(i => i.TotalSeats);
        int reportTotalSoldSeats = items.Sum(i => i.SoldSeats);
        double averageLoadPercentage = reportTotalCapacity > 0
            ? Math.Round((double)reportTotalSoldSeats / reportTotalCapacity * 100.0, 1)
            : 0.0;

        return new FlightLoadReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalFlights = items.Count,
            TotalCapacity = reportTotalCapacity,
            TotalSoldSeats = reportTotalSoldSeats,
            AverageLoadPercentage = averageLoadPercentage,
            Items = items
        };
    }

    /// <summary>
    /// Формирует отчёт по выручке по направлениям за указанный период вылетов.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по валовой и чистой выручке в разрезе маршрутов.</returns>
    public RouteRevenueReportDto GetRevenueByRoutesReport(DateTime fromDate, DateTime toDate)
    {
        List<Ticket> tickets = _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(t => t.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .Include(t => t.Refund)
            .Where(t => t.Flight != null &&
                        t.Flight.DepartureTime >= fromDate &&
                        t.Flight.DepartureTime <= toDate &&
                        (t.Status == TicketStatus.Paid || t.Status == TicketStatus.Used || t.Status == TicketStatus.Refunded))
            .ToList();

        var groupedByRoute = tickets
            .GroupBy(t => new
            {
                OriginCode = t.Flight!.OriginAirport?.Code ?? string.Empty,
                DestCode = t.Flight.DestinationAirport?.Code ?? string.Empty,
                OriginCity = t.Flight.OriginAirport?.City ?? string.Empty,
                DestCity = t.Flight.DestinationAirport?.City ?? string.Empty
            })
            .OrderBy(g => g.Key.OriginCode)
            .ThenBy(g => g.Key.DestCode)
            .ToList();

        List<RouteRevenueItemDto> items = [];

        foreach (var group in groupedByRoute)
        {
            int ticketsSold = group.Count();
            decimal grossRevenue = group.Sum(t => t.Price);
            decimal refundedAmount = group.Where(t => t.Refund != null).Sum(t => t.Refund!.Amount);
            decimal netRevenue = grossRevenue - refundedAmount;

            items.Add(new RouteRevenueItemDto
            {
                Route = $"{group.Key.OriginCode} -> {group.Key.DestCode}",
                OriginCity = group.Key.OriginCity,
                DestinationCity = group.Key.DestCity,
                TicketsSold = ticketsSold,
                GrossRevenue = grossRevenue,
                RefundedAmount = refundedAmount,
                NetRevenue = netRevenue
            });
        }

        int totalTicketsSold = items.Sum(i => i.TicketsSold);
        decimal totalGrossRevenue = items.Sum(i => i.GrossRevenue);
        decimal totalRefundedAmount = items.Sum(i => i.RefundedAmount);
        decimal totalNetRevenue = totalGrossRevenue - totalRefundedAmount;

        return new RouteRevenueReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalTicketsSold = totalTicketsSold,
            TotalGrossRevenue = totalGrossRevenue,
            TotalRefundedAmount = totalRefundedAmount,
            TotalNetRevenue = totalNetRevenue,
            Items = items
        };
    }

    /// <summary>
    /// Формирует отчёт по продажам в разрезе каналов (касса vs самообслуживание) за указанный период.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по объёмам продаж и долям выручки по каналам.</returns>
    public ChannelSalesReportDto GetSalesByChannelsReport(DateTime fromDate, DateTime toDate)
    {
        List<Order> orders = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Tickets)
            .Where(o => o.CreatedAt >= fromDate &&
                        o.CreatedAt <= toDate &&
                        (o.Status == OrderStatus.Paid || o.Status == OrderStatus.PartiallyRefunded || o.Status == OrderStatus.Refunded))
            .ToList();

        decimal totalRevenue = orders.Sum(o => o.TotalAmount);

        List<ChannelSalesItemDto> items = orders
            .GroupBy(o => o.Channel)
            .OrderBy(g => g.Key)
            .Select(group =>
            {
                int ordersCount = group.Count();
                int ticketsCount = group.SelectMany(o => o.Tickets)
                    .Count(t => t.Status is TicketStatus.Paid or TicketStatus.Used or TicketStatus.Refunded);
                decimal salesAmount = group.Sum(o => o.TotalAmount);
                double percentage = totalRevenue > 0
                    ? Math.Round((double)(salesAmount / totalRevenue) * 100.0, 1)
                    : 0.0;

                string channelName = group.Key == OrderChannel.Cashier ? "Касса" : "Самообслуживание";

                return new ChannelSalesItemDto
                {
                    Channel = group.Key,
                    ChannelName = channelName,
                    OrdersCount = ordersCount,
                    TicketsCount = ticketsCount,
                    TotalSalesAmount = salesAmount,
                    PercentageOfTotalRevenue = percentage
                };
            })
            .ToList();

        int totalOrders = items.Sum(i => i.OrdersCount);
        int totalTickets = items.Sum(i => i.TicketsCount);

        return new ChannelSalesReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalOrders = totalOrders,
            TotalTickets = totalTickets,
            TotalRevenue = totalRevenue,
            Items = items
        };
    }

    /// <summary>
    /// Формирует статистический отчёт по возвратам билетов за указанный период.
    /// </summary>
    /// <param name="fromDate">Начальная дата периода.</param>
    /// <param name="toDate">Конечная дата периода.</param>
    /// <returns>Отчёт по суммам выплат и удержанных штрафов в разрезе причин возвратов.</returns>
    public RefundStatisticsReportDto GetRefundsStatisticsReport(DateTime fromDate, DateTime toDate)
    {
        List<Refund> refunds = _dbContext.Refunds
            .AsNoTracking()
            .Include(r => r.Ticket)
            .Where(r => r.CreatedAt >= fromDate && r.CreatedAt <= toDate)
            .ToList();

        List<RefundStatisticsItemDto> items = refunds
            .GroupBy(r => r.Reason)
            .OrderBy(g => g.Key)
            .Select(group =>
            {
                int refundsCount = group.Count();
                decimal totalRefundAmount = group.Sum(r => r.Amount);
                decimal totalPenaltyAmount = group.Sum(r => (r.Ticket != null ? r.Ticket.Price : 0m) - r.Amount);
                string reasonName = group.Key switch
                {
                    RefundReason.Voluntary => "Добровольный возврат",
                    RefundReason.FlightCancelled => "Отмена рейса",
                    _ => group.Key.ToString()
                };

                return new RefundStatisticsItemDto
                {
                    Reason = group.Key,
                    ReasonName = reasonName,
                    RefundsCount = refundsCount,
                    TotalRefundAmount = totalRefundAmount,
                    TotalPenaltyAmount = totalPenaltyAmount
                };
            })
            .ToList();

        int totalRefundsCount = refunds.Count;
        decimal totalRefundAmount = refunds.Sum(r => r.Amount);
        decimal totalPenaltyAmount = items.Sum(i => i.TotalPenaltyAmount);

        return new RefundStatisticsReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalRefundsCount = totalRefundsCount,
            TotalRefundAmount = totalRefundAmount,
            TotalPenaltyAmount = totalPenaltyAmount,
            Items = items
        };
    }
}
