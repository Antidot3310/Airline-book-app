using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Booking;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис бронирования, управления картой мест и удержания кресел (M6).
/// </summary>
public class BookingService : IBookingService
{
    private readonly AirlineDbContext _dbContext;
    private readonly ITariffCalculator _tariffCalculator;
    private readonly IVirtualClockService _clockService;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="BookingService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных авиакассы.</param>
    /// <param name="tariffCalculator">Калькулятор динамических тарифов.</param>
    /// <param name="clockService">Сервис системного виртуального времени.</param>
    public BookingService(
        AirlineDbContext dbContext,
        ITariffCalculator tariffCalculator,
        IVirtualClockService clockService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tariffCalculator = tariffCalculator ?? throw new ArgumentNullException(nameof(tariffCalculator));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
    }

    /// <inheritdoc />
    public Result<SeatMapDto> GetSeatMap(int flightId)
    {
        var flight = _dbContext.Flights
            .AsNoTracking()
            .Include(f => f.Aircraft)
                .ThenInclude(a => a!.AircraftType)
            .Include(f => f.Seats)
            .FirstOrDefault(f => f.Id == flightId);

        if (flight is null)
        {
            return Result<SeatMapDto>.Failure($"Рейс с идентификатором {flightId} не найден.");
        }

        var currentVirtualTime = _clockService.CurrentVirtualTime;
        var priceByClass = new Dictionary<SeatClass, decimal>();

        decimal GetPrice(SeatClass seatClass)
        {
            if (!priceByClass.TryGetValue(seatClass, out decimal price))
            {
                price = _tariffCalculator.CalculateTicketPrice(
                    flight.OriginAirportId,
                    flight.DestinationAirportId,
                    seatClass,
                    flight.DepartureTime,
                    hasBaggage: false);
                priceByClass[seatClass] = price;
            }

            return price;
        }

        var seatDtos = new List<SeatItemDto>(flight.Seats.Count);

        foreach (var seat in flight.Seats)
        {
            var state = seat.State;
            if (state == SeatState.Held && seat.HoldUntilTime.HasValue && seat.HoldUntilTime.Value <= currentVirtualTime)
            {
                state = SeatState.Available;
            }

            decimal price = GetPrice(seat.SeatClass);

            seatDtos.Add(new SeatItemDto
            {
                SeatId = seat.Id,
                SeatNumber = seat.SeatNumber,
                Class = seat.SeatClass,
                State = state,
                Price = price
            });
        }

        var sortedSeats = seatDtos
            .OrderBy(s => ParseSeatNumber(s.SeatNumber).Row)
            .ThenBy(s => ParseSeatNumber(s.SeatNumber).Letter, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var seatMap = new SeatMapDto
        {
            FlightId = flight.Id,
            FlightNumber = flight.FlightNumber,
            AircraftModel = flight.Aircraft?.AircraftType?.Name ?? string.Empty,
            Seats = sortedSeats
        };

        return Result<SeatMapDto>.Success(seatMap);
    }

    /// <inheritdoc />
    public Result<HoldSeatsResultDto> HoldSeats(int flightId, IReadOnlyList<string> seatNumbers)
    {
        return Result<HoldSeatsResultDto>.Failure("Метод будет реализован в Инкременте 20.");
    }

    /// <inheritdoc />
    public Result ReleaseSeats(int flightId, IReadOnlyList<string> seatNumbers)
    {
        return Result.Failure("Метод будет реализован в Инкременте 20.");
    }

    /// <summary>
    /// Разбирает обозначение места на числовой номер ряда и буквенный индекс (например, "12A" -> (12, "A")).
    /// </summary>
    private static (int Row, string Letter) ParseSeatNumber(string seatNumber)
    {
        if (string.IsNullOrWhiteSpace(seatNumber))
        {
            return (0, string.Empty);
        }

        int i = 0;
        while (i < seatNumber.Length && char.IsDigit(seatNumber[i]))
        {
            i++;
        }

        int row = 0;
        if (i > 0)
        {
            int.TryParse(seatNumber[..i], out row);
        }

        string letter = seatNumber[i..];
        return (row, letter);
    }
}
