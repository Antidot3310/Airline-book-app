using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Common;

namespace AirlineTicketOffice.Core.Contracts.Booking;

/// <summary>
/// Контракт сервиса бронирования, управления картой мест и удержания кресел (M6).
/// </summary>
public interface IBookingService
{
    /// <summary>
    /// Формирует актуальную карту мест рейса с динамически рассчитанными ценами и статусами доступности.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <returns>Результат с картой мест рейса <see cref="SeatMapDto"/>.</returns>
    Result<SeatMapDto> GetSeatMap(int flightId);

    /// <summary>
    /// Выполняет атомарную попытку временного удержания (Hold) набора мест на 15 минут виртуального времени.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <param name="seatNumbers">Список номеров мест для удержания.</param>
    /// <returns>Результат с информацией об удержанных местах <see cref="HoldSeatsResultDto"/>.</returns>
    Result<HoldSeatsResultDto> HoldSeats(int flightId, IReadOnlyList<string> seatNumbers);

    /// <summary>
    /// Снимает временное удержание с указанных мест рейса вручную и возвращает их в состояние Available.
    /// </summary>
    /// <param name="flightId">Идентификатор рейса.</param>
    /// <param name="seatNumbers">Список номеров мест для снятия удержания.</param>
    /// <returns>Результат операции <see cref="Result"/>.</returns>
    Result ReleaseSeats(int flightId, IReadOnlyList<string> seatNumbers);
}
