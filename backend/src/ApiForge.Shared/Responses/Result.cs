namespace ApiForge.Shared.Responses;

public class Result
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = "Success";
    public IReadOnlyList<ErrorDetail> Errors { get; init; } = Array.Empty<ErrorDetail>();

    public static Result Success(string message = "Success") => new()
    {
        Succeeded = true,
        Message = message
    };

    public static Result Failure(string message, params ErrorDetail[] errors) => new()
    {
        Succeeded = false,
        Message = message,
        Errors = errors
    };
}

public sealed class Result<T> : Result
{
    public T? Data { get; init; }

    public static Result<T> Success(T data, string message = "Success") => new()
    {
        Succeeded = true,
        Message = message,
        Data = data
    };

    public new static Result<T> Failure(string message, params ErrorDetail[] errors) => new()
    {
        Succeeded = false,
        Message = message,
        Errors = errors
    };
}
