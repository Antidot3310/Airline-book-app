using System;
using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Common;

namespace AirlineTicketOffice.Core.Contracts.Flights;

/// <summary>
/// Контракт сервиса управления расписанием рейсов и схемами посадочных мест (M4).
/// </summary>
public interface IFlightService
{
    /// <summary>
    /// Создаёт новый рейс в расписании и выполняет автогенерацию схемы посадочных мест.
    /// </summary>
    /// <param name="request">Параметры создаваемого рейса.</param>
    /// <returns>Идентификатор созданного рейса или ошибка валидации.</returns>
    Result<int> CreateFlight(CreateFlightRequest request);

    /// <summary>
    /// Возвращает подробную информацию о рейсе по его уникальному идентификатору.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <returns>Детальные сведения о рейсе или ошибка, если рейс не найден.</returns>
    Result<FlightDetailsDto> GetFlightById(int flightId);

    /// <summary>
    /// Выполняет поиск доступных рейсов по маршруту, дате и количеству требуемых мест.
    /// </summary>
    /// <param name="originAirportId">Идентификатор аэропорта вылета.</param>
    /// <param name="destinationAirportId">Идентификатор аэропорта назначения.</param>
    /// <param name="flightDate">Дата вылета рейса.</param>
    /// <param name="passengersCount">Количество пассажиров в заказе.</param>
    /// <returns>Список рейсов, удовлетворяющих критериям поиска.</returns>
    IReadOnlyList<FlightListItemDto> SearchFlights(
        int originAirportId,
        int destinationAirportId,
        DateTime flightDate,
        int passengersCount);

    /// <summary>
    /// Выполняет замену воздушного судна на запланированном рейсе с регенерацией мест.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <param name="newAircraftId">Идентификатор нового воздушного судна.</param>
    /// <returns>Результат выполнения операции <see cref="Result"/>.</returns>
    Result ChangeAircraft(int flightId, int newAircraftId);

    /// <summary>
    /// Выполняет отмену рейса администратором с запуском массового 100% возврата билетов.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <returns>Результат выполнения операции <see cref="Result"/>.</returns>
    Result CancelFlight(int flightId);
}
