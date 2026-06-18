namespace ApiForge.Application.DTOs.Organizations;

public sealed record OrganizationDto(Guid Id, string Name, string Slug, string? ProductName, DateTime CreatedOn);

public sealed record CreateOrganizationRequest(string Name, string? ProductName);

public sealed record OrganizationMemberDto(
    Guid Id,
    Guid UserId,
    string FullName,
    string Email,
    string? AvatarUrl,
    string Status,
    string RoleName,
    DateTime? LastActiveOn,
    DateTime CreatedOn);

public sealed record InviteMemberRequest(string Email, Guid RoleId, string? Message, Guid? WorkspaceId = null);

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Status,
    DateTime ExpiresOn,
    string? InviteToken = null,
    Guid? WorkspaceId = null,
    string? WorkspaceName = null,
    string EmailDeliveryStatus = "NotAttempted",
    string? EmailDeliveryError = null,
    string? InviteUrl = null);

public sealed record UpdateMemberStatusRequest(string Status);
public sealed record ChangeMemberRoleRequest(Guid RoleId);
public sealed record AcceptInvitationRequest(string Token);
public sealed record InvitationAcceptanceDto(Guid OrganizationId, Guid MemberId, string Email, string Status);

public sealed record OrganizationRoleDto(Guid Id, string Name, string Scope);

public sealed record InvitationEmailContextDto(
    Guid OrganizationId,
    string OrganizationName,
    Guid? WorkspaceId,
    string? WorkspaceName,
    string InviterName,
    string InviterEmail);
