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

        var invitation = await organizationRepository.InviteAsync(organizationId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, "UserInvited", "Invitation", invitation.Id, invitation.Email, "Invite", "Success", "Info", $"Invitation sent to {invitation.Email}.", null, cancellationToken);
        return Result<InvitationDto>.Success(invitation, "Invitation created.");
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

        await organizationRepository.UpdateMemberStatusAsync(organizationId, memberId, request.Status, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId, null, "MemberStatusChanged", "OrganizationMember", memberId, request.Status, "Update", "Success", "Info", "Member status changed.", null, cancellationToken);
        return Result.Success("Member status updated.");
    }
}
