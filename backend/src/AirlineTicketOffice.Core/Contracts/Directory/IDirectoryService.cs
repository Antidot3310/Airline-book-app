using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Directory;

/// <summary>
/// Интерфейс сервиса управления справочниками авиакомпании (M3).
/// Обеспечивает ведение справочников аэропортов, типов ВС и флота воздушных судов с контролем ссылочной целостности.
/// </summary>
public interface IDirectoryService
{
    /// <summary>
    /// Получает список всех аэропортов, отсортированный по городу и названию.
    /// </summary>
    /// <returns>Список DTO всех аэропортов.</returns>
    IReadOnlyList<AirportDto> GetAllAirports();

    /// <summary>
    /// Добавляет новый аэропорт в систему с валидацией IATA-кода и проверкой уникальности.
    /// </summary>
    /// <param name="airport">DTO добавляемого аэропорта.</param>
    /// <returns>Результат операции.</returns>
    Result AddAirport(AirportDto airport);

    /// <summary>
    /// Деактивирует аэропорт с проверкой отсутствия запланированных рейсов.
    /// </summary>
    /// <param name="airportId">Идентификатор деактивируемого аэропорта.</param>
    /// <returns>Результат операции.</returns>
    Result DeactivateAirport(int airportId);

    /// <summary>
    /// Получает список всех типов воздушных судов в системе.
    /// </summary>
    /// <returns>Список DTO типов воздушных судов.</returns>
    IReadOnlyList<AircraftTypeDto> GetAllAircraftTypes();

    /// <summary>
    /// Добавляет новый тип воздушного судна в систему.
    /// </summary>
    /// <param name="aircraftType">DTO добавляемого типа ВС.</param>
    /// <returns>Результат операции.</returns>
    Result AddAircraftType(AircraftTypeDto aircraftType);

    /// <summary>
    /// Получает список всех бортов флота авиакомпании с информацией о типе ВС.
    /// </summary>
    /// <returns>Список DTO бортов.</returns>
    IReadOnlyList<AircraftDto> GetAllAircrafts();

    /// <summary>
    /// Добавляет новый борт в авиапарк с проверкой уникальности бортового номера.
    /// </summary>
    /// <param name="aircraft">DTO добавляемого самолёта.</param>
    /// <returns>Результат операции.</returns>
    Result AddAircraft(AircraftDto aircraft);

    /// <summary>
    /// Изменяет эксплуатационный статус борта (например, перевод на техобслуживание или возврат в строй).
    /// </summary>
    /// <param name="aircraftId">Идентификатор борта.</param>
    /// <param name="status">Новый эксплуатационный статус.</param>
    /// <returns>Результат операции.</returns>
    Result SetAircraftStatus(int aircraftId, AircraftStatus status);
}
