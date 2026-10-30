using System.Reflection;

namespace AirlineTicketOffice.Core.Tests;

/// <summary>
/// Базовые дымовые тесты для проверки корректности настройки тестового окружения.
/// </summary>
public class SmokeTests
{
    /// <summary>
    /// Проверяет доступность и корректность подключения проекта Core.
    /// </summary>
    [Fact]
    public void CoreProject_ShouldBeReferencedAndFunctional()
    {
        // Arrange & Act
        Assembly coreAssembly = Assembly.Load("AirlineTicketOffice.Core");

        // Assert
        Assert.NotNull(coreAssembly);
        Assert.True(true);
    }
}
