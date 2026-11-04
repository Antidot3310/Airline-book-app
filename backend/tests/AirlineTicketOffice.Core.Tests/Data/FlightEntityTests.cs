using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Tests.Data;

/// <summary>
/// Модульные тесты для проверки сущностей расписания и схемы мест (Flight, FlightSeat).
/// </summary>
public class FlightEntityTests
{
    [Fact]
    public void CreateFlightAndFlightSeat_DefaultAndAssignedValues_SetsPropertiesAndRelationshipsCorrectly()
    {
        // Arrange
        var origin = new Airport { Id = 1, Code = "SVO", City = "Москва", Name = "Шереметьево" };
        var destination = new Airport { Id = 2, Code = "LED", City = "Санкт-Петербург", Name = "Пулково" };
        var aircraft = new Aircraft { Id = 10, TailNumber = "RA-89014", AircraftTypeId = 1 };

        DateTime departure = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc);
        DateTime arrival = departure.AddHours(1.5);

        var flight = new Flight
        {
            Id = 100,
            FlightNumber = "SU-101",
            OriginAirportId = origin.Id,
            OriginAirport = origin,
            DestinationAirportId = destination.Id,
            DestinationAirport = destination,
            AircraftId = aircraft.Id,
            Aircraft = aircraft,
            DepartureTime = departure,
            ArrivalTime = arrival,
            Status = FlightStatus.Scheduled
        };

        byte[] rowVersion = [0x00, 0x01, 0x02, 0x03];
        var seat = new FlightSeat
        {
            Id = 1001,
            FlightId = flight.Id,
            Flight = flight,
            SeatNumber = "1A",
            SeatClass = SeatClass.Business,
            State = SeatState.Available,
            HoldUntilTime = null,
            RowVersion = rowVersion
        };

        // Act
        flight.Seats.Add(seat);

        // Assert
        // Проверка свойств Flight
        Assert.Equal(100, flight.Id);
        Assert.Equal("SU-101", flight.FlightNumber);
        Assert.Equal(1, flight.OriginAirportId);
        Assert.Same(origin, flight.OriginAirport);
        Assert.Equal(2, flight.DestinationAirportId);
        Assert.Same(destination, flight.DestinationAirport);
        Assert.Equal(10, flight.AircraftId);
        Assert.Same(aircraft, flight.Aircraft);
        Assert.Equal(departure, flight.DepartureTime);
        Assert.Equal(arrival, flight.ArrivalTime);
        Assert.Equal(FlightStatus.Scheduled, flight.Status);
        Assert.Single(flight.Seats);
        Assert.Same(seat, flight.Seats.First());

        // Проверка свойств FlightSeat
        Assert.Equal(1001, seat.Id);
        Assert.Equal(100, seat.FlightId);
        Assert.Same(flight, seat.Flight);
        Assert.Equal("1A", seat.SeatNumber);
        Assert.Equal(SeatClass.Business, seat.SeatClass);
        Assert.Equal(SeatState.Available, seat.State);
        Assert.Null(seat.HoldUntilTime);
        Assert.Equal(rowVersion, seat.RowVersion);
    }
}
