namespace HrastERP.SharedKernel.Results;

/// <summary>
/// Represents the outcome of an operation that does not return a value.
/// Used instead of throwing exceptions for expected failures (validation errors, not found, etc.).
/// Usage: return Result.Success() or Result.Failure(error).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    // Protected constructor — only this class and Result<TValue> (which inherits from Result) can call it.
    // Forces external code to use the Success() / Failure() factory methods instead of "new Result(...)".
    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    // Factory methods — the only way to create a Result from outside the class.
    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);
}

/// <summary>
/// Represents the outcome of an operation that returns a value of type TValue on success.
/// Inherits from Result so it can be used anywhere a Result is expected.
/// </summary>
public sealed class Result<TValue> : Result
{
    // Nullable because failed results have no value
    private readonly TValue? _value;

    private Result(TValue value) : base(true, Error.None)
    {
        _value = value;
    }

    private Result(Error error) : base(false, error)
    {
        _value = default;
    }

    // Provides access to the value, but only on success.
    // Throws if the result is a failure — forces callers to check IsSuccess first.
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value of a failed result.");

    public static Result<TValue> Success(TValue value) => new(value);

    // "new" keyword hides the parent's Failure method because this version
    // returns Result<TValue> instead of Result.
    public new static Result<TValue> Failure(Error error) => new(error);

    // Implicit operators let you return a value or error directly without calling the factory methods.
    // Example: "return order;" instead of "return Result<Order>.Success(order);"
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    // Example: "return error;" instead of "return Result<Order>.Failure(error);"
    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
