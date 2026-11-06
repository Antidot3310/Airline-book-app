using System.Collections.Generic;
using AirlineTicketOffice.Core.Contracts.Common;

namespace AirlineTicketOffice.Core.Contracts.Clients;

/// <summary>
/// Интерфейс сервиса управления клиентами и пассажирами (M2).
/// Обеспечивает поиск пассажиров по документам, строке поиска и регистрацию/обновление карточек.
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Выполняет точный поиск пассажира по номеру документа, удостоверяющего личность.
    /// </summary>
    /// <param name="documentNumber">Номер документа (паспорта).</param>
    /// <returns>Результат с найденным <see cref="ClientDto"/> или сообщением об ошибке.</returns>
    Result<ClientDto> FindByDocument(string documentNumber);

    /// <summary>
    /// Выполняет поиск пассажиров по подстроке ФИО или номера документа.
    /// </summary>
    /// <param name="searchPattern">Подстрока для поиска (если пустая — возвращаются все клиенты).</param>
    /// <returns>Список найденных пассажиров, отсортированный по ФИО.</returns>
    IReadOnlyList<ClientDto> SearchClients(string searchPattern);

    /// <summary>
    /// Создаёт новую карточку пассажира (при Id == 0) или обновляет существующую (при Id > 0).
    /// </summary>
    /// <param name="client">DTO с данными пассажира.</param>
    /// <returns>Результат операции с сохранённым <see cref="ClientDto"/> или сообщением об ошибке.</returns>
    Result<ClientDto> CreateOrUpdateClient(ClientDto client);
}
