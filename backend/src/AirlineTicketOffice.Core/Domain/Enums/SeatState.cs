namespace AirlineTicketOffice.Core.Domain.Enums;

/// <summary>
/// Состояние пассажирского места на рейсе.
/// </summary>
public enum SeatState
{
    /// <summary>
    /// Свободно для выбора и покупки.
    /// </summary>
    Available,

    /// <summary>
    /// Удерживается (находится в активной брони).
    /// </summary>
    Held,

    /// <summary>
    /// Продано (оплачено).
    /// </summary>
    Sold,

    /// <summary>
    /// Заблокировано для продажи администратором или по служебным причинам.
    /// </summary>
    Blocked
}
