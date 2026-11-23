using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Вариант фильтра журнала аудита W-15 по роли инициатора операции.
/// Русскоязычная подпись формируется во ViewModel, а не в XAML-представлении.
/// </summary>
public sealed class AuditRoleFilterOption
{
    /// <summary>
    /// Инициализирует новый экземпляр варианта фильтра по роли.
    /// </summary>
    /// <param name="title">Русскоязычная подпись фильтра.</param>
    /// <param name="role">Роль инициатора; <c>null</c> означает «все роли».</param>
    public AuditRoleFilterOption(string title, UserRole? role)
    {
        Title = title;
        Role = role;
    }

    /// <summary>
    /// Русскоязычная подпись фильтра.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Роль инициатора операции; <c>null</c> — без фильтра по роли.
    /// </summary>
    public UserRole? Role { get; }
}
