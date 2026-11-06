using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Реализация сервиса управления справочниками авиакомпании (M3).
/// Обеспечивает синхронные операции чтения и модификации данных с контролем ссылочной целостности и аудитом.
/// </summary>
public class DirectoryService : IDirectoryService
{
    private readonly AirlineDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="DirectoryService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если один из аргументов равен null.</exception>
    public DirectoryService(AirlineDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public IReadOnlyList<AirportDto> GetAllAirports()
    {
        return _dbContext.Airports
            .AsNoTracking()
            .OrderBy(a => a.City)
            .ThenBy(a => a.Name)
            .Select(a => new AirportDto
            {
                Id = a.Id,
                Code = a.Code,
                City = a.City,
                Name = a.Name,
                IsActive = a.IsActive
            })
            .ToList();
    }

    /// <inheritdoc />
    public Result AddAirport(AirportDto airport)
    {
        if (airport is null)
        {
            return Result.Failure("Данные аэропорта не указаны.");
        }

        if (string.IsNullOrWhiteSpace(airport.Code))
        {
            return Result.Failure("Код IATA аэропорта обязателен для заполнения.");
        }

        if (string.IsNullOrWhiteSpace(airport.City))
        {
            return Result.Failure("Город расположения аэропорта обязателен для заполнения.");
        }

        if (string.IsNullOrWhiteSpace(airport.Name))
        {
            return Result.Failure("Название аэропорта обязательно для заполнения.");
        }

        var code = airport.Code.Trim().ToUpperInvariant();
        var exists = _dbContext.Airports.Any(a => a.Code == code);
        if (exists)
        {
            return Result.Failure("Аэропорт с таким кодом уже зарегистрирован.");
        }

        var entity = new Airport
        {
            Code = code,
            City = airport.City.Trim(),
            Name = airport.Name.Trim(),
            IsActive = airport.IsActive
        };

        _dbContext.Airports.Add(entity);
        _dbContext.SaveChanges();

        _auditLogService.Log("AddAirport", "Airport", entity.Id, $"Добавлен аэропорт '{entity.Code}' ({entity.City}, {entity.Name})");

        return Result.Success();
    }

    /// <inheritdoc />
    public Result DeactivateAirport(int airportId)
    {
        var airport = _dbContext.Airports.FirstOrDefault(a => a.Id == airportId);
        if (airport is null)
        {
            return Result.Failure("Аэропорт не найден.");
        }

        var isUsedInScheduled = _dbContext.Flights.Any(f =>
            f.Status == FlightStatus.Scheduled &&
            (f.OriginAirportId == airportId || f.DestinationAirportId == airportId));

        if (isUsedInScheduled)
        {
            return Result.Failure("Невозможно деактивировать аэропорт: он задействован в запланированных рейсах.");
        }

        airport.IsActive = false;
        _dbContext.SaveChanges();

        _auditLogService.Log("DeactivateAirport", "Airport", airport.Id, $"Деактивирован аэропорт '{airport.Code}' ({airport.City})");

        return Result.Success();
    }

    /// <inheritdoc />
    public IReadOnlyList<AircraftTypeDto> GetAllAircraftTypes()
    {
        return _dbContext.AircraftTypes
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new AircraftTypeDto
            {
                Id = t.Id,
                Name = t.Name,
                TotalSeats = t.TotalSeats,
                BusinessRowsCount = t.BusinessRowsCount,
                EconomyRowsCount = t.EconomyRowsCount,
                LayoutConfigJson = t.LayoutConfigJson,
                IsActive = t.IsActive
            })
            .ToList();
    }

    /// <inheritdoc />
    public Result AddAircraftType(AircraftTypeDto aircraftType)
    {
        if (aircraftType is null)
        {
            return Result.Failure("Данные типа ВС не указаны.");
        }

        if (string.IsNullOrWhiteSpace(aircraftType.Name))
        {
            return Result.Failure("Наименование типа ВС обязательно для заполнения.");
        }

        if (aircraftType.TotalSeats <= 0)
        {
            return Result.Failure("Количество мест должно быть больше нуля.");
        }

        var entity = new AircraftType
        {
            Name = aircraftType.Name.Trim(),
            TotalSeats = aircraftType.TotalSeats,
            BusinessRowsCount = aircraftType.BusinessRowsCount,
            EconomyRowsCount = aircraftType.EconomyRowsCount,
            LayoutConfigJson = aircraftType.LayoutConfigJson,
            IsActive = aircraftType.IsActive
        };

        _dbContext.AircraftTypes.Add(entity);
        _dbContext.SaveChanges();

        _auditLogService.Log("AddAircraftType", "AircraftType", entity.Id, $"Добавлен тип ВС '{entity.Name}' ({entity.TotalSeats} мест)");

        return Result.Success();
    }

    /// <inheritdoc />
    public IReadOnlyList<AircraftDto> GetAllAircrafts()
    {
        return _dbContext.Aircrafts
            .AsNoTracking()
            .Include(a => a.AircraftType)
            .OrderBy(a => a.TailNumber)
            .Select(a => new AircraftDto
            {
                Id = a.Id,
                TailNumber = a.TailNumber,
                AircraftTypeId = a.AircraftTypeId,
                AircraftTypeName = a.AircraftType != null ? a.AircraftType.Name : string.Empty,
                Status = a.Status,
                IsActive = a.IsActive
            })
            .ToList();
    }

    /// <inheritdoc />
    public Result AddAircraft(AircraftDto aircraft)
    {
        if (aircraft is null)
        {
            return Result.Failure("Данные самолёта не указаны.");
        }

        if (string.IsNullOrWhiteSpace(aircraft.TailNumber))
        {
            return Result.Failure("Бортовой номер самолёта обязателен для заполнения.");
        }

        var typeExists = _dbContext.AircraftTypes.Any(t => t.Id == aircraft.AircraftTypeId);
        if (!typeExists)
        {
            return Result.Failure("Указанный тип воздушного судна не найден.");
        }

        var tailNumber = aircraft.TailNumber.Trim().ToUpperInvariant();
        var tailExists = _dbContext.Aircrafts.Any(a => a.TailNumber == tailNumber);
        if (tailExists)
        {
            return Result.Failure("Самолёт с таким бортовым номером уже существует.");
        }

        var entity = new Aircraft
        {
            TailNumber = tailNumber,
            AircraftTypeId = aircraft.AircraftTypeId,
            Status = aircraft.Status,
            IsActive = aircraft.IsActive
        };

        _dbContext.Aircrafts.Add(entity);
        _dbContext.SaveChanges();

        _auditLogService.Log("AddAircraft", "Aircraft", entity.Id, $"Добавлен самолёт '{entity.TailNumber}' (тип ID: {entity.AircraftTypeId})");

        return Result.Success();
    }

    /// <inheritdoc />
    public Result SetAircraftStatus(int aircraftId, AircraftStatus status)
    {
        var aircraft = _dbContext.Aircrafts.FirstOrDefault(a => a.Id == aircraftId);
        if (aircraft is null)
        {
            return Result.Failure("Самолёт не найден.");
        }

        aircraft.Status = status;
        _dbContext.SaveChanges();

        _auditLogService.Log("SetAircraftStatus", "Aircraft", aircraft.Id, $"Изменен статус самолета '{aircraft.TailNumber}' на '{status}'");

        return Result.Success();
    }
}
