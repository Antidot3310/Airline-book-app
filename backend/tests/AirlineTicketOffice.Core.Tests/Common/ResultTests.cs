using AirlineTicketOffice.Core.Contracts.Common;
using Xunit;

namespace AirlineTicketOffice.Core.Tests.Common;

/// <summary>
/// Модульные тесты для проверки поведения паттерна <see cref="Result"/> и <see cref="Result{T}"/>.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_WithoutValue_ReturnsIsSuccessTrueAndEmptyErrorMessage()
    {
        // Arrange & Act
        Result result = Result.Success();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void Failure_WithoutValue_ReturnsIsFailureTrueAndSpecifiedErrorMessage()
    {
        // Arrange
        string expectedError = "Рейс не найден";

        // Act
        Result result = Result.Failure(expectedError);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(expectedError, result.ErrorMessage);
    }

    [Fact]
    public void Success_WithValue_ReturnsIsSuccessTrueAndContainsValue()
    {
        // Arrange
        int expectedValue = 42;

        // Act
        Result<int> result = Result<int>.Success(expectedValue);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(expectedValue, result.Value);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void Failure_Generic_ReturnsIsFailureTrueAndDefaultValue()
    {
        // Arrange
        string expectedError = "Место уже забронировано";

        // Act
        Result<string?> result = Result<string?>.Failure(expectedError);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal(expectedError, result.ErrorMessage);
    }
}
