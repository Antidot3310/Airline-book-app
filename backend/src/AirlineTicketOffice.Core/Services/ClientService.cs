using System;
using System.Collections.Generic;
using System.Linq;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clients;
using AirlineTicketOffice.Core.Contracts.Common;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Реализация сервиса управления карточками клиентов и пассажиров (M2).
/// Обеспечивает синхронные операции поиска, создания и обновления пассажиров с фиксацией в аудите.
/// </summary>
public class ClientService : IClientService
{
    private readonly AirlineDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    /// <summary>
    /// Инициализирует новый экземпляр сервиса <see cref="ClientService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <param name="auditLogService">Сервис журнала аудита операций.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если один из параметров равен null.</exception>
    public ClientService(AirlineDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <inheritdoc />
    public Result<ClientDto> FindByDocument(string documentNumber)
    {
        if (string.IsNullOrWhiteSpace(documentNumber))
        {
            return Result<ClientDto>.Failure("Номер документа не указан.");
        }

        var trimmedDoc = documentNumber.Trim();
        var client = _dbContext.Clients
            .AsNoTracking()
            .FirstOrDefault(c => c.DocumentNumber == trimmedDoc);

        if (client is null)
        {
            return Result<ClientDto>.Failure("Пассажир с указанным номером документа не найден.");
        }

        var dto = new ClientDto
        {
            Id = client.Id,
            FullName = client.FullName,
            DocumentNumber = client.DocumentNumber,
            ContactPhone = client.ContactPhone,
            ContactEmail = client.ContactEmail
        };

        return Result<ClientDto>.Success(dto);
    }

    /// <inheritdoc />
    public IReadOnlyList<ClientDto> SearchClients(string searchPattern)
    {
        var query = _dbContext.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchPattern))
        {
            var pattern = searchPattern.Trim();
            query = query.Where(c => c.FullName.Contains(pattern) || c.DocumentNumber.Contains(pattern));
        }

        return query
            .OrderBy(c => c.FullName)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                FullName = c.FullName,
                DocumentNumber = c.DocumentNumber,
                ContactPhone = c.ContactPhone,
                ContactEmail = c.ContactEmail
            })
            .ToList();
    }

    /// <inheritdoc />
    public Result<ClientDto> CreateOrUpdateClient(ClientDto client)
    {
        if (client is null)
        {
            return Result<ClientDto>.Failure("Данные клиента не указаны.");
        }

        if (string.IsNullOrWhiteSpace(client.FullName))
        {
            return Result<ClientDto>.Failure("ФИО пассажира обязательно для заполнения.");
        }

        if (string.IsNullOrWhiteSpace(client.DocumentNumber))
        {
            return Result<ClientDto>.Failure("Номер документа обязателен для заполнения.");
        }

        var trimmedFullName = client.FullName.Trim();
        var trimmedDocNumber = client.DocumentNumber.Trim();
        var trimmedPhone = string.IsNullOrWhiteSpace(client.ContactPhone) ? null : client.ContactPhone.Trim();
        var trimmedEmail = string.IsNullOrWhiteSpace(client.ContactEmail) ? null : client.ContactEmail.Trim();

        if (client.Id == 0)
        {
            var exists = _dbContext.Clients.Any(c => c.DocumentNumber == trimmedDocNumber);
            if (exists)
            {
                return Result<ClientDto>.Failure("Пассажир с таким номером документа уже зарегистрирован.");
            }

            var entity = new Client
            {
                FullName = trimmedFullName,
                DocumentNumber = trimmedDocNumber,
                ContactPhone = trimmedPhone,
                ContactEmail = trimmedEmail
            };

            _dbContext.Clients.Add(entity);
            _dbContext.SaveChanges();

            _auditLogService.Log("CreateClient", "Client", entity.Id, $"Создан пассажир '{entity.FullName}' (документ: {entity.DocumentNumber})");

            var resultDto = new ClientDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                DocumentNumber = entity.DocumentNumber,
                ContactPhone = entity.ContactPhone,
                ContactEmail = entity.ContactEmail
            };

            return Result<ClientDto>.Success(resultDto);
        }
        else
        {
            var entity = _dbContext.Clients.FirstOrDefault(c => c.Id == client.Id);
            if (entity is null)
            {
                return Result<ClientDto>.Failure("Пассажир не найден.");
            }

            var duplicate = _dbContext.Clients.Any(c => c.DocumentNumber == trimmedDocNumber && c.Id != client.Id);
            if (duplicate)
            {
                return Result<ClientDto>.Failure("Номер документа уже используется другим пассажиром.");
            }

            entity.FullName = trimmedFullName;
            entity.DocumentNumber = trimmedDocNumber;
            entity.ContactPhone = trimmedPhone;
            entity.ContactEmail = trimmedEmail;

            _dbContext.SaveChanges();

            _auditLogService.Log("UpdateClient", "Client", entity.Id, $"Обновлены данные пассажира '{entity.FullName}' (ID: {entity.Id})");

            var resultDto = new ClientDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                DocumentNumber = entity.DocumentNumber,
                ContactPhone = entity.ContactPhone,
                ContactEmail = entity.ContactEmail
            };

            return Result<ClientDto>.Success(resultDto);
        }
    }
}
