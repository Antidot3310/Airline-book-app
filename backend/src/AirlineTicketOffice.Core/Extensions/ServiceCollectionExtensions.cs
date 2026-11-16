using System;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Contracts.Booking;
using AirlineTicketOffice.Core.Contracts.Clients;
using AirlineTicketOffice.Core.Contracts.Clock;
using AirlineTicketOffice.Core.Contracts.Directory;
using AirlineTicketOffice.Core.Contracts.Documents;
using AirlineTicketOffice.Core.Contracts.Flights;
using AirlineTicketOffice.Core.Contracts.Orders;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Contracts.Refunds;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Contracts.Session;
using AirlineTicketOffice.Core.Contracts.Tariffs;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AirlineTicketOffice.Core.Extensions;

/// <summary>
/// Методы расширения для регистрации сервисов ядра «Авиакасса» в DI-контейнере (M1–M15).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует все доменные сервисы, эмуляторы и контекст БД ядра «Авиакасса» в коллекции сервисов.
    /// </summary>
    /// <param name="services">Коллекция дескрипторов служб DI.</param>
    /// <param name="connectionString">Строка подключения к базе данных SQLite (по умолчанию "Data Source=airline.db").</param>
    /// <returns>Коллекция сервисов для выстраивания цепочки вызовов.</returns>
    public static IServiceCollection AddAirlineCore(
        this IServiceCollection services,
        string connectionString = "Data Source=airline.db")
    {
        return services.AddAirlineCore(options => options.UseSqlite(connectionString));
    }

    /// <summary>
    /// Регистрирует все доменные сервисы, эмуляторы и контекст БД ядра «Авиакасса» с настраиваемой конфигурацией DbContext.
    /// </summary>
    /// <param name="services">Коллекция дескрипторов служб DI.</param>
    /// <param name="configureDbContext">Делегат настройки опций контекста базы данных <see cref="AirlineDbContext"/>.</param>
    /// <returns>Коллекция сервисов для выстраивания цепочки вызовов.</returns>
    public static IServiceCollection AddAirlineCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        // 1) БД (M13)
        services.AddDbContext<AirlineDbContext>(configureDbContext);

        // 2) Виртуальное время (M12)
        services.AddSingleton<IVirtualClockService, VirtualClockService>();

        // 3) Сессии и аудит (M1, M11)
        services.AddSingleton<ISessionService>(sp =>
            new SessionService(sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddScoped<IAuditLogService, AuditLogService>();

        // 4) Справочники и клиенты (M2, M3)
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IDirectoryService, DirectoryService>();

        // 5) Рейсы и тарифы (M4, M5)
        services.AddScoped<IFlightService, FlightService>();
        services.AddScoped<ITariffCalculator, TariffCalculator>();

        // 6) Бронирование и заказы (M6, M7)
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IOrderTicketService, OrderTicketService>();

        // 7) Платежи и эмулятор (M8, M14)
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddSingleton<IPaymentEmulator>(sp =>
            new PaymentEmulator(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<IVirtualClockService>()));

        // 8) Возвраты, отчёты и печать (M9, M10, M15)
        services.AddScoped<IRefundService, RefundService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDocumentService, DocumentService>();

        return services;
    }

    /// <summary>
    /// Выполняет начальную инициализацию базы данных: создание схемы и сидирование справочников, рейсов и тарифов.
    /// </summary>
    /// <param name="serviceProvider">Провайдер служб DI.</param>
    /// <returns>Провайдер служб для выстраивания цепочки вызовов.</returns>
    public static IServiceProvider InitializeAirlineDatabase(this IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        using IServiceScope scope = serviceProvider.CreateScope();
        AirlineDbContext dbContext = scope.ServiceProvider.GetRequiredService<AirlineDbContext>();
        dbContext.Database.EnsureCreated();
        DbInitializer.Initialize(dbContext);

        return serviceProvider;
    }
}
