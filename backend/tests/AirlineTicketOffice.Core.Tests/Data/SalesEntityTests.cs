using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Tests.Data;

/// <summary>
/// Модульные тесты для проверки сущностей тарифов, клиентов, заказов и билетов (Tariff, Client, Order, Ticket).
/// </summary>
public class SalesEntityTests
{
    [Fact]
    public void CreateSalesEntities_DefaultAndAssignedValues_SetsPropertiesAndRelationshipsCorrectly()
    {
        // Arrange
        var origin = new Airport { Id = 1, Code = "SVO", City = "Москва", Name = "Шереметьево" };
        var destination = new Airport { Id = 2, Code = "LED", City = "Санкт-Петербург", Name = "Пулково" };

        var tariff = new Tariff
        {
            Id = 1,
            OriginAirportId = origin.Id,
            OriginAirport = origin,
            DestinationAirportId = destination.Id,
            DestinationAirport = destination,
            BasePrice = 4500.00m
        };

        var client = new Client
        {
            Id = 10,
            FullName = "Иванов Иван Иванович",
            DocumentNumber = "4510 123456",
            ContactPhone = "+79991234567",
            ContactEmail = "ivanov@example.com"
        };

        DateTime now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var order = new Order
        {
            Id = 100,
            OrderNumber = "ORD-1001",
            Channel = OrderChannel.SelfService,
            CreatedRole = UserRole.Passenger,
            TotalAmount = 6000.00m,
            Status = OrderStatus.Created,
            CreatedAt = now,
            HoldExpiresAt = now.AddMinutes(15)
        };

        var flight = new Flight
        {
            Id = 200,
            FlightNumber = "SU-101",
            OriginAirportId = origin.Id,
            DestinationAirportId = destination.Id,
            DepartureTime = now.AddDays(2),
            ArrivalTime = now.AddDays(2).AddHours(1.5)
        };

        var flightSeat = new FlightSeat
        {
            Id = 300,
            FlightId = flight.Id,
            SeatNumber = "12A",
            SeatClass = SeatClass.Economy,
            State = SeatState.Held
        };

        var ticket = new Ticket
        {
            Id = 1000,
            TicketNumber = "TKT-10001",
            OrderId = order.Id,
            Order = order,
            ClientId = client.Id,
            Client = client,
            FlightId = flight.Id,
            Flight = flight,
            FlightSeatId = flightSeat.Id,
            FlightSeat = flightSeat,
            Price = 6000.00m,
            HasBaggage = true,
            Status = TicketStatus.Booked
        };

        // Act
        order.Tickets.Add(ticket);
        client.Tickets.Add(ticket);
        flight.Tickets.Add(ticket);

        // Assert
        // Проверка сущности Tariff
        Assert.Equal(1, tariff.Id);
        Assert.Equal(1, tariff.OriginAirportId);
        Assert.Same(origin, tariff.OriginAirport);
        Assert.Equal(2, tariff.DestinationAirportId);
        Assert.Same(destination, tariff.DestinationAirport);
        Assert.Equal(4500.00m, tariff.BasePrice);
        Assert.Equal(1500.00m, tariff.BaggageFee);

        // Проверка сущности Client
        Assert.Equal(10, client.Id);
        Assert.Equal("Иванов Иван Иванович", client.FullName);
        Assert.Equal("4510 123456", client.DocumentNumber);
        Assert.Equal("+79991234567", client.ContactPhone);
        Assert.Equal("ivanov@example.com", client.ContactEmail);
        Assert.Single(client.Tickets);
        Assert.Same(ticket, client.Tickets.First());

        // Проверка сущности Order
        Assert.Equal(100, order.Id);
        Assert.Equal("ORD-1001", order.OrderNumber);
        Assert.Equal(OrderChannel.SelfService, order.Channel);
        Assert.Equal(UserRole.Passenger, order.CreatedRole);
        Assert.Equal(6000.00m, order.TotalAmount);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(now, order.CreatedAt);
        Assert.Equal(now.AddMinutes(15), order.HoldExpiresAt);
        Assert.Single(order.Tickets);
        Assert.Same(ticket, order.Tickets.First());

        // Проверка сущности Ticket
        Assert.Equal(1000, ticket.Id);
        Assert.Equal("TKT-10001", ticket.TicketNumber);
        Assert.Equal(100, ticket.OrderId);
        Assert.Same(order, ticket.Order);
        Assert.Equal(10, ticket.ClientId);
        Assert.Same(client, ticket.Client);
        Assert.Equal(200, ticket.FlightId);
        Assert.Same(flight, ticket.Flight);
        Assert.Equal(300, ticket.FlightSeatId);
        Assert.Same(flightSeat, ticket.FlightSeat);
        Assert.Equal(6000.00m, ticket.Price);
        Assert.True(ticket.HasBaggage);
        Assert.Equal(TicketStatus.Booked, ticket.Status);

        // Проверка навигационного свойства Flight.Tickets
        Assert.Single(flight.Tickets);
        Assert.Same(ticket, flight.Tickets.First());
    }
}
