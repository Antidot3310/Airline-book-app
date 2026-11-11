using System;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса управления заказами и электронными билетами <see cref="OrderTicketService"/> (M7).
/// </summary>
public class OrderTicketServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AirlineDbContext _dbContext;

    private readonly List<IDisposable> _extraDisposables = [];

    public OrderTicketServiceTests()
    {
        var services = new ServiceCollection();

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        services.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(_connection));

        _serviceProvider = services.BuildServiceProvider();

        _dbContext = _serviceProvider.GetRequiredService<AirlineDbContext>();
        _dbContext.Database.EnsureCreated();
        DbInitializer.Initialize(_dbContext);

        _scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
    }

    public void Dispose()
    {
        foreach (var item in _extraDisposables)
        {
            item.Dispose();
        }

        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Dispose();
    }

    private OrderTicketService CreateService(UserRole role = UserRole.Passenger)
    {
        var clockService = new VirtualClockService(_scopeFactory);
        var auditService = new AuditLogService(_dbContext);
        var sessionService = new SessionService(auditService);
        sessionService.OpenSession(role);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);

        return new OrderTicketService(_dbContext, tariffCalculator, clockService, sessionService, auditService);
    }

    private (OrderTicketService Service, VirtualClockService ClockService) CreateServiceWithClock(UserRole role = UserRole.Passenger)
    {
        var isolatedServices = new ServiceCollection();
        var clockConn = new SqliteConnection("DataSource=:memory:");
        clockConn.Open();
        isolatedServices.AddDbContext<AirlineDbContext>(opt => opt.UseSqlite(clockConn));
        var clockProvider = isolatedServices.BuildServiceProvider();
        var clockDb = clockProvider.GetRequiredService<AirlineDbContext>();
        clockDb.Database.EnsureCreated();
        DbInitializer.Initialize(clockDb);

        _extraDisposables.Add(clockDb);
        _extraDisposables.Add(clockProvider);
        _extraDisposables.Add(clockConn);

        var clockScopeFactory = clockProvider.GetRequiredService<IServiceScopeFactory>();
        var clockService = new VirtualClockService(clockScopeFactory);

        var auditService = new AuditLogService(_dbContext);
        var sessionService = new SessionService(auditService);
        sessionService.OpenSession(role);
        var tariffCalculator = new TariffCalculator(_dbContext, clockService, auditService);

        var service = new OrderTicketService(_dbContext, tariffCalculator, clockService, sessionService, auditService);
        return (service, clockService);
    }

    [Fact]
    public void CreateOrderWithBooking_When5Passengers_SucceedsAndHoldsSeatsMatchesTC01()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var request = new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Иванов Иван", DocumentNumber = "DOC-001", SeatNumber = "4A", HasBaggage = false },
                new() { FullName = "Петров Петр", DocumentNumber = "DOC-002", SeatNumber = "4C", HasBaggage = false },
                new() { FullName = "Сидоров Сидор", DocumentNumber = "DOC-003", SeatNumber = "4D", HasBaggage = false },
                new() { FullName = "Кузнецов Алексей", DocumentNumber = "DOC-004", SeatNumber = "4E", HasBaggage = false },
                new() { FullName = "Смирнов Дмитрий", DocumentNumber = "DOC-005", SeatNumber = "4F", HasBaggage = false }
            ]
        };

        // Act
        var result = service.CreateOrderWithBooking(request);

        // Assert (TC-01)
        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(UserRole.Passenger, order.CreatedRole);
        Assert.Equal(OrderChannel.SelfService, order.Channel);
        Assert.Equal(5, order.Tickets.Count);

        // SU-101 SVO->LED базовый тариф 4500, K_days = 1.8, эконом = 8100.00 руб. За 5 мест = 40500.00 руб.
        Assert.Equal(40500.00m, order.TotalAmount);
        Assert.All(order.Tickets, t => Assert.Equal(TicketStatus.Booked, t.Status));
        Assert.All(order.Tickets, t => Assert.Equal(8100.00m, t.Price));

        // Проверяем, что в базе все 5 мест переведены в состояние Held
        var heldSeats = _dbContext.FlightSeats
            .Where(s => s.FlightId == flight.Id && (s.SeatNumber == "4A" || s.SeatNumber == "4C" || s.SeatNumber == "4D" || s.SeatNumber == "4E" || s.SeatNumber == "4F"))
            .ToList();

        Assert.Equal(5, heldSeats.Count);
        Assert.All(heldSeats, s => Assert.Equal(SeatState.Held, s.State));
    }

    [Fact]
    public void CreateOrderWithBooking_When6Passengers_FailsAndHoldsNoSeatsMatchesTC02()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var request = new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Пассажир 1", DocumentNumber = "D-1", SeatNumber = "4A" },
                new() { FullName = "Пассажир 2", DocumentNumber = "D-2", SeatNumber = "4C" },
                new() { FullName = "Пассажир 3", DocumentNumber = "D-3", SeatNumber = "4D" },
                new() { FullName = "Пассажир 4", DocumentNumber = "D-4", SeatNumber = "4E" },
                new() { FullName = "Пассажир 5", DocumentNumber = "D-5", SeatNumber = "4F" },
                new() { FullName = "Пассажир 6", DocumentNumber = "D-6", SeatNumber = "5A" }
            ]
        };

        // Act
        var result = service.CreateOrderWithBooking(request);

        // Assert (TC-02)
        Assert.True(result.IsFailure);
        Assert.Contains("не более 5 пассажиров", result.ErrorMessage);

        // Проверяем, что ни одно место не удержано
        var seats = _dbContext.FlightSeats
            .Where(s => s.FlightId == flight.Id && (s.SeatNumber == "4A" || s.SeatNumber == "4C" || s.SeatNumber == "5A"))
            .ToList();

        Assert.All(seats, s => Assert.Equal(SeatState.Available, s.State));
        Assert.Empty(_dbContext.Orders);
    }

    [Fact]
    public void CreateOrderWithBooking_When0Passengers_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var request = new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers = []
        };

        // Act
        var result = service.CreateOrderWithBooking(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("не более 5 пассажиров", result.ErrorMessage);
    }

    [Fact]
    public void CreateOrderWithBooking_WithBaggage_CalculatesBaggageFeeInPrice()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var request = new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Без Багажа", DocumentNumber = "DOC-NB", SeatNumber = "4A", HasBaggage = false },
                new() { FullName = "С Багажом", DocumentNumber = "DOC-WB", SeatNumber = "4C", HasBaggage = true }
            ]
        };

        // Act
        var result = service.CreateOrderWithBooking(request);

        // Assert
        Assert.True(result.IsSuccess);
        var order = result.Value;

        var ticketNoBag = order.Tickets.First(t => t.SeatNumber == "4A");
        var ticketWithBag = order.Tickets.First(t => t.SeatNumber == "4C");

        Assert.Equal(8100.00m, ticketNoBag.Price);
        Assert.Equal(9600.00m, ticketWithBag.Price); // 8100 + 1500 багаж
        Assert.Equal(17700.00m, order.TotalAmount);
    }

    [Fact]
    public void CreateOrderWithBooking_WhenSeatAlreadyOccupied_FailsAndRollsBack()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.Include(f => f.Seats).First(f => f.FlightNumber == "SU-101");

        // Место 4A уже занято (продано)
        var seat4A = flight.Seats.First(s => s.SeatNumber == "4A");
        seat4A.State = SeatState.Sold;
        _dbContext.SaveChanges();

        var request = new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Пассажир 1", DocumentNumber = "DOC-1", SeatNumber = "4A" },
                new() { FullName = "Пассажир 2", DocumentNumber = "DOC-2", SeatNumber = "4C" }
            ]
        };

        // Act
        var result = service.CreateOrderWithBooking(request);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("уже заняты", result.ErrorMessage);

        // Проверяем, что место 4C осталось Available, а заказ не был создан
        int seat4CId = flight.Seats.First(x => x.SeatNumber == "4C").Id;
        var seat4C = _dbContext.FlightSeats.AsNoTracking().First(s => s.Id == seat4CId);
        Assert.Equal(SeatState.Available, seat4C.State);
        Assert.Empty(_dbContext.Orders);
    }

    [Fact]
    public void GetOrderById_WhenOrderExists_ReturnsCorrectDto()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var createResult = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Тестовый Пассажир", DocumentNumber = "DOC-TEST", SeatNumber = "4A" }
            ]
        });

        Assert.True(createResult.IsSuccess);
        int orderId = createResult.Value.Id;

        // Act
        var getResult = service.GetOrderById(orderId);

        // Assert
        Assert.True(getResult.IsSuccess);
        Assert.Equal(orderId, getResult.Value.Id);
        Assert.Equal(createResult.Value.OrderNumber, getResult.Value.OrderNumber);
        Assert.Single(getResult.Value.Tickets);
        Assert.Equal("4A", getResult.Value.Tickets[0].SeatNumber);
        Assert.Equal("Тестовый Пассажир", getResult.Value.Tickets[0].PassengerName);
    }

    [Fact]
    public void GetTicketsByClientDocument_ReturnsTicketsForPassenger()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        string docNumber = "PASSPORT-777888";

        service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Иван Клиентов", DocumentNumber = docNumber, SeatNumber = "4A" }
            ]
        });

        // Act
        var tickets = service.GetTicketsByClientDocument(docNumber);

        // Assert
        Assert.Single(tickets);
        Assert.Equal(docNumber, tickets[0].PassengerDocument);
        Assert.Equal("Иван Клиентов", tickets[0].PassengerName);
        Assert.Equal("4A", tickets[0].SeatNumber);
        Assert.False(string.IsNullOrWhiteSpace(tickets[0].OrderNumber));
    }

    [Fact]
    public void CancelOrder_WhenOrderIsCreated_CancelsOrderAndReleasesHeldSeats()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var createResult = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Пассажир 1", DocumentNumber = "DOC-C1", SeatNumber = "4A" },
                new() { FullName = "Пассажир 2", DocumentNumber = "DOC-C2", SeatNumber = "4C" }
            ]
        });

        Assert.True(createResult.IsSuccess);
        int orderId = createResult.Value.Id;

        // Act
        var result = service.CancelOrder(orderId);

        // Assert
        Assert.True(result.IsSuccess);

        var orderInDb = _dbContext.Orders.Include(o => o.Tickets).First(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Cancelled, orderInDb.Status);
        Assert.Equal(2, orderInDb.Tickets.Count);
        Assert.All(orderInDb.Tickets, t => Assert.Equal(TicketStatus.Cancelled, t.Status));

        var seats = _dbContext.FlightSeats
            .Where(s => s.FlightId == flight.Id && (s.SeatNumber == "4A" || s.SeatNumber == "4C"))
            .ToList();

        Assert.Equal(2, seats.Count);
        Assert.All(seats, s =>
        {
            Assert.Equal(SeatState.Available, s.State);
            Assert.Null(s.HoldUntilTime);
        });

        var auditLog = _dbContext.AuditLogs.FirstOrDefault(l => l.ActionType == "CancelOrder" && l.EntityId == orderId);
        Assert.NotNull(auditLog);
    }

    [Fact]
    public void CancelOrder_WhenOrderDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        int nonExistentId = 999999;

        // Act
        var result = service.CancelOrder(nonExistentId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("не найден", result.ErrorMessage);
    }

    [Fact]
    public void CancelOrder_WhenOrderIsPaid_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var createResult = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Оплативший Пассажир", DocumentNumber = "DOC-PAID", SeatNumber = "4A" }
            ]
        });

        Assert.True(createResult.IsSuccess);
        int orderId = createResult.Value.Id;

        var order = _dbContext.Orders.First(o => o.Id == orderId);
        order.Status = OrderStatus.Paid;
        _dbContext.SaveChanges();

        // Act
        var result = service.CancelOrder(orderId);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Нельзя отменить уже оплаченный заказ", result.ErrorMessage);

        var orderInDb = _dbContext.Orders.First(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Paid, orderInDb.Status);

        var seat = _dbContext.FlightSeats.First(s => s.FlightId == flight.Id && s.SeatNumber == "4A");
        Assert.Equal(SeatState.Held, seat.State);
    }

    [Fact]
    public void CancelOrder_WhenOrderAlreadyCancelled_ReturnsFailure()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var createResult = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Отменяющий Пассажир", DocumentNumber = "DOC-CANCEL", SeatNumber = "4A" }
            ]
        });

        Assert.True(createResult.IsSuccess);
        int orderId = createResult.Value.Id;

        var firstCancel = service.CancelOrder(orderId);
        Assert.True(firstCancel.IsSuccess);

        // Act
        var secondCancel = service.CancelOrder(orderId);

        // Assert
        Assert.True(secondCancel.IsFailure);
        Assert.Equal("Заказ уже отменён.", secondCancel.ErrorMessage);
    }

    [Fact]
    public void ExpireOverdueOrders_WhenHoldExpired_CancelsTicketsSetsOrderExpiredAndFreesSeatsMatchesTC04B()
    {
        // Arrange
        var (service, clockService) = CreateServiceWithClock(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var createResult = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Иван Просроченный", DocumentNumber = "DOC-EXP1", SeatNumber = "4A" },
                new() { FullName = "Петр Просроченный", DocumentNumber = "DOC-EXP2", SeatNumber = "4C" }
            ]
        });

        Assert.True(createResult.IsSuccess);
        int orderId = createResult.Value.Id;

        // Сдвигаем виртуальное время на +16 минут (срок 15 минут истёк, TC-04Б)
        clockService.AdvanceTime(TimeSpan.FromMinutes(16));

        // Act
        int expiredCount = service.ExpireOverdueOrders();

        // Assert (TC-04Б)
        Assert.Equal(1, expiredCount);

        var orderInDb = _dbContext.Orders.Include(o => o.Tickets).First(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Expired, orderInDb.Status);
        Assert.Equal(2, orderInDb.Tickets.Count);
        Assert.All(orderInDb.Tickets, t => Assert.Equal(TicketStatus.Cancelled, t.Status));

        var seats = _dbContext.FlightSeats
            .Where(s => s.FlightId == flight.Id && (s.SeatNumber == "4A" || s.SeatNumber == "4C"))
            .ToList();

        Assert.Equal(2, seats.Count);
        Assert.All(seats, s =>
        {
            Assert.Equal(SeatState.Available, s.State);
            Assert.Null(s.HoldUntilTime);
        });

        var auditLog = _dbContext.AuditLogs.FirstOrDefault(l => l.ActionType == "ExpireOrders");
        Assert.NotNull(auditLog);
    }

    [Fact]
    public void ExpireOverdueOrders_WhenHoldNotExpired_DoesNotExpireOrderMatchesTC04A()
    {
        // Arrange
        var (service, clockService) = CreateServiceWithClock(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");

        var createResult = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Иван Активный", DocumentNumber = "DOC-ACT", SeatNumber = "4A" }
            ]
        });

        Assert.True(createResult.IsSuccess);
        int orderId = createResult.Value.Id;

        // Сдвигаем время на 14 минут 50 секунд (срок 15 минут ещё не вышел, TC-04А)
        clockService.AdvanceTime(TimeSpan.FromMinutes(14).Add(TimeSpan.FromSeconds(50)));

        // Act
        int expiredCount = service.ExpireOverdueOrders();

        // Assert (TC-04А)
        Assert.Equal(0, expiredCount);

        var orderInDb = _dbContext.Orders.First(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Created, orderInDb.Status);

        var seat = _dbContext.FlightSeats.First(s => s.FlightId == flight.Id && s.SeatNumber == "4A");
        Assert.Equal(SeatState.Held, seat.State);
    }

    [Fact]
    public void GetTicketsByClientDocument_WhenPassengerHasMultipleTickets_ReturnsAllTicketsSorted()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);
        var flight = _dbContext.Flights.First(f => f.FlightNumber == "SU-101");
        string docNumber = "PASSPORT-MULTI-999";

        var order1 = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Многократный Пассажир", DocumentNumber = docNumber, SeatNumber = "4A" }
            ]
        });
        Assert.True(order1.IsSuccess);

        var order2 = service.CreateOrderWithBooking(new CreateOrderRequest
        {
            FlightId = flight.Id,
            Passengers =
            [
                new() { FullName = "Многократный Пассажир", DocumentNumber = docNumber, SeatNumber = "4C" }
            ]
        });
        Assert.True(order2.IsSuccess);

        // Act (тестируем также обрезку пробелов)
        var tickets = service.GetTicketsByClientDocument($"  {docNumber}  ");

        // Assert
        Assert.Equal(2, tickets.Count);
        Assert.All(tickets, t =>
        {
            Assert.Equal(docNumber, t.PassengerDocument);
            Assert.Equal("Многократный Пассажир", t.PassengerName);
            Assert.Equal("SU-101", t.FlightNumber);
            Assert.Equal("SVO", t.OriginAirportCode);
            Assert.Equal("LED", t.DestinationAirportCode);
        });

        Assert.Contains(tickets, t => t.SeatNumber == "4A");
        Assert.Contains(tickets, t => t.SeatNumber == "4C");
    }

    [Fact]
    public void GetTicketsByClientDocument_WhenDocumentNotFoundOrEmpty_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService(UserRole.Passenger);

        // Act & Assert
        Assert.Empty(service.GetTicketsByClientDocument(string.Empty));
        Assert.Empty(service.GetTicketsByClientDocument("   "));
        Assert.Empty(service.GetTicketsByClientDocument("NON_EXISTING_DOCUMENT_NUMBER_12345"));
    }
}
