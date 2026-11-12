using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Common;

namespace AirlineTicketOffice.Core.Contracts.Orders;

/// <summary>
/// Контракт сервиса управления заказами и электронными билетами (M7).
/// </summary>
public interface IOrderTicketService
{
    /// <summary>
    /// Создаёт заказ с одновременным временным удержанием мест для 1–5 пассажиров.
    /// </summary>
    /// <param name="request">Запрос с идентификатором рейса и данными пассажиров.</param>
    /// <returns>Результат с DTO созданного заказа <see cref="OrderDto"/>.</returns>
    Result<OrderDto> CreateOrderWithBooking(CreateOrderRequest request);

    /// <summary>
    /// Отменяет заказ и освобождает закреплённые за ним пассажирские места.
    /// </summary>
    /// <param name="orderId">Идентификатор отменяемого заказа.</param>
    /// <returns>Результат операции <see cref="Result"/>.</returns>
    Result CancelOrder(int orderId);

    /// <summary>
    /// Возвращает заказ по его уникальному числовому идентификатору.
    /// </summary>
    /// <param name="orderId">Идентификатор заказа.</param>
    /// <returns>Результат с DTO найденного заказа <see cref="OrderDto"/>.</returns>
    Result<OrderDto> GetOrderById(int orderId);

    /// <summary>
    /// Возвращает заказ по его пользовательскому номеру.
    /// </summary>
    /// <param name="orderNumber">Пользовательский номер заказа (например, "ORD-A1B2C3D4").</param>
    /// <returns>Результат с DTO найденного заказа <see cref="OrderDto"/>.</returns>
    Result<OrderDto> GetOrderByNumber(string orderNumber);

    /// <summary>
    /// Возвращает список всех билетов, оформленных на пассажира с указанным номером документа.
    /// </summary>
    /// <param name="documentNumber">Номер документа пассажира (паспорт).</param>
    /// <returns>Список DTO билетов пассажира.</returns>
    IReadOnlyList<TicketDto> GetTicketsByClientDocument(string documentNumber);

    /// <summary>
    /// Возвращает список всех заказов, созданных под ролью текущей активной сессии пользователя.
    /// </summary>
    /// <returns>Список DTO заказов текущей сессии.</returns>
    IReadOnlyList<OrderDto> GetOrdersForCurrentSession();

    /// <summary>
    /// Выполняет поиск и аннулирование всех заказов, срок удержания мест по которым истёк.
    /// Освобождает удерживаемые места и переводит билеты в статус Cancelled.
    /// </summary>
    /// <returns>Количество аннулированных просроченных заказов.</returns>
    int ExpireOverdueOrders();
}
