using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис управления заказами и электронными авиабилетами (M7).
/// </summary>
public class OrderTicketService : IOrderTicketService
{
    private readonly AirlineDbContext _dbContext;
    private readonly ITariffCalculator _tariffCalculator;
    private readonly IVirtualClockService _clockService;
    private readonly ISessionService _sessionService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="OrderTicketService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных авиакассы.</param>
    /// <param name="tariffCalculator">Калькулятор динамических тарифов.</param>
    /// <param name="clockService">Сервис системного виртуального времени.</param>
    /// <param name="sessionService">Сервис пользовательских сессий и ролей.</param>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    public OrderTicketService(
        AirlineDbContext dbContext,
        ITariffCalculator tariffCalculator,
        IVirtualClockService clockService,
        ISessionService sessionService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tariffCalculator = tariffCalculator ?? throw new ArgumentNullException(nameof(tariffCalculator));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public Result<OrderDto> CreateOrderWithBooking(CreateOrderRequest request)
    {
        if (request?.Passengers == null || request.Passengers.Count < 1 || request.Passengers.Count > 5)
        {
            return Result<OrderDto>.Failure("В одном заказе может быть оформлено не более 5 пассажиров.");
        }

        foreach (var p in request.Passengers)
        {
            if (string.IsNullOrWhiteSpace(p.FullName) ||
                string.IsNullOrWhiteSpace(p.DocumentNumber) ||
                string.IsNullOrWhiteSpace(p.SeatNumber))
            {
                return Result<OrderDto>.Failure("Данные пассажира (ФИО, документ, место) должны быть заполнены.");
            }
        }

        if (request.Passengers.Select(p => p.SeatNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Passengers.Count)
        {
            return Result<OrderDto>.Failure("В заказе указаны дублирующиеся места.");
        }

        var flight = _dbContext.Flights
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .Include(f => f.Seats)
            .FirstOrDefault(f => f.Id == request.FlightId);

        if (flight is null)
        {
            return Result<OrderDto>.Failure($"Рейс с идентификатором {request.FlightId} не найден.");
        }

        if (flight.Status != FlightStatus.Scheduled)
        {
            return Result<OrderDto>.Failure("Бронирование доступно только для запланированных рейсов.");
        }

        using var transaction = _dbContext.Database.BeginTransaction();
        var currentVirtualTime = _clockService.CurrentVirtualTime;

        var requestedSeatNumbers = request.Passengers.Select(p => p.SeatNumber).ToList();
        var seats = flight.Seats
            .Where(s => requestedSeatNumbers.Contains(s.SeatNumber, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (seats.Count != request.Passengers.Count)
        {
            transaction.Rollback();
            return Result<OrderDto>.Failure("Одно или несколько выбранных мест не существуют на данном рейсе.");
        }

        foreach (var seat in seats)
        {
            bool isAvailable = seat.State == SeatState.Available ||
                (seat.State == SeatState.Held && seat.HoldUntilTime.HasValue && seat.HoldUntilTime.Value <= currentVirtualTime);

            if (!isAvailable)
            {
                transaction.Rollback();
                return Result<OrderDto>.Failure("Одно или несколько выбранных мест уже заняты.");
            }
        }

        var holdExpiresAt = currentVirtualTime.AddMinutes(15);
        var seatByNumber = seats.ToDictionary(s => s.SeatNumber, s => s, StringComparer.OrdinalIgnoreCase);

        var ticketData = new List<(PassengerOrderItemDto Passenger, FlightSeat Seat, Client Client, decimal Price)>();

        foreach (var passenger in request.Passengers)
        {
            var seat = seatByNumber[passenger.SeatNumber];
            seat.State = SeatState.Held;
            seat.HoldUntilTime = holdExpiresAt;

            var client = _dbContext.Clients.FirstOrDefault(c => c.DocumentNumber == passenger.DocumentNumber);
            if (client is null)
            {
                client = new Client
                {
                    FullName = passenger.FullName.Trim(),
                    DocumentNumber = passenger.DocumentNumber.Trim(),
                    ContactPhone = passenger.ContactPhone?.Trim(),
                    ContactEmail = passenger.ContactEmail?.Trim()
                };
                _dbContext.Clients.Add(client);
            }
            else
            {
                client.FullName = passenger.FullName.Trim();
                if (!string.IsNullOrWhiteSpace(passenger.ContactPhone)) client.ContactPhone = passenger.ContactPhone.Trim();
                if (!string.IsNullOrWhiteSpace(passenger.ContactEmail)) client.ContactEmail = passenger.ContactEmail.Trim();
            }

            decimal price = _tariffCalculator.CalculateTicketPrice(
                flight.OriginAirportId,
                flight.DestinationAirportId,
                seat.SeatClass,
                flight.DepartureTime,
                passenger.HasBaggage);

            ticketData.Add((passenger, seat, client, price));
        }

        _dbContext.SaveChanges();

        var totalAmount = ticketData.Sum(td => td.Price);

        var order = new Order
        {
            Channel = _sessionService.CurrentRole == UserRole.Cashier ? OrderChannel.Cashier : OrderChannel.SelfService,
            CreatedRole = _sessionService.CurrentRole,
            TotalAmount = totalAmount,
            Status = OrderStatus.Created,
            CreatedAt = currentVirtualTime,
            HoldExpiresAt = holdExpiresAt,
            OrderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
        };

        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();

        var tickets = new List<Ticket>();
        foreach (var item in ticketData)
        {
            var ticket = new Ticket
            {
                TicketNumber = $"TKT-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}",
                OrderId = order.Id,
                Order = order,
                ClientId = item.Client.Id,
                Client = item.Client,
                FlightId = flight.Id,
                Flight = flight,
                FlightSeatId = item.Seat.Id,
                FlightSeat = item.Seat,
                Price = item.Price,
                HasBaggage = item.Passenger.HasBaggage,
                Status = TicketStatus.Booked
            };
            tickets.Add(ticket);
            _dbContext.Tickets.Add(ticket);
        }

        _dbContext.SaveChanges();

        _auditLogService.Log("CreateOrder", "Order", order.Id, $"Создан заказ {order.OrderNumber} на {request.Passengers.Count} пассажиров на сумму {order.TotalAmount} руб.");

        transaction.Commit();

        var orderDto = MapToOrderDto(order, tickets, flight);
        return Result<OrderDto>.Success(orderDto);
    }

    /// <inheritdoc />
    public Result CancelOrder(int orderId)
    {
        var order = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .FirstOrDefault(o => o.Id == orderId);

        if (order is null)
        {
            return Result.Failure($"Заказ с идентификатором {orderId} не найден.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return Result.Failure("Нельзя отменить уже оплаченный заказ. Для возврата билетов воспользуйтесь процедурой возврата.");
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            return Result.Failure("Заказ уже отменён.");
        }

        if (order.Status == OrderStatus.Expired)
        {
            return Result.Failure("Срок бронирования заказа уже истёк.");
        }

        if (order.Status != OrderStatus.Created)
        {
            return Result.Failure($"Отмена заказа невозможна: заказ находится в статусе {order.Status}.");
        }

        using var transaction = _dbContext.Database.BeginTransaction();

        order.Status = OrderStatus.Cancelled;

        foreach (var ticket in order.Tickets)
        {
            if (ticket.Status == TicketStatus.Booked)
            {
                ticket.Status = TicketStatus.Cancelled;
            }

            if (ticket.FlightSeat != null && ticket.FlightSeat.State == SeatState.Held)
            {
                ticket.FlightSeat.State = SeatState.Available;
                ticket.FlightSeat.HoldUntilTime = null;
            }
        }

        _dbContext.SaveChanges();

        _auditLogService.Log("CancelOrder", "Order", order.Id, $"Отменено бронирование заказа {order.OrderNumber}, освобождено мест: {order.Tickets.Count}.");

        transaction.Commit();

        return Result.Success();
    }

    /// <inheritdoc />
    public Result<OrderDto> GetOrderById(int orderId)
    {
        var order = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Client)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f!.OriginAirport)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f!.DestinationAirport)
            .FirstOrDefault(o => o.Id == orderId);

        if (order is null)
        {
            return Result<OrderDto>.Failure($"Заказ с идентификатором {orderId} не найден.");
        }

        return Result<OrderDto>.Success(MapToOrderDto(order, order.Tickets));
    }

    /// <inheritdoc />
    public Result<OrderDto> GetOrderByNumber(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return Result<OrderDto>.Failure("Номер заказа не может быть пустым.");
        }

        var order = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Client)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f!.OriginAirport)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f!.DestinationAirport)
            .FirstOrDefault(o => o.OrderNumber == orderNumber);

        if (order is null)
        {
            return Result<OrderDto>.Failure($"Заказ с номером {orderNumber} не найден.");
        }

        return Result<OrderDto>.Success(MapToOrderDto(order, order.Tickets));
    }

    /// <inheritdoc />
    public IReadOnlyList<TicketDto> GetTicketsByClientDocument(string documentNumber)
    {
        var trimmedDoc = documentNumber?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDoc))
        {
            return [];
        }

        var tickets = _dbContext.Tickets
            .AsNoTracking()
            .Include(t => t.Order)
            .Include(t => t.Client)
            .Include(t => t.FlightSeat)
            .Include(t => t.Flight)
                .ThenInclude(f => f!.OriginAirport)
            .Include(t => t.Flight)
                .ThenInclude(f => f!.DestinationAirport)
            .Where(t => t.Client != null && t.Client.DocumentNumber == trimmedDoc)
            .OrderByDescending(t => t.Flight != null ? t.Flight.DepartureTime : default)
            .ThenByDescending(t => t.Id)
            .ToList();

        return tickets.Select(t => new TicketDto
        {
            Id = t.Id,
            TicketNumber = t.TicketNumber,
            OrderId = t.OrderId,
            OrderNumber = t.Order?.OrderNumber ?? string.Empty,
            ClientId = t.ClientId,
            PassengerName = t.Client?.FullName ?? string.Empty,
            PassengerDocument = t.Client?.DocumentNumber ?? string.Empty,
            FlightId = t.FlightId,
            FlightNumber = t.Flight?.FlightNumber ?? string.Empty,
            FlightSeatId = t.FlightSeatId,
            SeatNumber = t.FlightSeat?.SeatNumber ?? string.Empty,
            SeatClass = t.FlightSeat?.SeatClass ?? SeatClass.Economy,
            Price = t.Price,
            HasBaggage = t.HasBaggage,
            Status = t.Status,
            DepartureTime = t.Flight?.DepartureTime ?? default,
            OriginAirportCode = t.Flight?.OriginAirport?.Code ?? string.Empty,
            DestinationAirportCode = t.Flight?.DestinationAirport?.Code ?? string.Empty
        }).ToList();
    }

    /// <inheritdoc />
    public int ExpireOverdueOrders()
    {
        var currentTime = _clockService.CurrentVirtualTime;

        var overdueOrders = _dbContext.Orders
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Where(o => o.Status == OrderStatus.Created && o.HoldExpiresAt <= currentTime)
            .ToList();

        if (overdueOrders.Count == 0)
        {
            return 0;
        }

        using var transaction = _dbContext.Database.BeginTransaction();

        foreach (var order in overdueOrders)
        {
            order.Status = OrderStatus.Expired;

            foreach (var ticket in order.Tickets)
            {
                if (ticket.Status == TicketStatus.Booked)
                {
                    ticket.Status = TicketStatus.Cancelled;
                }

                if (ticket.FlightSeat != null && ticket.FlightSeat.State == SeatState.Held)
                {
                    ticket.FlightSeat.State = SeatState.Available;
                    ticket.FlightSeat.HoldUntilTime = null;
                }
            }
        }

        _dbContext.SaveChanges();

        _auditLogService.Log("ExpireOrders", "Order", null, $"Автоматически аннулировано просроченных заказов: {overdueOrders.Count}, освобождены места.");

        transaction.Commit();

        return overdueOrders.Count;
    }

    /// <inheritdoc />
    public IReadOnlyList<OrderDto> GetOrdersForCurrentSession()
    {
        var currentRole = _sessionService.CurrentRole;
        var orders = _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Client)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.FlightSeat)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f!.OriginAirport)
            .Include(o => o.Tickets)
                .ThenInclude(t => t.Flight)
                    .ThenInclude(f => f!.DestinationAirport)
            .Where(o => o.CreatedRole == currentRole)
            .OrderByDescending(o => o.CreatedAt)
            .ToList();

        return orders.Select(o => MapToOrderDto(o, o.Tickets)).ToList();
    }

    private static OrderDto MapToOrderDto(Order order, IEnumerable<Ticket> tickets, Flight? flight = null)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            Channel = order.Channel,
            CreatedRole = order.CreatedRole,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            CreatedAt = order.CreatedAt,
            HoldExpiresAt = order.HoldExpiresAt,
            Tickets = tickets.Select(t =>
            {
                var fl = t.Flight ?? flight;
                return new TicketDto
                {
                    Id = t.Id,
                    TicketNumber = t.TicketNumber,
                    OrderId = t.OrderId,
                    OrderNumber = order.OrderNumber,
                    ClientId = t.ClientId,
                    PassengerName = t.Client?.FullName ?? string.Empty,
                    PassengerDocument = t.Client?.DocumentNumber ?? string.Empty,
                    FlightId = t.FlightId,
                    FlightNumber = fl?.FlightNumber ?? string.Empty,
                    FlightSeatId = t.FlightSeatId,
                    SeatNumber = t.FlightSeat?.SeatNumber ?? string.Empty,
                    SeatClass = t.FlightSeat?.SeatClass ?? SeatClass.Economy,
                    Price = t.Price,
                    HasBaggage = t.HasBaggage,
                    Status = t.Status,
                    DepartureTime = fl?.DepartureTime ?? default,
                    OriginAirportCode = fl?.OriginAirport?.Code ?? string.Empty,
                    DestinationAirportCode = fl?.DestinationAirport?.Code ?? string.Empty
                };
            }).ToList()
        };
    }
}
