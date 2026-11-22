using System;
using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Audit;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка журнала аудита W-15.
/// Выполняет исключительно форматирование DTO ядра <see cref="AuditLogDto"/>:
/// русскоязычные подписи ролей и типов действий, объединённая подпись сущности,
/// а также поиск по ключевому слову в описании события.
/// Контракты <c>AirlineTicketOffice.Core.Contracts.*</c> при этом не изменяются.
/// </summary>
public sealed class AuditLogRowViewModel
{
    private static readonly IReadOnlyDictionary<string, string> ActionTitles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OpenSession"] = "Открытие сеанса",
            ["CloseSession"] = "Закрытие сеанса",
            ["CreateClient"] = "Создание клиента",
            ["UpdateClient"] = "Изменение клиента",
            ["AddAirport"] = "Добавление аэропорта",
            ["DeactivateAirport"] = "Деактивация аэропорта",
            ["AddAircraftType"] = "Добавление типа ВС",
            ["AddAircraft"] = "Добавление воздушного судна",
            ["SetAircraftStatus"] = "Изменение статуса ВС",
            ["CreateFlight"] = "Создание рейса",
            ["ChangeAircraft"] = "Замена воздушного судна",
            ["CancelFlight"] = "Отмена рейса",
            ["FlightDeparted"] = "Вылет рейса",
            ["CreateOrder"] = "Создание заказа",
            ["CancelOrder"] = "Отмена бронирования",
            ["ExpireOrders"] = "Аннулирование просроченных заказов",
            ["ExpireHold"] = "Снятие просроченной брони",
            ["PaymentSuccess"] = "Успешная оплата",
            ["PaymentFailed"] = "Отказ в оплате",
            ["PaymentIndeterminate"] = "Неопределённый статус платежа",
            ["ManualPaymentResolve"] = "Ручное подтверждение платежа",
            ["ProcessRefund"] = "Возврат билета",
            ["MassRefund"] = "Массовый возврат при отмене рейса",
            ["SetBaseTariff"] = "Изменение базового тарифа"
        };

    private readonly AuditLogDto _log;

    /// <summary>
    /// Инициализирует новый экземпляр строки журнала аудита.
    /// </summary>
    /// <param name="log">DTO записи аудита, полученный из <c>IAuditLogService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="log"/> равен null.</exception>
    public AuditLogRowViewModel(AuditLogDto log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Уникальный идентификатор записи журнала аудита.
    /// </summary>
    public int Id => _log.Id;

    /// <summary>
    /// Виртуальное системное время события.
    /// </summary>
    public DateTime VirtualTime => _log.VirtualTime;

    /// <summary>
    /// Виртуальное время для отображения «дд.ММ.гггг ЧЧ:мм:сс».
    /// </summary>
    public string VirtualTimeText => _log.VirtualTime.ToString("dd.MM.yyyy HH:mm:ss");

    /// <summary>
    /// Реальное физическое время фиксации события.
    /// </summary>
    public DateTime RealTime => _log.RealTime;

    /// <summary>
    /// Реальное время для отображения «дд.ММ.гггг ЧЧ:мм:сс».
    /// </summary>
    public string RealTimeText => _log.RealTime.ToString("dd.MM.yyyy HH:mm:ss");

    /// <summary>
    /// Роль инициатора операции из доменной модели (используется для цветовых меток).
    /// </summary>
    public UserRole Role => _log.Role;

    /// <summary>
    /// Русскоязычная подпись роли инициатора операции.
    /// </summary>
    public string RoleText => GetRoleText(_log.Role);

    /// <summary>
    /// Исходный код типа действия из ядра (например, CreateBooking, CancelFlight).
    /// </summary>
    public string ActionCode =>
        string.IsNullOrWhiteSpace(_log.ActionType) ? "—" : _log.ActionType;

    /// <summary>
    /// Русскоязычная подпись типа действия; для неизвестных кодов отображается исходный код.
    /// </summary>
    public string ActionText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_log.ActionType))
            {
                return "—";
            }

            return ActionTitles.TryGetValue(_log.ActionType, out string? title)
                ? title
                : _log.ActionType;
        }
    }

    /// <summary>
    /// Исходный код действия для дополнительной строки таблицы.
    /// Пустая строка, если русскоязычная подпись совпадает с кодом.
    /// </summary>
    public string ActionCodeText =>
        string.Equals(ActionCode, ActionText, StringComparison.Ordinal) ? string.Empty : ActionCode;

    /// <summary>
    /// Наименование затронутой сущности (например, Order, Flight, Ticket).
    /// </summary>
    public string EntityName =>
        string.IsNullOrWhiteSpace(_log.EntityName) ? "—" : _log.EntityName;

    /// <summary>
    /// Идентификатор затронутой сущности.
    /// </summary>
    public int? EntityId => _log.EntityId;

    /// <summary>
    /// Объединённая подпись сущности «Order #42».
    /// </summary>
    public string EntityText => _log.EntityId.HasValue
        ? $"{EntityName} #{_log.EntityId.Value}"
        : EntityName;

    /// <summary>
    /// Подробное текстовое описание события аудита.
    /// </summary>
    public string Description =>
        string.IsNullOrWhiteSpace(_log.Description) ? "—" : _log.Description;

    /// <summary>
    /// Признак события, инициированного системным процессом.
    /// </summary>
    public bool IsSystemAction => _log.Role == UserRole.System;

    /// <summary>
    /// Проверяет, содержит ли запись заданное ключевое слово в типе действия,
    /// наименовании сущности, роли или описании события.
    /// </summary>
    /// <param name="keyword">Ключевое слово для поиска без учёта регистра.</param>
    /// <returns><c>true</c>, если запись соответствует поисковому запросу.</returns>
    public bool MatchesKeyword(string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        string needle = keyword.Trim();
        return ContainsText(ActionCode, needle)
            || ContainsText(ActionText, needle)
            || ContainsText(EntityText, needle)
            || ContainsText(RoleText, needle)
            || ContainsText(Description, needle);
    }

    /// <summary>
    /// Возвращает русскоязычную подпись роли пользователя или системного процесса.
    /// </summary>
    /// <param name="role">Роль инициатора операции.</param>
    /// <returns>Русскоязычное наименование роли.</returns>
    public static string GetRoleText(UserRole role) => role switch
    {
        UserRole.Passenger => "Пассажир",
        UserRole.Cashier => "Кассир",
        UserRole.Administrator => "Администратор",
        UserRole.System => "Системный процесс",
        _ => role.ToString()
    };

    private static bool ContainsText(string? source, string needle)
    {
        return !string.IsNullOrEmpty(source)
            && source.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
