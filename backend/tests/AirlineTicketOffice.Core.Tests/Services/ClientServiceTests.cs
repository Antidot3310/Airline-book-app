using AirlineTicketOffice.Core.Contracts.Clients;
using AirlineTicketOffice.Core.Data;
using AirlineTicketOffice.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AirlineTicketOffice.Core.Tests.Services;

/// <summary>
/// Модульные тесты для сервиса клиентов <see cref="ClientService"/>.
/// </summary>
public class ClientServiceTests
{
    private static AirlineDbContext CreateInMemoryDbContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AirlineDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AirlineDbContext(options);
        context.Database.EnsureCreated();
        DbInitializer.Initialize(context);

        return context;
    }

    [Fact]
    public void CreateOrUpdateClient_NewClient_SavesAndReturnsSuccess()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var clientService = new ClientService(context, auditService);

        var newClientDto = new ClientDto
        {
            FullName = "Иванов Иван Иванович",
            DocumentNumber = "4510 123456",
            ContactPhone = "+79991112233",
            ContactEmail = "ivanov@example.com"
        };

        // Act
        var result = clientService.CreateOrUpdateClient(newClientDto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Id > 0);
        Assert.Equal("Иванов Иван Иванович", result.Value.FullName);
        Assert.Equal("4510 123456", result.Value.DocumentNumber);
        Assert.Equal("+79991112233", result.Value.ContactPhone);
        Assert.Equal("ivanov@example.com", result.Value.ContactEmail);

        // Проверяем запись в аудите
        var logs = auditService.GetLogs(null, null, null, "CreateClient");
        Assert.Single(logs);
        Assert.Equal("Client", logs[0].EntityName);
        Assert.Equal(result.Value.Id, logs[0].EntityId);

        // Проверяем последующее обновление (Id > 0)
        var updateDto = result.Value;
        updateDto.ContactPhone = "+79998887766";
        var updateResult = clientService.CreateOrUpdateClient(updateDto);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal("+79998887766", updateResult.Value.ContactPhone);

        var updateLogs = auditService.GetLogs(null, null, null, "UpdateClient");
        Assert.Single(updateLogs);
    }

    [Fact]
    public void CreateOrUpdateClient_DuplicateDocumentNumber_ReturnsFailure()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var clientService = new ClientService(context, auditService);

        var firstClient = new ClientDto
        {
            FullName = "Иванов Иван Иванович",
            DocumentNumber = "4510 123456"
        };
        var firstResult = clientService.CreateOrUpdateClient(firstClient);
        Assert.True(firstResult.IsSuccess);

        var secondClient = new ClientDto
        {
            FullName = "Петров Петр Петрович",
            DocumentNumber = "4510 123456"
        };

        // Act
        var secondResult = clientService.CreateOrUpdateClient(secondClient);

        // Assert
        Assert.False(secondResult.IsSuccess);
        Assert.Contains("уже зарегистрирован", secondResult.ErrorMessage);
    }

    [Fact]
    public void FindByDocument_ExistingClient_ReturnsSuccessWithDto()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var clientService = new ClientService(context, auditService);

        var clientDto = new ClientDto
        {
            FullName = "Сидоров Сидор Сидорович",
            DocumentNumber = "4512 654321",
            ContactPhone = "+79993334455",
            ContactEmail = "sidorov@mail.ru"
        };
        var createResult = clientService.CreateOrUpdateClient(clientDto);
        Assert.True(createResult.IsSuccess);

        // Act
        var findResult = clientService.FindByDocument("4512 654321");

        // Assert
        Assert.True(findResult.IsSuccess);
        Assert.NotNull(findResult.Value);
        Assert.Equal(createResult.Value.Id, findResult.Value.Id);
        Assert.Equal("Сидоров Сидор Сидорович", findResult.Value.FullName);
        Assert.Equal("4512 654321", findResult.Value.DocumentNumber);
        Assert.Equal("+79993334455", findResult.Value.ContactPhone);
        Assert.Equal("sidorov@mail.ru", findResult.Value.ContactEmail);
    }

    [Fact]
    public void FindByDocument_NonExistingClient_ReturnsFailure()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var clientService = new ClientService(context, auditService);

        // Act
        var findResult = clientService.FindByDocument("9999 999999");

        // Assert
        Assert.False(findResult.IsSuccess);
        Assert.Contains("не найден", findResult.ErrorMessage);
    }

    [Fact]
    public void SearchClients_WithMatchingPattern_ReturnsFilteredClients()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var auditService = new AuditLogService(context);
        var clientService = new ClientService(context, auditService);

        clientService.CreateOrUpdateClient(new ClientDto
        {
            FullName = "Алексеев Алексей Алексеевич",
            DocumentNumber = "1111 111111"
        });
        clientService.CreateOrUpdateClient(new ClientDto
        {
            FullName = "Борисов Борис Борисович",
            DocumentNumber = "2222 222222"
        });
        clientService.CreateOrUpdateClient(new ClientDto
        {
            FullName = "Алексеева Анна Сергеевна",
            DocumentNumber = "3333 333333"
        });

        // Act
        var searchByName = clientService.SearchClients("Алексе");
        var searchByDoc = clientService.SearchClients("2222");

        // Assert
        Assert.Equal(2, searchByName.Count);
        Assert.Equal("Алексеев Алексей Алексеевич", searchByName[0].FullName);
        Assert.Equal("Алексеева Анна Сергеевна", searchByName[1].FullName);

        Assert.Single(searchByDoc);
        Assert.Equal("Борисов Борис Борисович", searchByDoc[0].FullName);
    }
}
