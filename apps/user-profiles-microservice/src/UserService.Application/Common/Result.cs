namespace UserService.Application.Common;

public enum ResultErrorType
{
    Validation,
    NotFound
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }

    public T? Value { get; }

    public ResultErrorType? ErrorType { get; }

    public string? Error { get; }

    private Result(bool isSuccess, T? value, ResultErrorType? errorType, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        Error = error;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public static Result<T> ValidationFailure(string error) => new(false, default, ResultErrorType.Validation, error);

    public static Result<T> NotFound(string error = "The requested resource was not found.") =>
        new(false, default, ResultErrorType.NotFound, error);
}
