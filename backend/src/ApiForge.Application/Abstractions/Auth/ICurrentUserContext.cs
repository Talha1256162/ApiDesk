namespace ApiForge.Application.Abstractions.Auth;

public interface ICurrentUserContext
{
    CurrentUser? User { get; }
    string CorrelationId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
