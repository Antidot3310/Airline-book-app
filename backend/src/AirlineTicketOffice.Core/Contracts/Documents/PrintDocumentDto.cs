using System;

namespace AirlineTicketOffice.Core.Contracts.Documents;

/// <summary>
/// Модель сформированного документа для отображения в окне предпросмотра (W-10) и печати.
/// </summary>
public class PrintDocumentDto
{
    /// <summary>
    /// Заголовок документа (например, "Маршрутная квитанция электронного билета", "Кассовый чек").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Уникальный номер документа (номер билета, чека, квитанции возврата или отчёта).
    /// </summary>
    public string DocumentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Дата и время формирования документа.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Отформатированный текстовый макет документа с псевдографикой (рамки, выровненные колонки, итоги).
    /// </summary>
    public string FormattedContent { get; set; } = string.Empty;
}
