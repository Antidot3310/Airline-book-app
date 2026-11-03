using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Data;

/// <summary>
/// Инициализатор базы данных с загрузкой эталонного начального набора данных (Seed Data).
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Выполняет первичное наполнение базы данных эталонными справочниками, парком ВС, тарифами и рейсами.
    /// Если база уже содержит данные, операция пропускается.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    public static void Initialize(AirlineDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Проверка: если база уже наполнена — пропускаем
        if (context.Airports.Any())
        {
            return;
        }

        // 1. Инициализация системного времени (T0 = 01.11.2026 08:00:00)
        var systemTime = new SystemTimeState
        {
            Id = 1,
            CurrentVirtualTime = new DateTime(2026, 11, 1, 8, 0, 0),
            Mode = ClockMode.Manual,
            SpeedMultiplier = 1.0
        };
        context.SystemTimeStates.Add(systemTime);

        // 2. Справочник аэропортов (55 аэропортов мира)
        var airports = CreateAirports();
        context.Airports.AddRange(airports.Values);
        context.SaveChanges();

        // 3. Типы воздушных судов (5 моделей)
        var aircraftTypes = CreateAircraftTypes();
        context.AircraftTypes.AddRange(aircraftTypes.Values);
        context.SaveChanges();

        // 4. Парк конкретных бортов (7 воздушных судов)
        var aircrafts = CreateAircrafts(aircraftTypes);
        context.Aircrafts.AddRange(aircrafts.Values);
        context.SaveChanges();

        // 5. Маршрутные тарифы (15 направлений)
        var tariffs = CreateTariffs(airports);
        context.Tariffs.AddRange(tariffs);
        context.SaveChanges();

        // 6. Сетка стартовых рейсов (8 рейсов) с генерацией мест FlightSeat
        var flights = CreateFlights(airports, aircrafts);
        context.Flights.AddRange(flights);
        context.SaveChanges();
    }

    private static Dictionary<string, Airport> CreateAirports()
    {
        var list = new List<Airport>
        {
            // 2.1. Беларусь и Россия (Базовый регион)
            new() { Code = "MSQ", City = "Минск", Name = "Национальный аэропорт Минск", IsActive = true },
            new() { Code = "SVO", City = "Москва", Name = "Шереметьево", IsActive = true },
            new() { Code = "LED", City = "Санкт-Петербург", Name = "Пулково", IsActive = true },
            new() { Code = "AER", City = "Сочи", Name = "Адлер", IsActive = true },
            new() { Code = "KZN", City = "Казань", Name = "Казань", IsActive = true },
            new() { Code = "OVB", City = "Новосибирск", Name = "Толмачёво", IsActive = true },

            // 2.2. Европа (15 аэропортов)
            new() { Code = "IST", City = "Стамбул", Name = "Стамбул", IsActive = true },
            new() { Code = "BEG", City = "Белград", Name = "Никола Тесла", IsActive = true },
            new() { Code = "CDG", City = "Париж", Name = "Шарль-де-Голль", IsActive = true },
            new() { Code = "LHR", City = "Лондон", Name = "Хитроу", IsActive = true },
            new() { Code = "FRA", City = "Франкфурт", Name = "Франкфурт", IsActive = true },
            new() { Code = "AMS", City = "Амстердам", Name = "Схипхол", IsActive = true },
            new() { Code = "FCO", City = "Рим", Name = "Фьюмичино", IsActive = true },
            new() { Code = "MAD", City = "Мадрид", Name = "Барахас", IsActive = true },
            new() { Code = "VIE", City = "Вена", Name = "Вена", IsActive = true },
            new() { Code = "PRG", City = "Прага", Name = "Вацлав Гавел", IsActive = true },
            new() { Code = "BUD", City = "Будапешт", Name = "Ференц Лист", IsActive = true },
            new() { Code = "ATH", City = "Афины", Name = "Афины", IsActive = true },
            new() { Code = "WAW", City = "Варшава", Name = "Шопен", IsActive = true },
            new() { Code = "ZRH", City = "Цюрих", Name = "Цюрих", IsActive = true },
            new() { Code = "LIS", City = "Лиссабон", Name = "Лиссабон", IsActive = true },

            // 2.3. Азия (12 аэропортов)
            new() { Code = "DXB", City = "Дубай", Name = "Дубай", IsActive = true },
            new() { Code = "DOH", City = "Доха", Name = "Хамад", IsActive = true },
            new() { Code = "PEK", City = "Пекин", Name = "Шоуду", IsActive = true },
            new() { Code = "PVG", City = "Шанхай", Name = "Пудун", IsActive = true },
            new() { Code = "HND", City = "Токио", Name = "Ханеда", IsActive = true },
            new() { Code = "ICN", City = "Сеул", Name = "Инчхон", IsActive = true },
            new() { Code = "SIN", City = "Сингапур", Name = "Чанги", IsActive = true },
            new() { Code = "BKK", City = "Бангкок", Name = "Суварнабхуми", IsActive = true },
            new() { Code = "DEL", City = "Дели", Name = "Индира Ганди", IsActive = true },
            new() { Code = "ALA", City = "Алматы", Name = "Алматы", IsActive = true },
            new() { Code = "TAS", City = "Ташкент", Name = "Ташкент", IsActive = true },
            new() { Code = "GYD", City = "Баку", Name = "Гейдар Алиев", IsActive = true },

            // 2.4. США (4 аэропорта)
            new() { Code = "JFK", City = "Нью-Йорк", Name = "им. Джона Кеннеди", IsActive = true },
            new() { Code = "LAX", City = "Лос-Анджелес", Name = "Лос-Анджелес", IsActive = true },
            new() { Code = "ORD", City = "Чикаго", Name = "О'Хара", IsActive = true },
            new() { Code = "MIA", City = "Майами", Name = "Майами", IsActive = true },

            // 2.5. Южная и Центральная Америка (11 аэропортов)
            new() { Code = "GRU", City = "Сан-Паулу", Name = "Гуарульос", IsActive = true },
            new() { Code = "GIG", City = "Рио-де-Жанейро", Name = "Галеан", IsActive = true },
            new() { Code = "EZE", City = "Буэнос-Айрес", Name = "Эсейса", IsActive = true },
            new() { Code = "LIM", City = "Лима", Name = "Хорхе Чавес", IsActive = true },
            new() { Code = "BOG", City = "Богота", Name = "Эль-Дорадо", IsActive = true },
            new() { Code = "SCL", City = "Сантьяго", Name = "Сантьяго", IsActive = true },
            new() { Code = "MEX", City = "Мехико", Name = "Бенито Хуарес", IsActive = true },
            new() { Code = "CUN", City = "Канкун", Name = "Канкун", IsActive = true },
            new() { Code = "HAV", City = "Гавана", Name = "Хосе Марти", IsActive = true },
            new() { Code = "PTY", City = "Панама-Сити", Name = "Токумен", IsActive = true },
            new() { Code = "MVD", City = "Монтевидео", Name = "Карраско", IsActive = true },

            // 2.6. Африка (7 аэропортов)
            new() { Code = "CAI", City = "Каир", Name = "Каир", IsActive = true },
            new() { Code = "CMN", City = "Касабланка", Name = "Мохаммед V", IsActive = true },
            new() { Code = "JNB", City = "Йоханнесбург", Name = "О. Р. Тамбо", IsActive = true },
            new() { Code = "CPT", City = "Кейптаун", Name = "Кейптаун", IsActive = true },
            new() { Code = "NBO", City = "Найроби", Name = "Джомо Кениата", IsActive = true },
            new() { Code = "ADD", City = "Аддис-Абеба", Name = "Боле", IsActive = true },
            new() { Code = "TUN", City = "Тунис", Name = "Карфаген", IsActive = true }
        };

        return list.ToDictionary(a => a.Code, a => a);
    }

    private static Dictionary<string, AircraftType> CreateAircraftTypes()
    {
        var list = new List<AircraftType>
        {
            new()
            {
                Name = "Sukhoi Superjet 100",
                TotalSeats = 87,
                BusinessRowsCount = 3,
                EconomyRowsCount = 15,
                LayoutConfigJson = "{\"model\":\"Sukhoi Superjet 100\",\"business\":{\"rows\":[1,3],\"seats\":[\"A\",\"C\",\"D\",\"F\"]},\"economy\":{\"rows\":[4,18],\"seats\":[\"A\",\"C\",\"D\",\"E\",\"F\"]}}",
                IsActive = true
            },
            new()
            {
                Name = "Airbus A320-200",
                TotalSeats = 150,
                BusinessRowsCount = 3,
                EconomyRowsCount = 23,
                LayoutConfigJson = "{\"model\":\"Airbus A320-200\",\"business\":{\"rows\":[1,3],\"seats\":[\"A\",\"C\",\"D\",\"F\"]},\"economy\":{\"rows\":[4,26],\"seats\":[\"A\",\"B\",\"C\",\"D\",\"E\",\"F\"]}}",
                IsActive = true
            },
            new()
            {
                Name = "Boeing 737-800",
                TotalSeats = 168,
                BusinessRowsCount = 4,
                EconomyRowsCount = 26,
                LayoutConfigJson = "{\"model\":\"Boeing 737-800\",\"business\":{\"rows\":[1,4],\"seats\":[\"A\",\"C\",\"D\",\"F\"]},\"economy\":{\"rows\":[5,30],\"seats\":[\"A\",\"B\",\"C\",\"D\",\"E\",\"F\"]}}",
                IsActive = true
            },
            new()
            {
                Name = "Boeing 777-300ER",
                TotalSeats = 368,
                BusinessRowsCount = 8,
                EconomyRowsCount = 37,
                LayoutConfigJson = "{\"model\":\"Boeing 777-300ER\",\"business\":{\"rows\":[1,8],\"seats\":[\"A\",\"C\",\"E\",\"H\",\"K\"]},\"economy\":{\"rows\":[9,45],\"seats\":[\"A\",\"B\",\"C\",\"D\",\"E\",\"F\",\"G\",\"H\",\"J\",\"K\"]}}",
                IsActive = true
            },
            new()
            {
                Name = "Airbus A350-900",
                TotalSeats = 316,
                BusinessRowsCount = 8,
                EconomyRowsCount = 34,
                LayoutConfigJson = "{\"model\":\"Airbus A350-900\",\"business\":{\"rows\":[1,8],\"seats\":[\"A\",\"D\",\"G\",\"K\"]},\"economy\":{\"rows\":[9,42],\"seats\":[\"A\",\"B\",\"C\",\"D\",\"E\",\"F\",\"G\",\"H\",\"K\"]}}",
                IsActive = true
            }
        };

        return list.ToDictionary(t => t.Name, t => t);
    }

    private static Dictionary<string, Aircraft> CreateAircrafts(Dictionary<string, AircraftType> types)
    {
        var list = new List<Aircraft>
        {
            new() { TailNumber = "RA-89014", AircraftType = types["Sukhoi Superjet 100"], Status = AircraftStatus.InService, IsActive = true },
            new() { TailNumber = "RA-89088", AircraftType = types["Sukhoi Superjet 100"], Status = AircraftStatus.Maintenance, IsActive = true },
            new() { TailNumber = "RA-73015", AircraftType = types["Airbus A320-200"], Status = AircraftStatus.InService, IsActive = true },
            new() { TailNumber = "RA-73022", AircraftType = types["Airbus A320-200"], Status = AircraftStatus.InService, IsActive = true },
            new() { TailNumber = "RA-73801", AircraftType = types["Boeing 737-800"], Status = AircraftStatus.InService, IsActive = true },
            new() { TailNumber = "RA-77005", AircraftType = types["Boeing 777-300ER"], Status = AircraftStatus.InService, IsActive = true },
            new() { TailNumber = "RA-75001", AircraftType = types["Airbus A350-900"], Status = AircraftStatus.InService, IsActive = true }
        };

        return list.ToDictionary(a => a.TailNumber, a => a);
    }

    private static List<Tariff> CreateTariffs(Dictionary<string, Airport> airports)
    {
        return
        [
            // 1. Минск (MSQ) <-> Москва (SVO)
            new Tariff { OriginAirport = airports["MSQ"], DestinationAirport = airports["SVO"], BasePrice = 5500.00m, BaggageFee = 1500.00m },
            // 2. Минск (MSQ) <-> Санкт-Петербург (LED)
            new Tariff { OriginAirport = airports["MSQ"], DestinationAirport = airports["LED"], BasePrice = 5600.00m, BaggageFee = 1500.00m },
            // 3. Минск (MSQ) <-> Стамбул (IST)
            new Tariff { OriginAirport = airports["MSQ"], DestinationAirport = airports["IST"], BasePrice = 9000.00m, BaggageFee = 1500.00m },
            // 4. Минск (MSQ) <-> Баку (GYD)
            new Tariff { OriginAirport = airports["MSQ"], DestinationAirport = airports["GYD"], BasePrice = 12500.00m, BaggageFee = 1500.00m },
            // 5. Минск (MSQ) <-> Дубай (DXB)
            new Tariff { OriginAirport = airports["MSQ"], DestinationAirport = airports["DXB"], BasePrice = 20000.00m, BaggageFee = 1500.00m },
            // 6. Москва (SVO) <-> Санкт-Петербург (LED)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["LED"], BasePrice = 4500.00m, BaggageFee = 1500.00m },
            // 7. Москва (SVO) <-> Сочи (AER)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["AER"], BasePrice = 7500.00m, BaggageFee = 1500.00m },
            // 8. Москва (SVO) <-> Казань (KZN)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["KZN"], BasePrice = 5000.00m, BaggageFee = 1500.00m },
            // 9. Москва (SVO) <-> Новосибирск (OVB)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["OVB"], BasePrice = 11000.00m, BaggageFee = 1500.00m },
            // 10. Москва (SVO) <-> Дубай (DXB)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["DXB"], BasePrice = 19000.00m, BaggageFee = 1500.00m },
            // 11. Москва (SVO) <-> Стамбул (IST)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["IST"], BasePrice = 10500.00m, BaggageFee = 1500.00m },
            // 12. Москва (SVO) <-> Каир (CAI)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["CAI"], BasePrice = 15500.00m, BaggageFee = 1500.00m },
            // 13. Москва (SVO) <-> Пекин (PEK)
            new Tariff { OriginAirport = airports["SVO"], DestinationAirport = airports["PEK"], BasePrice = 28500.00m, BaggageFee = 1500.00m },
            // 14. Стамбул (IST) <-> Париж (CDG)
            new Tariff { OriginAirport = airports["IST"], DestinationAirport = airports["CDG"], BasePrice = 12700.00m, BaggageFee = 1500.00m },
            // 15. Дубай (DXB) <-> Бангкок (BKK)
            new Tariff { OriginAirport = airports["DXB"], DestinationAirport = airports["BKK"], BasePrice = 24500.00m, BaggageFee = 1500.00m }
        ];
    }

    private static List<Flight> CreateFlights(
        Dictionary<string, Airport> airports,
        Dictionary<string, Aircraft> aircrafts)
    {
        var flights = new List<Flight>
        {
            // SU-101: SVO -> LED, RA-89014 (SSJ), T0 + 36ч
            new()
            {
                FlightNumber = "SU-101",
                OriginAirport = airports["SVO"],
                DestinationAirport = airports["LED"],
                Aircraft = aircrafts["RA-89014"],
                DepartureTime = new DateTime(2026, 11, 2, 20, 0, 0),
                ArrivalTime = new DateTime(2026, 11, 2, 21, 30, 0),
                Status = FlightStatus.Scheduled
            },
            // SU-102: LED -> SVO, RA-89014 (SSJ), T0 + 12ч
            new()
            {
                FlightNumber = "SU-102",
                OriginAirport = airports["LED"],
                DestinationAirport = airports["SVO"],
                Aircraft = aircrafts["RA-89014"],
                DepartureTime = new DateTime(2026, 11, 1, 20, 0, 0),
                ArrivalTime = new DateTime(2026, 11, 1, 21, 30, 0),
                Status = FlightStatus.Scheduled
            },
            // SU-201: SVO -> AER, RA-73015 (A320), T0 + 3дн
            new()
            {
                FlightNumber = "SU-201",
                OriginAirport = airports["SVO"],
                DestinationAirport = airports["AER"],
                Aircraft = aircrafts["RA-73015"],
                DepartureTime = new DateTime(2026, 11, 4, 10, 0, 0),
                ArrivalTime = new DateTime(2026, 11, 4, 13, 45, 0),
                Status = FlightStatus.Scheduled
            },
            // B2-701: MSQ -> SVO, RA-73801 (B737), T0 + 5ч
            new()
            {
                FlightNumber = "B2-701",
                OriginAirport = airports["MSQ"],
                DestinationAirport = airports["SVO"],
                Aircraft = aircrafts["RA-73801"],
                DepartureTime = new DateTime(2026, 11, 1, 13, 0, 0),
                ArrivalTime = new DateTime(2026, 11, 1, 14, 25, 0),
                Status = FlightStatus.Scheduled
            },
            // B2-731: MSQ -> IST, RA-73801 (B737), T0 + 1дн
            new()
            {
                FlightNumber = "B2-731",
                OriginAirport = airports["MSQ"],
                DestinationAirport = airports["IST"],
                Aircraft = aircrafts["RA-73801"],
                DepartureTime = new DateTime(2026, 11, 2, 9, 30, 0),
                ArrivalTime = new DateTime(2026, 11, 2, 14, 30, 0),
                Status = FlightStatus.Scheduled
            },
            // B2-755: MSQ -> DXB, RA-75001 (A350), T0 + 2дн
            new()
            {
                FlightNumber = "B2-755",
                OriginAirport = airports["MSQ"],
                DestinationAirport = airports["DXB"],
                Aircraft = aircrafts["RA-75001"],
                DepartureTime = new DateTime(2026, 11, 3, 4, 0, 0),
                ArrivalTime = new DateTime(2026, 11, 3, 11, 30, 0),
                Status = FlightStatus.Scheduled
            },
            // SU-320: SVO -> PEK, RA-77005 (B777), T0 + 4дн
            new()
            {
                FlightNumber = "SU-320",
                OriginAirport = airports["SVO"],
                DestinationAirport = airports["PEK"],
                Aircraft = aircrafts["RA-77005"],
                DepartureTime = new DateTime(2026, 11, 5, 19, 30, 0),
                ArrivalTime = new DateTime(2026, 11, 6, 8, 30, 0),
                Status = FlightStatus.Scheduled
            },
            // SU-505: SVO -> CAI, RA-73022 (A320), T0 + 2дн
            new()
            {
                FlightNumber = "SU-505",
                OriginAirport = airports["SVO"],
                DestinationAirport = airports["CAI"],
                Aircraft = aircrafts["RA-73022"],
                DepartureTime = new DateTime(2026, 11, 3, 14, 0, 0),
                ArrivalTime = new DateTime(2026, 11, 3, 19, 30, 0),
                Status = FlightStatus.Scheduled
            }
        };

        // Генерация схемы мест для каждого рейса
        foreach (var flight in flights)
        {
            if (flight.Aircraft?.AircraftType != null)
            {
                GenerateSeatsForFlight(flight, flight.Aircraft.AircraftType);
            }
        }

        return flights;
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
    }
}
