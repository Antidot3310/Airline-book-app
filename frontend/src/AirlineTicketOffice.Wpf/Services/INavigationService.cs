namespace AirlineTicketOffice.Wpf.Services;

/// <summary>
/// Сервис навигации между окнами приложения.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Открывает окно выбора роли (W-01) и закрывает текущее окно.
    /// </summary>
    void NavigateToRoleSelection();

    /// <summary>
    /// Открывает главное окно приложения (W-02) для активной роли и закрывает текущее окно.
    /// </summary>
    void NavigateToMainShell();
}
