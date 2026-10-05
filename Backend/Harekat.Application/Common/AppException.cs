namespace Harekat.Application.Common;

public sealed class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(string message, int statusCode = 400, string errorCode = "bad_request")
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

public static class ErrorCodes
{
    public const string NotFound = "not_found";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string Validation = "validation";
    public const string Banned = "banned";
    public const string RateLimited = "rate_limited";
}
