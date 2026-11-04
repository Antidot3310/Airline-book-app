using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Data.Entities;
using AirlineTicketOffice.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Services;

/// <summary>
/// Сервис журнала аудита операций (M11).
/// Реализует фиксацию системных действий и запрос записей аудита с фильтрацией.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly AirlineDbContext _dbContext;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="AuditLogService"/>.
    /// </summary>
    /// <param name="dbContext">Контекст базы данных системы.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="dbContext"/> равен null.</exception>
    public AuditLogService(AirlineDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public void Log(string actionType, string entityName, int? entityId, string description)
    {
        Log(UserRole.System, actionType, entityName, entityId, description);
    }

    /// <inheritdoc />
    public void Log(UserRole role, string actionType, string entityName, int? entityId, string description)
    {
        var virtualTime = _dbContext.SystemTimeStates
            .Select(s => (DateTime?)s.CurrentVirtualTime)
            .FirstOrDefault() ?? DateTime.Now;

        var auditLog = new AuditLog
        {
            VirtualTime = virtualTime,
            RealTime = DateTime.Now,
            Role = role,
            ActionType = actionType ?? string.Empty,
            EntityName = entityName,
            EntityId = entityId,
            Description = description ?? string.Empty
        };

        _dbContext.AuditLogs.Add(auditLog);
        _dbContext.SaveChanges();
    }

    /// <inheritdoc />
    public IReadOnlyList<AuditLogDto> GetLogs(DateTime? fromDate, DateTime? toDate, UserRole? roleFilter, string? actionFilter)
    {
        var query = _dbContext.AuditLogs.AsNoTracking();

        if (fromDate.HasValue)
        {
            query = query.Where(l => l.VirtualTime >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(l => l.VirtualTime <= toDate.Value);
        }

        if (roleFilter.HasValue)
        {
            query = query.Where(l => l.Role == roleFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(l => l.ActionType.Contains(actionFilter));
        }

        return query
            .OrderByDescending(l => l.VirtualTime)
            .ThenByDescending(l => l.Id)
            .Select(l => new AuditLogDto
            {
                Id = l.Id,
                VirtualTime = l.VirtualTime,
                RealTime = l.RealTime,
                Role = l.Role,
                ActionType = l.ActionType,
                EntityName = l.EntityName,
                EntityId = l.EntityId,
                Description = l.Description
            })
            .ToList();
    }
}
