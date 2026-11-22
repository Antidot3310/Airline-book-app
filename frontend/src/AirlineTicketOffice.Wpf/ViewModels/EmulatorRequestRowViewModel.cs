using System;
using AirlineTicketOffice.Core.Contracts.Payments;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка журнала входящих запросов эмулятора платёжного шлюза W-16.
/// Форматирует DTO ядра <see cref="EmulatorLogEntryDto"/>: время запроса, сумму,
/// русскоязычный статус обработки и признак необходимости ручного подтверждения (TC-15).
/// Контракты ядра не изменяются.
/// </summary>
public sealed class EmulatorRequestRowViewModel
{
    private readonly EmulatorLogEntryDto _entry;

    /// <summary>
    /// Инициализирует новый экземпляр строки журнала запросов эмулятора.
    /// </summary>
    /// <param name="entry">DTO записи журнала, полученный из <c>IPaymentEmulator</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="entry"/> равен null.</exception>
    public EmulatorRequestRowViewModel(EmulatorLogEntryDto entry)
    {
        _entry = entry ?? throw new ArgumentNullException(nameof(entry));
    }

    /// <summary>
    /// Порядковый идентификатор записи в журнале эмулятора.
    /// </summary>
    public int Id => _entry.Id;

    /// <summary>
    /// Виртуальное время поступления запроса.
    /// </summary>
    public DateTime Timestamp => _entry.Timestamp;

    /// <summary>
    /// Время запроса для отображения «дд.ММ ЧЧ:мм:сс».
    /// </summary>
    public string TimestampText => _entry.Timestamp.ToString("dd.MM HH:mm:ss");

    /// <summary>
    /// Идентификатор оплачиваемого заказа.
    /// </summary>
    public int OrderId => _entry.OrderId;

    /// <summary>
    /// Подпись заказа «Заказ #42».
    /// </summary>
    public string OrderText => $"Заказ #{_entry.OrderId}";

    /// <summary>
    /// Сумма платёжного запроса.
    /// </summary>
    public decimal Amount => _entry.Amount;

    /// <summary>
    /// Сумма запроса для отображения «8 000 ₽».
    /// </summary>
    public string AmountText => $"{_entry.Amount:N0} ₽";

    /// <summary>
    /// Маскированный номер карты.
    /// </summary>
    public string MaskedCardNumber =>
        string.IsNullOrWhiteSpace(_entry.MaskedCardNumber) ? "—" : _entry.MaskedCardNumber;

    /// <summary>
    /// Итоговый статус обработки транзакции из доменной модели.
    /// </summary>
    public PaymentStatus Status => _entry.Status;

    /// <summary>
    /// Русскоязычная подпись статуса обработки транзакции.
    /// </summary>
    public string StatusText => GetStatusText(_entry.Status);

    /// <summary>
    /// Внешний идентификатор транзакции или «—», если платёж не был проведён.
    /// </summary>
    public string ExternalTransactionId =>
        string.IsNullOrWhiteSpace(_entry.ExternalTransactionId) ? "—" : _entry.ExternalTransactionId;

    /// <summary>
    /// Текстовое сообщение с результатом обработки запроса.
    /// </summary>
    public string Message =>
        string.IsNullOrWhiteSpace(_entry.Message) ? "—" : _entry.Message;

    /// <summary>
    /// Признак платежа, зависшего в неопределённом статусе и требующего ручного подтверждения (TC-15).
    /// </summary>
    public bool RequiresManualResolve => _entry.Status == PaymentStatus.Indeterminate;

    /// <summary>
    /// Возвращает русскоязычную подпись статуса платежа.
    /// </summary>
    /// <param name="status">Статус платежа из доменной модели.</param>
    /// <returns>Русскоязычное наименование статуса.</returns>
    public static string GetStatusText(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "Ожидает обработки",
        PaymentStatus.Success => "Успешно",
        PaymentStatus.Failed => "Отклонён",
        PaymentStatus.Indeterminate => "Требует подтверждения",
        PaymentStatus.Refunded => "Возвращён",
        _ => status.ToString()
    };
}
