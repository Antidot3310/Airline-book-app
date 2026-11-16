using AirlineTicketOffice.Core.Domain.Enums;

namespace AirlineTicketOffice.Core.Contracts.Reports;

/// <summary>
/// DTO аналитического отчёта по статистике возвратов билетов за выбранный период.
/// </summary>
public class RefundStatisticsReportDto
{
    /// <summary>
    /// Начальная дата периода отчёта.
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// Конечная дата периода отчёта.
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Общее количество выполненных возвратов.
    /// </summary>
    public int TotalRefundsCount { get; set; }

    /// <summary>
    /// Общая выплаченная сумма возвратов клиентам в рублях.
    /// </summary>
    public decimal TotalRefundAmount { get; set; }

    /// <summary>
    /// Общая удержанная сумма штрафов авиакомпании в рублях.
    /// </summary>
    public decimal TotalPenaltyAmount { get; set; }

    /// <summary>
    /// Детализированные строки статистики в разрезе причин возврата.
    /// </summary>
    public IReadOnlyList<RefundStatisticsItemDto> Items { get; set; } = [];
}

/// <summary>
/// Элемент отчёта по статистике возвратов конкретной причины.
/// </summary>
public class RefundStatisticsItemDto
{
    /// <summary>
    /// Причина оформления возврата (добровольный возврат или отмена рейса).
    /// </summary>
    public RefundReason Reason { get; set; }

    /// <summary>
    /// Человекочитаемое наименование причины возврата ("Добровольный возврат" или "Отмена рейса").
    /// </summary>
    public string ReasonName { get; set; } = string.Empty;

    /// <summary>
    /// Количество операций возврата по данной причине.
    /// </summary>
    public int RefundsCount { get; set; }

    /// <summary>
    /// Общая сумма возвращённых средств по данной причине в рублях.
    /// </summary>
    public decimal TotalRefundAmount { get; set; }

    /// <summary>
    /// Общая сумма удержанных штрафов по данной причине в рублях.
    /// </summary>
    public decimal TotalPenaltyAmount { get; set; }
}
