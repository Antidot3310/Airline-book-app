using AirlineTicketOffice.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Data;

/// <summary>
/// Контекст базы данных SQLite системы бронирования авиабилетов (EF Core).
/// </summary>
public class AirlineDbContext : DbContext
{
    /// <summary>
    /// Инициализирует новый экземпляр контекста базы данных с заданными параметрами.
    /// </summary>
    /// <param name="options">Параметры конфигурации подключения к базе данных.</param>
    public AirlineDbContext(DbContextOptions<AirlineDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Набор данных аэропортов (справочник M3).
    /// </summary>
    public DbSet<Airport> Airports => Set<Airport>();

    /// <summary>
    /// Набор данных типов воздушных судов (справочник M3).
    /// </summary>
    public DbSet<AircraftType> AircraftTypes => Set<AircraftType>();

    /// <summary>
    /// Набор данных воздушных судов / бортов (справочник M3).
    /// </summary>
    public DbSet<Aircraft> Aircrafts => Set<Aircraft>();

    /// <summary>
    /// Набор данных рейсов расписания (M4).
    /// </summary>
    public DbSet<Flight> Flights => Set<Flight>();

    /// <summary>
    /// Набор данных мест на рейсах (M6).
    /// </summary>
    public DbSet<FlightSeat> FlightSeats => Set<FlightSeat>();

    /// <summary>
    /// Набор данных маршрутных тарифов (M5).
    /// </summary>
    public DbSet<Tariff> Tariffs => Set<Tariff>();

    /// <summary>
    /// Набор данных пассажиров / клиентов (M2).
    /// </summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>
    /// Набор данных заказов (M7).
    /// </summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>
    /// Набор данных электронных билетов (M7).
    /// </summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>
    /// Набор данных платежей по заказам (M8).
    /// </summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <summary>
    /// Набор данных возвратов билетов (M9).
    /// </summary>
    public DbSet<Refund> Refunds => Set<Refund>();

    /// <summary>
    /// Набор данных журнала аудита операций (M11).
    /// </summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Набор данных состояния виртуальных системных часов (M12).
    /// </summary>
    public DbSet<SystemTimeState> SystemTimeStates => Set<SystemTimeState>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Запрет каскадного удаления для всех внешних ключей (DeleteBehavior.Restrict)
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // 2. Настройка точности для всех полей decimal (18, 2)
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(2);
        }

        // 3. Конфигурация сущности Airport
        modelBuilder.Entity<Airport>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.Code).IsUnique();
            b.Property(a => a.Code).HasMaxLength(3).IsRequired();
            b.Property(a => a.City).HasMaxLength(100).IsRequired();
            b.Property(a => a.Name).HasMaxLength(150).IsRequired();
        });

        // 4. Конфигурация сущности AircraftType
        modelBuilder.Entity<AircraftType>(b =>
        {
            b.HasKey(at => at.Id);
            b.Property(at => at.Name).HasMaxLength(100).IsRequired();
        });

        // 5. Конфигурация сущности Aircraft
        modelBuilder.Entity<Aircraft>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.TailNumber).IsUnique();
            b.Property(a => a.TailNumber).HasMaxLength(20).IsRequired();
            b.HasOne(a => a.AircraftType)
                .WithMany(at => at.Aircrafts)
                .HasForeignKey(a => a.AircraftTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. Конфигурация сущности Flight
        modelBuilder.Entity<Flight>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.FlightNumber).HasMaxLength(20).IsRequired();
            b.HasOne(f => f.OriginAirport)
                .WithMany()
                .HasForeignKey(f => f.OriginAirportId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(f => f.DestinationAirport)
                .WithMany()
                .HasForeignKey(f => f.DestinationAirportId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(f => f.Aircraft)
                .WithMany()
                .HasForeignKey(f => f.AircraftId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 7. Конфигурация сущности FlightSeat
        modelBuilder.Entity<FlightSeat>(b =>
        {
            b.HasKey(fs => fs.Id);
            b.HasIndex(fs => new { fs.FlightId, fs.SeatNumber }).IsUnique();
            b.Property(fs => fs.SeatNumber).HasMaxLength(10).IsRequired();
            b.Property(fs => fs.RowVersion)
                .IsRowVersion()
                .HasDefaultValueSql("randomblob(8)");
            b.HasOne(fs => fs.Flight)
                .WithMany(f => f.Seats)
                .HasForeignKey(fs => fs.FlightId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. Конфигурация сущности Tariff
        modelBuilder.Entity<Tariff>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.OriginAirportId, t.DestinationAirportId }).IsUnique();
            b.HasOne(t => t.OriginAirport)
                .WithMany()
                .HasForeignKey(t => t.OriginAirportId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.DestinationAirport)
                .WithMany()
                .HasForeignKey(t => t.DestinationAirportId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 9. Конфигурация сущности Client
        modelBuilder.Entity<Client>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.FullName).HasMaxLength(150).IsRequired();
            b.Property(c => c.DocumentNumber).HasMaxLength(50).IsRequired();
            b.Property(c => c.ContactPhone).HasMaxLength(30);
            b.Property(c => c.ContactEmail).HasMaxLength(100);
        });

        // 10. Конфигурация сущности Order
        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.HasIndex(o => o.OrderNumber).IsUnique();
            b.Property(o => o.OrderNumber).HasMaxLength(30).IsRequired();
        });

        // 11. Конфигурация сущности Ticket
        modelBuilder.Entity<Ticket>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => t.TicketNumber).IsUnique();
            b.Property(t => t.TicketNumber).HasMaxLength(30).IsRequired();
            b.HasOne(t => t.Order)
                .WithMany(o => o.Tickets)
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.Client)
                .WithMany(c => c.Tickets)
                .HasForeignKey(t => t.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.Flight)
                .WithMany(f => f.Tickets)
                .HasForeignKey(t => t.FlightId)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(t => t.FlightSeat)
                .WithMany()
                .HasForeignKey(t => t.FlightSeatId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 12. Конфигурация сущности Payment
        modelBuilder.Entity<Payment>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.ExternalTransactionId).HasMaxLength(100);
            b.HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 13. Конфигурация сущности Refund
        modelBuilder.Entity<Refund>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasOne(r => r.Ticket)
                .WithOne(t => t.Refund)
                .HasForeignKey<Refund>(r => r.TicketId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // 14. Конфигурация сущности AuditLog
        modelBuilder.Entity<AuditLog>(b =>
        {
            b.HasKey(a => a.Id);
            b.Property(a => a.ActionType).HasMaxLength(50).IsRequired();
            b.Property(a => a.EntityName).HasMaxLength(50);
        });

        // 15. Конфигурация сущности SystemTimeState
        modelBuilder.Entity<SystemTimeState>(b =>
        {
            b.HasKey(s => s.Id);
        });
    }
}
