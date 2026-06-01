namespace ApiForge.Application.Abstractions.Auth;

public sealed record CurrentUser(Guid UserId, string Email, string Name, Guid? OrganizationId, Guid? WorkspaceId);
