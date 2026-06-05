using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Organizations;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class OrganizationService(
    IOrganizationRepository organizationRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IOrganizationService
{
    public async Task<Result<IReadOnlyList<OrganizationDto>>> GetMyOrganizationsAsync(CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<OrganizationDto>>();
        }

        var organizations = await organizationRepository.GetByUserAsync(CurrentUser.UserId, cancellationToken);
        return Result<IReadOnlyList<OrganizationDto>>.Success(organizations);
    }

    public async Task<Result<OrganizationDto>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<OrganizationDto>();
        }

        var organization = await organizationRepository.CreateAsync(CurrentUser.UserId, request, cancellationToken);
        await RecordActivityAsync(organization.Id, null, "OrganizationCreated", "Organization", organization.Id, organization.Name, "Create", "Success", "Info", "Organization created.", null, cancellationToken);
        return Result<OrganizationDto>.Success(organization, "Organization created.");
    }

    public async Task<Result<PagedResult<OrganizationMemberDto>>> GetMembersAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<PagedResult<OrganizationMemberDto>>();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.ViewTeamActivity, cancellationToken);
        if (!allowed)
        {
            return Forbidden<PagedResult<OrganizationMemberDto>>(PermissionKeys.ViewTeamActivity);
        }

        var members = await organizationRepository.GetMembersAsync(organizationId, request, cancellationToken);
        return Result<PagedResult<OrganizationMemberDto>>.Success(members);
    }

    public async Task<Result<IReadOnlyList<OrganizationRoleDto>>> GetRolesAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<OrganizationRoleDto>>();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.ViewTeamActivity, cancellationToken);
        if (!allowed)
        {
            return Forbidden<IReadOnlyList<OrganizationRoleDto>>(PermissionKeys.ViewTeamActivity);
        }

        var roles = await organizationRepository.GetRolesAsync(cancellationToken);
        return Result<IReadOnlyList<OrganizationRoleDto>>.Success(roles);
    }

    public async Task<Result<InvitationDto>> InviteAsync(Guid organizationId, InviteMemberRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<InvitationDto>();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.InviteMembers, cancellationToken);
        if (!allowed)
        {
            return Forbidden<InvitationDto>(PermissionKeys.InviteMembers);
        }

        if (!await HasValidAssignableRoleScopeAsync(request.RoleId, cancellationToken))
        {
            return Result<InvitationDto>.Failure("Role scope is not valid for organization membership.", new ErrorDetail("role.scope_invalid", "Role scope is not valid for organization membership."));
        }

        var invitation = await organizationRepository.InviteAsync(organizationId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, "UserInvited", "Invitation", invitation.Id, invitation.Email, "Invite", "Success", "Info", $"Invitation created for {invitation.Email}.", null, cancellationToken);
        return Result<InvitationDto>.Success(invitation, "Invitation created.");
    }

    public async Task<Result<InvitationDto>> RegenerateInvitationAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<InvitationDto>();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.InviteMembers, cancellationToken);
        if (!allowed)
        {
            return Forbidden<InvitationDto>(PermissionKeys.InviteMembers);
        }

        var invitation = await organizationRepository.RegenerateInvitationAsync(organizationId, invitationId, CurrentUser.UserId, cancellationToken);
        if (invitation is null)
        {
            return Result<InvitationDto>.Failure("Invitation was not found or is no longer pending.", new ErrorDetail("invitation.not_found", "Invitation was not found or is no longer pending."));
        }

        await RecordActivityAsync(organizationId, null, "InviteRegenerated", "Invitation", invitation.Id, invitation.Email, "Regenerate", "Success", "Info", "Invitation link regenerated.", null, cancellationToken);
        return Result<InvitationDto>.Success(invitation, "Invitation link regenerated.");
    }

    public async Task<Result> RevokeInvitationAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.InviteMembers, cancellationToken);
        if (!allowed)
        {
            return Forbidden(PermissionKeys.InviteMembers);
        }

        var revoked = await organizationRepository.RevokeInvitationAsync(organizationId, invitationId, CurrentUser.UserId, cancellationToken);
        if (!revoked)
        {
            return Result.Failure("Invitation was not found or is no longer pending.", new ErrorDetail("invitation.not_found", "Invitation was not found or is no longer pending."));
        }

        await RecordActivityAsync(organizationId, null, "InviteRevoked", "Invitation", invitationId, null, "Revoke", "Success", "Info", "Invitation revoked.", null, cancellationToken);
        return Result.Success("Invitation revoked.");
    }

    public async Task<Result<InvitationAcceptanceDto>> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<InvitationAcceptanceDto>();
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Result<InvitationAcceptanceDto>.Failure("Invitation token is required.", new ErrorDetail("invitation.token_required", "Invitation token is required."));
        }

        var accepted = await organizationRepository.AcceptInvitationAsync(request.Token.Trim(), CurrentUser.UserId, CurrentUser.Email, cancellationToken);
        if (accepted is null)
        {
            return Result<InvitationAcceptanceDto>.Failure("Invitation is invalid, expired, or belongs to a different email address.", new ErrorDetail("invitation.invalid", "Invitation is invalid, expired, or belongs to a different email address."));
        }

        await RecordActivityAsync(accepted.OrganizationId, null, "InviteAccepted", "OrganizationMember", accepted.MemberId, accepted.Email, "Accept", "Success", "Info", "Invitation accepted.", null, cancellationToken);
        return Result<InvitationAcceptanceDto>.Success(accepted, "Invitation accepted.");
    }

    public async Task<Result> ChangeMemberRoleAsync(Guid organizationId, Guid memberId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.ManageOrganization, cancellationToken);
        if (!allowed)
        {
            return Forbidden(PermissionKeys.ManageOrganization);
        }

        var member = await organizationRepository.GetMemberAsync(organizationId, memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure("Member was not found.", new ErrorDetail("member.not_found", "Member was not found."));
        }

        var actor = await organizationRepository.GetMemberByUserIdAsync(organizationId, CurrentUser.UserId, cancellationToken);
        if (member.RoleName == RoleNames.Owner && actor?.RoleName != RoleNames.Owner)
        {
            return Result.Failure("Only an owner can change another owner's role.", new ErrorDetail("member.owner_protected", "Only an owner can change another owner's role."));
        }

        if (member.RoleName == RoleNames.Owner && await organizationRepository.CountActiveOwnersAsync(organizationId, cancellationToken) <= 1)
        {
            return Result.Failure("You cannot change the only active owner's role. Add or transfer another owner first.", new ErrorDetail("member.only_owner", "You cannot change the only active owner's role."));
        }

        if (!await HasValidAssignableRoleScopeAsync(request.RoleId, cancellationToken))
        {
            return Result.Failure("Role scope is not valid for organization membership.", new ErrorDetail("role.scope_invalid", "Role scope is not valid for organization membership."));
        }

        await organizationRepository.ChangeMemberRoleAsync(organizationId, memberId, request.RoleId, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, "MemberRoleChanged", "OrganizationMember", memberId, member.Email, "UpdateRole", "Success", "Info", "Member role changed.", null, cancellationToken);
        return Result.Success("Member role updated.");
    }

    public async Task<Result> UpdateMemberStatusAsync(Guid organizationId, Guid memberId, UpdateMemberStatusRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.ManageOrganization, cancellationToken);
        if (!allowed)
        {
            return Forbidden(PermissionKeys.ManageOrganization);
        }

        var status = request.Status.Trim();
        if (!new[] { "Active", "Suspended", "Removed" }.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure("Member status is not supported.", new ErrorDetail("member.status_invalid", "Member status is not supported."));
        }

        var member = await organizationRepository.GetMemberAsync(organizationId, memberId, cancellationToken);
        if (member is null)
        {
            return Result.Failure("Member was not found.", new ErrorDetail("member.not_found", "Member was not found."));
        }

        var actor = await organizationRepository.GetMemberByUserIdAsync(organizationId, CurrentUser.UserId, cancellationToken);
        if (member.RoleName == RoleNames.Owner && actor?.RoleName != RoleNames.Owner)
        {
            return Result.Failure("Only an owner can change an owner's member status.", new ErrorDetail("member.owner_protected", "Only an owner can change an owner's member status."));
        }

        if (status.Equals("Removed", StringComparison.OrdinalIgnoreCase) && member.UserId == CurrentUser.UserId && member.RoleName == RoleNames.Owner && await organizationRepository.CountActiveOwnersAsync(organizationId, cancellationToken) <= 1)
        {
            return Result.Failure("You cannot remove yourself as the only active owner. Add or transfer another owner first.", new ErrorDetail("member.only_owner", "You cannot remove the only active owner."));
        }

        if (status.Equals("Removed", StringComparison.OrdinalIgnoreCase) && member.RoleName == RoleNames.Owner && await organizationRepository.CountActiveOwnersAsync(organizationId, cancellationToken) <= 1)
        {
            return Result.Failure("You cannot remove the only active owner. Add or transfer another owner first.", new ErrorDetail("member.only_owner", "You cannot remove the only active owner."));
        }

        await organizationRepository.UpdateMemberStatusAsync(organizationId, memberId, status, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, status.Equals("Removed", StringComparison.OrdinalIgnoreCase) ? "MemberRemoved" : "MemberStatusChanged", "OrganizationMember", memberId, member.Email, "Update", "Success", "Info", $"Member status changed to {status}.", null, cancellationToken);
        return Result.Success("Member status updated.");
    }

    private async Task<bool> HasValidAssignableRoleScopeAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var scope = await organizationRepository.GetRoleScopeAsync(roleId, cancellationToken);
        return scope is not null && new[] { "Organization", "Workspace" }.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }
}
