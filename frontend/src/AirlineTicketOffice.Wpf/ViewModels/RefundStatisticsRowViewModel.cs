using System;
using AirlineTicketOffice.Core.Contracts.Reports;
using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Wpf.ViewModels;

/// <summary>
/// Презентационная строка отчёта по статистике возвратов W-14.
/// Форматирует DTO ядра <see cref="RefundStatisticsItemDto"/>: русскоязычное основание
/// возврата, суммы выплат и удержанных штрафов. Контракты ядра не изменяются.
/// </summary>
public sealed class RefundStatisticsRowViewModel
{
    private readonly RefundStatisticsItemDto _item;

    /// <summary>
    /// Инициализирует новый экземпляр строки отчёта по статистике возвратов.
    /// </summary>
    /// <param name="item">DTO строки отчёта, полученный из <c>IReportService</c>.</param>
    /// <exception cref="ArgumentNullException">Выбрасывается, если <paramref name="item"/> равен null.</exception>
    public RefundStatisticsRowViewModel(RefundStatisticsItemDto item)
    {
        _item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    /// Причина оформления возврата из доменной модели.
    /// </summary>
    public RefundReason Reason => _item.Reason;

    /// <summary>
    /// Русскоязычное наименование причины возврата.
    /// </summary>
    public string ReasonName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_item.ReasonName))
            {
                return _item.ReasonName;
            }

            return _item.Reason == RefundReason.Voluntary
                ? "Добровольный возврат"
                : "Отмена рейса";
        }
    }

    /// <summary>
    /// Символьная иконка основания возврата для таблицы.
    /// </summary>
    public string ReasonIcon => _item.Reason == RefundReason.Voluntary ? "🙋" : "✈";

    /// <summary>
    /// Количество операций возврата по данной причине.
    /// </summary>
    public int RefundsCount => _item.RefundsCount;

    /// <summary>
    /// Общая сумма возвращённых средств по причине.
    /// </summary>
    public decimal TotalRefundAmount => _item.TotalRefundAmount;

    /// <summary>
    /// Сумма выплат клиентам для отображения «180 000 ₽».
    /// </summary>
    public string TotalRefundAmountText => $"{_item.TotalRefundAmount:N0} ₽";

    /// <summary>
    /// Общая сумма удержанных штрафов по причине.
    /// </summary>
    public decimal TotalPenaltyAmount => _item.TotalPenaltyAmount;

    /// <summary>
    /// Сумма удержанных штрафов для отображения «12 000 ₽».
    /// </summary>
    public string TotalPenaltyAmountText => $"{_item.TotalPenaltyAmount:N0} ₽";

    /// <summary>
    /// Доля удержанных штрафов в общей сумме возврата по причине.
    /// </summary>
    public double PenaltySharePercent
    {
        get
        {
            decimal total = _item.TotalRefundAmount + _item.TotalPenaltyAmount;
            return total > 0m ? (double)(_item.TotalPenaltyAmount / total) * 100d : 0d;
        }
    }

    /// <summary>
    /// Подпись доли штрафов «12,5 %».
    /// </summary>
    public string PenaltyShareText => $"{PenaltySharePercent:F1} %";
}
