using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Реализация сервиса управления расписанием рейсов и схемами посадочных мест (M4).
/// </summary>
public class FlightService : IFlightService
{
    private readonly AirlineDbContext _dbContext;
    private readonly IVirtualClockService _clockService;
    private readonly ITariffCalculator _tariffCalculator;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="FlightService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <param name="clockService">Сервис системного виртуального времени.</param>
    /// <param name="tariffCalculator">Калькулятор динамических тарифов.</param>
    /// <param name="auditLogService">Сервис журнала аудита действий.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если один из аргументов равен null.</exception>
    public FlightService(
        AirlineDbContext dbContext,
        IVirtualClockService clockService,
        ITariffCalculator tariffCalculator,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _tariffCalculator = tariffCalculator ?? throw new ArgumentNullException(nameof(tariffCalculator));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public Result<int> CreateFlight(CreateFlightRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.FlightNumber))
        {
            return Result<int>.Failure("Номер рейса обязателен для заполнения.");
        }

        if (request.OriginAirportId == request.DestinationAirportId)
        {
            return Result<int>.Failure("Аэропорты вылета и прилёта должны различаться.");
        }

        if (request.ArrivalTime <= request.DepartureTime)
        {
            return Result<int>.Failure("Время прилёта должно быть позже времени вылета.");
        }

        var originAirport = _dbContext.Airports.FirstOrDefault(a => a.Id == request.OriginAirportId);
        if (originAirport is null || !originAirport.IsActive)
        {
            return Result<int>.Failure("Аэропорт вылета не найден или деактивирован.");
        }

        var destinationAirport = _dbContext.Airports.FirstOrDefault(a => a.Id == request.DestinationAirportId);
        if (destinationAirport is null || !destinationAirport.IsActive)
        {
            return Result<int>.Failure("Аэропорт назначения не найден или деактивирован.");
        }

        var aircraft = _dbContext.Aircrafts
            .Include(a => a.AircraftType)
            .FirstOrDefault(a => a.Id == request.AircraftId);

        if (aircraft is null || !aircraft.IsActive)
        {
            return Result<int>.Failure("Самолёт не найден или деактивирован.");
        }

        if (aircraft.Status == AircraftStatus.Maintenance)
        {
            return Result<int>.Failure("Самолёт находится на техническом обслуживании и не может быть назначен на рейс.");
        }

        if (aircraft.Status != AircraftStatus.InService)
        {
            return Result<int>.Failure("Самолёт недоступен для назначения на рейс.");
        }

        if (aircraft.AircraftType is null)
        {
            return Result<int>.Failure("Тип воздушного судна для указанного самолёта не определён.");
        }

        var flight = new Flight
        {
            FlightNumber = request.FlightNumber.Trim().ToUpperInvariant(),
            OriginAirportId = request.OriginAirportId,
            DestinationAirportId = request.DestinationAirportId,
            AircraftId = request.AircraftId,
            DepartureTime = request.DepartureTime,
            ArrivalTime = request.ArrivalTime,
            Status = FlightStatus.Scheduled
        };

        GenerateSeatsForFlight(flight, aircraft.AircraftType);

        _dbContext.Flights.Add(flight);
        _dbContext.SaveChanges();

        _auditLogService.Log(
            "CreateFlight",
            nameof(Flight),
            flight.Id,
            $"Создан рейс {flight.FlightNumber} с генерацией {flight.Seats.Count} мест");

