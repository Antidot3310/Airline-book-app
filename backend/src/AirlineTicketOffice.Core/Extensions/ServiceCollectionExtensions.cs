using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Extensions;

/// <summary>
/// Точка подключения ядра системы бронирования авиабилетов к DI-контейнеру приложения.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует контекст базы данных SQLite, сервис виртуального времени и доменные сервисы ядра.
    /// </summary>
    /// <param name="services">Коллекция сервисов DI-контейнера.</param>
    /// <param name="connectionString">Строка подключения к базе данных SQLite.</param>
    /// <returns>Коллекция сервисов для построения цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="services"/> равен null.</exception>
    public static IServiceCollection AddAirlineCore(
        this IServiceCollection services,
        string connectionString = "Data Source=airline.db")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<AirlineDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddSingleton<IVirtualClockService, VirtualClockService>();

        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}
