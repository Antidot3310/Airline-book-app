namespace AirlineTicketOffice.Wpf.Models;

/// <summary>
/// Описание пункта меню навигации главного окна приложения (W-02).
/// </summary>
/// <param name="Title">Краткое название раздела.</param>
/// <param name="Description">Пояснение назначения раздела.</param>
public sealed record NavigationMenuItem(string Title, string Description);
