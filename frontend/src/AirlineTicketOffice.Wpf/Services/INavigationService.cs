using AirlineTicketOffice.Core.Contracts.Documents;

namespace AirlineTicketOffice.Wpf.Services;

/// <summary>
/// Интерфейс сервиса навигации между окнами и представлениями WPF-приложения.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Открывает главное окно приложения (W-02) и закрывает окно выбора роли (W-01).
    /// </summary>
    void OpenMainWindow();

    /// <summary>
    /// Возвращает пользователя к стартовому окну выбора роли (W-01), закрывая главное окно.
    /// </summary>
    void OpenRoleSelectionWindow();

    /// <summary>
    /// Открывает служебное окно эмулятора (W-16).
    /// </summary>
    void OpenEmulatorWindow();

    /// <summary>
    /// Открывает модальное окно предпросмотра и печати документов W-10.
    /// </summary>
    /// <param name="document">Сформированный печатный документ.</param>
    void OpenDocumentPreview(PrintDocumentDto document);
}
