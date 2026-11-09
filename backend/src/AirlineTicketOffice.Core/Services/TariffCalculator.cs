using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис расчёта динамических тарифов и управления базовой тарифной сеткой маршрутов (M5).
/// </summary>
public class TariffCalculator : ITariffCalculator
{
    private readonly AirlineDbContext _dbContext;
    private readonly IVirtualClockService _clockService;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="TariffCalculator"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <param name="clockService">Сервис виртуального времени.</param>
    /// <param name="auditLogService">Сервис аудита действий системы.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если один из аргументов равен null.</exception>
    public TariffCalculator(
        AirlineDbContext dbContext,
        IVirtualClockService clockService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _clockService = clockService ?? throw new ArgumentNullException(nameof(clockService));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public decimal CalculateTicketPrice(
        int originAirportId,
        int destinationAirportId,
        SeatClass seatClass,
        DateTime flightDepartureTime,
        bool hasBaggage)
    {
        var tariff = _dbContext.Tariffs
            .AsNoTracking()
            .FirstOrDefault(t => t.OriginAirportId == originAirportId && t.DestinationAirportId == destinationAirportId);

        if (tariff is null)
        {
            throw new InvalidOperationException($"Тариф для маршрута {originAirportId} -> {destinationAirportId} не найден.");
        }

        decimal classMultiplier = seatClass switch
        {
            SeatClass.Business => 2.0m,
            SeatClass.Economy => 1.0m,
            _ => throw new ArgumentOutOfRangeException(nameof(seatClass), seatClass, "Недопустимый класс посадочного места.")
        };

        double totalDays = (flightDepartureTime - _clockService.CurrentVirtualTime).TotalDays;
        decimal daysMultiplier;

        if (totalDays > 30.0)
        {
            daysMultiplier = 1.0m;
        }
        else if (totalDays >= 15.0)
        {
            daysMultiplier = 1.2m;
        }
        else if (totalDays >= 7.0)
        {
            daysMultiplier = 1.5m;
        }
        else
        {
            daysMultiplier = 1.8m;
        }

        decimal baggageCost = hasBaggage ? tariff.BaggageFee : 0m;
        decimal price = (tariff.BasePrice * classMultiplier * daysMultiplier) + baggageCost;

        return Math.Round(price, 2);
    }

    /// <inheritdoc />
    public IReadOnlyList<TariffDto> GetAllTariffs()
    {
        return _dbContext.Tariffs
            .AsNoTracking()
            .Include(t => t.OriginAirport)
            .Include(t => t.DestinationAirport)
            .OrderBy(t => t.OriginAirport != null ? t.OriginAirport.Code : string.Empty)
            .ThenBy(t => t.DestinationAirport != null ? t.DestinationAirport.Code : string.Empty)
            .Select(t => new TariffDto
            {
                Id = t.Id,
                OriginAirportId = t.OriginAirportId,
                OriginAirportCode = t.OriginAirport != null ? t.OriginAirport.Code : string.Empty,
                DestinationAirportId = t.DestinationAirportId,
                DestinationAirportCode = t.DestinationAirport != null ? t.DestinationAirport.Code : string.Empty,
                BasePrice = t.BasePrice,
                BaggageFee = t.BaggageFee
            })
            .ToList();
    }

    /// <inheritdoc />
    public Result SetBaseTariff(int originId, int destId, decimal basePrice, decimal baggageFee)
    {
        if (originId == destId)
        {
            return Result.Failure("Аэропорты вылета и прилёта не могут совпадать.");
        }

        if (basePrice <= 0)
        {
            return Result.Failure("Базовая цена тарифа должна быть строго больше нуля.");
        }

        if (baggageFee < 0)
        {
            return Result.Failure("Стоимость багажа не может быть отрицательной.");
        }

        var originAirport = _dbContext.Airports.FirstOrDefault(a => a.Id == originId);
        if (originAirport is null)
        {
            return Result.Failure($"Аэропорт вылета с идентификатором {originId} не найден.");
        }

        var destinationAirport = _dbContext.Airports.FirstOrDefault(a => a.Id == destId);
        if (destinationAirport is null)
        {
            return Result.Failure($"Аэропорт назначения с идентификатором {destId} не найден.");
        }

        var tariff = _dbContext.Tariffs.FirstOrDefault(t => t.OriginAirportId == originId && t.DestinationAirportId == destId);
        if (tariff is not null)
        {
            tariff.BasePrice = basePrice;
            tariff.BaggageFee = baggageFee;
        }
        else
        {
            tariff = new Tariff
            {
                OriginAirportId = originId,
                DestinationAirportId = destId,
                BasePrice = basePrice,
                BaggageFee = baggageFee
            };
            _dbContext.Tariffs.Add(tariff);
        }

        _dbContext.SaveChanges();

        _auditLogService.Log(
            "SetBaseTariff",
            "Tariff",
            tariff.Id,
            $"Установлен базовый тариф для маршрута {originAirport.Code} -> {destinationAirport.Code}: базовая ставка {basePrice:F2} руб., сбор за багаж {baggageFee:F2} руб.");

        return Result.Success();
    }
}
