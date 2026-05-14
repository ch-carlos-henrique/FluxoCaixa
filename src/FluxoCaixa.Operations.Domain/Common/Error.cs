namespace FluxoCaixa.Operations.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Unexpected
}

public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Unexpected)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error Validation(string code, string message)  => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message)    => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message)    => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
}
