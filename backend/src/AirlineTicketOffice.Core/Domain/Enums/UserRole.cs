namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Роли пользователей в системе бронирования авиабилетов.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Пассажир (пользователь интерфейса самообслуживания).
    /// </summary>
    Passenger,

    /// <summary>
    /// Кассир (оператор кассы продажи и возврата билетов).
    /// </summary>
    Cashier,

    /// <summary>
    /// Администратор (управление расписанием, флотом, тарифами и пользователями).
    /// </summary>
    Administrator,

    /// <summary>
    /// Системный процесс или фоновая служба (автоматические операции).
    /// </summary>
    System
}
