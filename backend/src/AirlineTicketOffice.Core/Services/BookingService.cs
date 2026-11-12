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
        if (seatNumbers is null || seatNumbers.Count == 0 || seatNumbers.Count > 5)
        {
            return Result<HoldSeatsResultDto>.Failure("В одном заказе может быть выбрано от 1 до 5 мест.");
        }

        if (seatNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != seatNumbers.Count)
        {
            return Result<HoldSeatsResultDto>.Failure("Список мест содержит дубликаты.");
        }

        var flight = _dbContext.Flights.FirstOrDefault(f => f.Id == flightId);
        if (flight is null)
        {
            return Result<HoldSeatsResultDto>.Failure($"Рейс с идентификатором {flightId} не найден.");
        }

        if (flight.Status != FlightStatus.Scheduled)
        {
            return Result<HoldSeatsResultDto>.Failure("Бронирование мест доступно только для запланированных рейсов.");
        }

        using var transaction = _dbContext.Database.BeginTransaction();

        var seats = _dbContext.FlightSeats
            .Where(s => s.FlightId == flightId && seatNumbers.Contains(s.SeatNumber))
            .ToList();

        if (seats.Count != seatNumbers.Count)
        {
            transaction.Rollback();
            return Result<HoldSeatsResultDto>.Failure("Одно или несколько указанных мест не существуют на данном рейсе.");
        }

        var currentVirtualTime = _clockService.CurrentVirtualTime;

        foreach (var seat in seats)
        {
            bool isAvailable = seat.State == SeatState.Available ||
                (seat.State == SeatState.Held && seat.HoldUntilTime.HasValue && seat.HoldUntilTime.Value <= currentVirtualTime);

            if (!isAvailable)
            {
                transaction.Rollback();
                return Result<HoldSeatsResultDto>.Failure("Одно или несколько выбранных мест уже заняты.");
            }
        }

        var holdUntil = currentVirtualTime.AddMinutes(15);

        foreach (var seat in seats)
        {
            seat.State = SeatState.Held;
            seat.HoldUntilTime = holdUntil;
        }

        _dbContext.SaveChanges();
        transaction.Commit();

        return Result<HoldSeatsResultDto>.Success(new HoldSeatsResultDto
        {
            FlightId = flightId,
            SeatNumbers = seatNumbers.ToList(),
            HoldUntilTime = holdUntil
        });
    }

    /// <inheritdoc />
    public Result ReleaseSeats(int flightId, IReadOnlyList<string> seatNumbers)
    {
        if (seatNumbers is null || seatNumbers.Count == 0)
        {
            return Result.Success();
        }

        var seats = _dbContext.FlightSeats
            .Where(s => s.FlightId == flightId && seatNumbers.Contains(s.SeatNumber))
            .ToList();

        foreach (var seat in seats)
        {
            if (seat.State == SeatState.Held)
            {
                seat.State = SeatState.Available;
                seat.HoldUntilTime = null;
            }
        }

        _dbContext.SaveChanges();
        return Result.Success();
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
