namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Модель представления заглушки для разделов, реализуемых в последующих инкрементах (Инкременты 35–40).
/// </summary>
public class PlaceholderViewModel
{
    /// <summary>
    /// Название окна или раздела (например, «Поиск рейсов (W-03)»).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Подробное описание назначения раздела и доступного функционала.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Код окна по спецификации (W-03, W-07, etc.).
    /// </summary>
    public string WindowCode { get; set; } = string.Empty;

    /// <summary>
    /// Инкремент дорожной карты, в котором реализуется данный модуль.
    /// </summary>
    public string PlannedIncrement { get; set; } = string.Empty;

    /// <summary>
    /// Символьная иконка раздела.
    /// </summary>
    public string Icon { get; set; } = "✈";
}
