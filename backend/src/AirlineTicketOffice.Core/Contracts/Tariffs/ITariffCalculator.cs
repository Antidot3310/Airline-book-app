using System;
using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Tariffs;

/// <summary>
/// Контракт сервиса расчёта динамических тарифов и управления тарифной сеткой маршрутов (M5).
/// </summary>
public interface ITariffCalculator
{
    /// <summary>
    /// Вычисляет итоговую стоимость билета с учётом класса обслуживания,
    /// коэффициента срочности до времени вылета рейса и опции провоза багажа.
    /// </summary>
    /// <param name="originAirportId">Идентификатор аэропорта вылета.</param>
    /// <param name="destinationAirportId">Идентификатор аэропорта назначения.</param>
    /// <param name="seatClass">Класс посадочного места (эконом или бизнес).</param>
    /// <param name="flightDepartureTime">Запланированное время вылета рейса.</param>
    /// <param name="hasBaggage">Признак включения платного багажного места.</param>
    /// <returns>Итоговая стоимость билета в рублях, округлённая до 2 знаков после запятой.</returns>
    /// <exception cref="InvalidOperationException">Выбрасывается, если базовый тариф для маршрута не найден.</exception>
    decimal CalculateTicketPrice(
        int originAirportId,
        int destinationAirportId,
        SeatClass seatClass,
        DateTime flightDepartureTime,
        bool hasBaggage);

    /// <summary>
    /// Возвращает перечень всех зарегистрированных базовых тарифов в системе.
    /// </summary>
    /// <returns>Список DTO тарифов.</returns>
    IReadOnlyList<TariffDto> GetAllTariffs();

    /// <summary>
    /// Устанавливает или обновляет базовый тариф и стоимость багажа для маршрута между двумя аэропортами.
    /// </summary>
    /// <param name="originId">Идентификатор аэропорта вылета.</param>
    /// <param name="destId">Идентификатор аэропорта назначения.</param>
    /// <param name="basePrice">Базовая стоимость тарифа (строго больше нуля).</param>
    /// <param name="baggageFee">Стоимость провоза багажа (неотрицательное значение).</param>
    /// <returns>Результат операции <see cref="Result"/>.</returns>
    Result SetBaseTariff(int originId, int destId, decimal basePrice, decimal baggageFee);
}
