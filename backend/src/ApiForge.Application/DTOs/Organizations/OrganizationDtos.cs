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

public sealed record InviteMemberRequest(string Email, Guid RoleId, string? Message);

public sealed record InvitationDto(Guid Id, string Email, string Status, DateTime ExpiresOn);

public sealed record UpdateMemberStatusRequest(string Status);
