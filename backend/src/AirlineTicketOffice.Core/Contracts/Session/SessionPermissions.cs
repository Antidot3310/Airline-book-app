namespace AirlineTicketOffice.Core.Contracts.Session;

/// <summary>
/// Набор строковых констант для проверки прав доступа пользователей в системе.
/// </summary>
public static class SessionPermissions
{
    /// <summary>
    /// Право на управление справочниками (аэропорты, типы воздушных судов, самолёты).
    /// </summary>
    public const string ManageDirectories = "ManageDirectories";

    /// <summary>
    /// Право на управление рейсами (создание, отмена рейсов, замена воздушного судна).
    /// </summary>
    public const string ManageFlights = "ManageFlights";

    /// <summary>
    /// Право на управление тарифами (изменение базовых цен направлений, коэффициентов и сборов).
    /// </summary>
    public const string ManageTariffs = "ManageTariffs";

    /// <summary>
    /// Право на просмотр аналитических отчётов (загрузка рейсов, выручка, статистика).
    /// </summary>
    public const string ViewReports = "ViewReports";

    /// <summary>
    /// Право на просмотр журнала аудита системных операций.
    /// </summary>
    public const string ViewAuditLogs = "ViewAuditLogs";

    /// <summary>
    /// Право на поиск рейсов и оформление бронирования мест.
    /// </summary>
    public const string CreateBooking = "CreateBooking";

    /// <summary>
    /// Право на приём наличных средств при оплате заказа в кассе.
    /// </summary>
    public const string AcceptCashPayment = "AcceptCashPayment";

    /// <summary>
    /// Право на приём оплаты заказа банковской картой.
    /// </summary>
    public const string AcceptCardPayment = "AcceptCardPayment";

    /// <summary>
    /// Право на проведение возврата билета.
    /// </summary>
    public const string RefundTicket = "RefundTicket";

    /// <summary>
    /// Право на управление карточками клиентов (поиск, создание пассажиров в кассе).
    /// </summary>
    public const string ManageClients = "ManageClients";
}
