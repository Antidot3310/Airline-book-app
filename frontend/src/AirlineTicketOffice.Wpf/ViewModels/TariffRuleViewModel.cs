using System;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Справочная карточка правила тарификации, возврата или провоза багажа W-13.
/// Правила заданы бизнес-логикой ядра (M5/M9) и отображаются в режиме только для чтения.
/// </summary>
public sealed class TariffRuleViewModel
{
    /// <summary>
    /// Инициализирует новый экземпляр справочной карточки правила.
    /// </summary>
    /// <param name="title">Краткое название правила.</param>
    /// <param name="description">Пояснение условия применения.</param>
    /// <param name="valueText">Значение или коэффициент правила.</param>
    public TariffRuleViewModel(string title, string description, string valueText)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        ValueText = valueText ?? throw new ArgumentNullException(nameof(valueText));
    }

    /// <summary>
    /// Краткое название правила.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Пояснение условия применения правила.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Значение, процент или коэффициент правила.
    /// </summary>
    public string ValueText { get; }
}