        return Result<int>.Success(flight.Id);
    }

    /// <inheritdoc />
    public Result<FlightDetailsDto> GetFlightById(int flightId)
    {
        var flight = _dbContext.Flights
            .AsNoTracking()
            .Include(f => f.OriginAirport)
            .Include(f => f.DestinationAirport)
            .Include(f => f.Aircraft)
                .ThenInclude(a => a!.AircraftType)
            .Include(f => f.Seats)
            .FirstOrDefault(f => f.Id == flightId);

        if (flight is null)
        {
            return Result<FlightDetailsDto>.Failure("Рейс не найден.");
        }

        var details = new FlightDetailsDto
        {
            Id = flight.Id,
            FlightNumber = flight.FlightNumber,
            OriginAirportId = flight.OriginAirportId,
            OriginAirportCode = flight.OriginAirport?.Code ?? string.Empty,
            OriginCity = flight.OriginAirport?.City ?? string.Empty,
            DestinationAirportId = flight.DestinationAirportId,
            DestinationAirportCode = flight.DestinationAirport?.Code ?? string.Empty,
            DestinationCity = flight.DestinationAirport?.City ?? string.Empty,
            AircraftId = flight.AircraftId,
            AircraftTailNumber = flight.Aircraft?.TailNumber ?? string.Empty,
            AircraftModel = flight.Aircraft?.AircraftType?.Name ?? string.Empty,
            DepartureTime = flight.DepartureTime,
            ArrivalTime = flight.ArrivalTime,
            Status = flight.Status,
            TotalSeats = flight.Seats.Count,
            AvailableSeats = flight.Seats.Count(s => s.State == SeatState.Available)
        };

        return Result<FlightDetailsDto>.Success(details);
    }

    /// <inheritdoc />
    public IReadOnlyList<FlightListItemDto> SearchFlights(
        int originAirportId,
        int destinationAirportId,
        DateTime flightDate,
        int passengersCount)
    {
        // Метод в разработке (Инкремент 17)
        return [];
    }

    /// <inheritdoc />
    public Result ChangeAircraft(int flightId, int newAircraftId)
    {
        // Метод в разработке (Инкремент 18)
        return Result.Failure("Метод в разработке (Инкремент 18).");
    }

    /// <inheritdoc />
    public Result CancelFlight(int flightId)
    {
        // Метод в разработке (Инкремент 18)
        return Result.Failure("Метод в разработке (Инкремент 18).");
    }

    private static void GenerateSeatsForFlight(Flight flight, AircraftType type)
    {
        byte[] defaultRowVersion = [0, 0, 0, 1];

        if (type.Name.Contains("Superjet"))
        {
            // Бизнес: ряды 1–3, A, C, D, F (12 мест)
            for (int row = 1; row <= 3; row++)
            {
                foreach (string letter in new[] { "A", "C", "D", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Business,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }

            // Эконом: ряды 4–18, A, C, D, E, F (75 мест)
            for (int row = 4; row <= 18; row++)
            {
                foreach (string letter in new[] { "A", "C", "D", "E", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Economy,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }
        }
        else if (type.Name.Contains("A320"))
        {
            // Бизнес: ряды 1–3, A, C, D, F (12 мест)
            for (int row = 1; row <= 3; row++)
            {
                foreach (string letter in new[] { "A", "C", "D", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Business,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }

            // Эконом: ряды 4–26, A, B, C, D, E, F (138 мест)
            for (int row = 4; row <= 26; row++)
            {
                foreach (string letter in new[] { "A", "B", "C", "D", "E", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Economy,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }
        }
        else if (type.Name.Contains("737"))
        {
            // Бизнес: ряды 1–4, A, C, D, F (16 мест)
            for (int row = 1; row <= 4; row++)
            {
                foreach (string letter in new[] { "A", "C", "D", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Business,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }

            // Эконом: ряды 5–30, A, B, C, D, E, F (152 места)
            for (int row = 5; row <= 30; row++)
            {
                foreach (string letter in new[] { "A", "B", "C", "D", "E", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Economy,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }
        }
        else if (type.Name.Contains("777"))
        {
            // Бизнес: ряды 1–8, A, C, E, H, K (40 мест)
            for (int row = 1; row <= 8; row++)
            {
                foreach (string letter in new[] { "A", "C", "E", "H", "K" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Business,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }

            // Эконом: ряды 9–45 (328 мест)
            string[] ecoLetters = ["A", "B", "C", "D", "E", "F", "G", "H", "J", "K"];
            int remainingEco = 328;
            for (int row = 9; row <= 45 && remainingEco > 0; row++)
            {
                foreach (string letter in ecoLetters)
                {
                    if (remainingEco == 0)
                    {
                        break;
                    }

                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Economy,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                    remainingEco--;
                }
            }
        }
        else if (type.Name.Contains("A350"))
        {
            // Бизнес: ряды 1–8, A, D, G, K (32 места)
            for (int row = 1; row <= 8; row++)
            {
                foreach (string letter in new[] { "A", "D", "G", "K" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Business,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }

            // Эконом: ряды 9–42 (284 места)
            string[] ecoLetters = ["A", "B", "C", "D", "E", "F", "G", "H", "K"];
            int remainingEco = 284;
            for (int row = 9; row <= 42 && remainingEco > 0; row++)
            {
                foreach (string letter in ecoLetters)
                {
                    if (remainingEco == 0)
                    {
                        break;
                    }

                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Economy,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                    remainingEco--;
                }
            }
        }
        else
        {
            // Общая раскладка для произвольного типа ВС
            for (int row = 1; row <= type.BusinessRowsCount; row++)
            {
                foreach (string letter in new[] { "A", "C", "D", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Business,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }

            int startEcoRow = type.BusinessRowsCount + 1;
            int endEcoRow = type.BusinessRowsCount + type.EconomyRowsCount;
            for (int row = startEcoRow; row <= endEcoRow; row++)
            {
                foreach (string letter in new[] { "A", "B", "C", "D", "E", "F" })
                {
                    flight.Seats.Add(new FlightSeat
                    {
                        Flight = flight,
                        SeatNumber = $"{row}{letter}",
                        SeatClass = SeatClass.Economy,
                        State = SeatState.Available,
                        RowVersion = defaultRowVersion
                    });
                }
            }
        }
    }
}
