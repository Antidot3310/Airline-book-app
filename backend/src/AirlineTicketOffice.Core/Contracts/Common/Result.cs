namespace AirlineTicketOffice.Core.Contracts.Common;

/// <summary>
/// Представляет результат выполнения операции без возвращаемого значения.
/// Реализует паттерн Result для безопасной обработки бизнес-ошибок без использования исключений.
/// </summary>
public class Result
{
    /// <summary>
    /// Возвращает значение, указывающее, завершилась ли операция успешно.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Возвращает сообщение об ошибке, если операция завершилась неудачно.
    /// В случае успешной операции содержит пустую строку.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Возвращает значение, указывающее, завершилась ли операция с ошибкой.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="Result"/>.
    /// </summary>
    /// <param name="isSuccess">Признак успешности операции.</param>
    /// <param name="errorMessage">Сообщение об ошибке (при наличии).</param>
    protected Result(bool isSuccess, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    /// <summary>
    /// Создаёт экземпляр успешного результата выполнения операции.
    /// </summary>
    /// <returns>Экземпляр <see cref="Result"/> с <see cref="IsSuccess"/> = true.</returns>
    public static Result Success() => new(true, string.Empty);

    /// <summary>
    /// Создаёт экземпляр результата выполнения операции с ошибкой.
    /// </summary>
    /// <param name="error">Описание возникшей ошибки.</param>
    /// <returns>Экземпляр <see cref="Result"/> с <see cref="IsSuccess"/> = false и заданным сообщением об ошибке.</returns>
    public static Result Failure(string error) => new(false, error);
}

/// <summary>
/// Представляет результат выполнения операции с типизированным возвращаемым значением.
/// </summary>
/// <typeparam name="T">Тип полезной нагрузки результата операции.</typeparam>
public class Result<T> : Result
{
    /// <summary>
    /// Возвращает значение, полученное в результате успешного выполнения операции.
    /// В случае ошибки возвращает значение по умолчанию для типа <typeparamref name="T"/>.
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="isSuccess">Признак успешности операции.</param>
    /// <param name="value">Полезная нагрузка операции.</param>
    /// <param name="errorMessage">Сообщение об ошибке (при наличии).</param>
    protected Result(bool isSuccess, T value, string errorMessage)
        : base(isSuccess, errorMessage)
    {
        Value = value;
    }

    /// <summary>
    /// Создаёт экземпляр успешного результата выполнения операции с возвращаемым значением.
    /// </summary>
    /// <param name="value">Значение, возвращаемое операцией.</param>
    /// <returns>Экземпляр <see cref="Result{T}"/> с <see cref="Result.IsSuccess"/> = true и установленным значением.</returns>
    public static Result<T> Success(T value) => new(true, value, string.Empty);

    /// <summary>
    /// Создаёт экземпляр результата выполнения операции с ошибкой.
    /// </summary>
    /// <param name="error">Описание возникшей ошибки.</param>
    /// <returns>Экземпляр <see cref="Result{T}"/> с <see cref="Result.IsSuccess"/> = false и заданным сообщением об ошибке.</returns>
    public static new Result<T> Failure(string error) => new(false, default!, error);
}
