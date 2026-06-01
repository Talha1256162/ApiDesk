using System.Security.Claims;
using ApiForge.Application.Abstractions.Auth;

namespace ApiForge.Api.Security;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    public CurrentUser? User
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            var principal = httpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var userIdRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");
            if (!Guid.TryParse(userIdRaw, out var userId))
            {
                return null;
            }

            Guid? organizationId = Guid.TryParse(principal.FindFirstValue("organizationId"), out var orgId) ? orgId : null;
            Guid? workspaceId = Guid.TryParse(principal.FindFirstValue("workspaceId"), out var wsId) ? wsId : null;

            return new CurrentUser(
                userId,
                principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email") ?? string.Empty,
                principal.FindFirstValue("name") ?? string.Empty,
                organizationId,
                workspaceId);
        }
    }

    public string CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
