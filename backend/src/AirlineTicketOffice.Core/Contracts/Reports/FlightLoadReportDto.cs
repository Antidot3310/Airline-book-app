namespace AirlineTicketOffice.Core.Contracts.Reports;

/// <summary>
/// DTO аналитического отчёта по загрузке рейсов за выбранный период.
/// </summary>
public class FlightLoadReportDto
{
    /// <summary>
    /// Начальная дата периода отчёта.
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// Конечная дата периода отчёта.
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Общее количество рейсов в выборке.
    /// </summary>
    public int TotalFlights { get; set; }

    /// <summary>
    /// Общая вместимость (суммарное количество мест) всех рейсов.
    /// </summary>
    public int TotalCapacity { get; set; }

    /// <summary>
    /// Общее количество проданных мест на всех рейсах.
    /// </summary>
    public int TotalSoldSeats { get; set; }

    /// <summary>
    /// Средний процент загрузки рейсов (0-100%).
    /// </summary>
    public double AverageLoadPercentage { get; set; }

    /// <summary>
    /// Детализированные строки загрузки по каждому рейсу.
    /// </summary>
    public IReadOnlyList<FlightLoadItemDto> Items { get; set; } = [];
}

/// <summary>
/// Элемент отчёта по загрузке конкретного рейса.
/// </summary>
public class FlightLoadItemDto
{
    /// <summary>
    /// Идентификатор рейса.
    /// </summary>
    public int FlightId { get; set; }

    /// <summary>
    /// Номер рейса.
    /// </summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>
    /// Маршрут рейса в формате "КодОткуда -> КодКуда" (например, "SVO -> LED").
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// Дата и время вылета рейса.
    /// </summary>
    public DateTime DepartureTime { get; set; }

    /// <summary>
    /// Модель воздушного судна.
    /// </summary>
    public string AircraftModel { get; set; } = string.Empty;

    /// <summary>
    /// Общее количество мест на рейсе.
    /// </summary>
    public int TotalSeats { get; set; }

    /// <summary>
    /// Количество проданных мест на рейсе.
    /// </summary>
    public int SoldSeats { get; set; }

    /// <summary>
    /// Процент занятости мест на рейсе (0-100%).
    /// </summary>
    public double LoadPercentage { get; set; }

    /// <summary>
    /// Общее количество мест бизнес-класса.
    /// </summary>
    public int BusinessSeatsTotal { get; set; }

    /// <summary>
    /// Количество проданных мест бизнес-класса.
    /// </summary>
    public int BusinessSeatsSold { get; set; }

    /// <summary>
    /// Процент занятости мест бизнес-класса (0-100%).
    /// </summary>
    public double BusinessLoadPercentage { get; set; }

    /// <summary>
    /// Общее количество мест эконом-класса.
    /// </summary>
    public int EconomySeatsTotal { get; set; }

    /// <summary>
    /// Количество проданных мест эконом-класса.
    /// </summary>
    public int EconomySeatsSold { get; set; }

    /// <summary>
    /// Процент занятости мест эконом-класса (0-100%).
    /// </summary>
    public double EconomyLoadPercentage { get; set; }
}
