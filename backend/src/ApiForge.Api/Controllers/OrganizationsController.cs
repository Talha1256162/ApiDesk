using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Organizations;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(IOrganizationService organizationService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.GetMyOrganizationsAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.CreateAsync(request, cancellationToken));
    }

    [HttpGet("{organizationId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid organizationId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.GetMembersAsync(organizationId, request, cancellationToken));
    }

    [HttpGet("{organizationId:guid}/roles")]
    public async Task<IActionResult> GetRoles(Guid organizationId, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.GetRolesAsync(organizationId, cancellationToken));
    }

    [HttpPost("{organizationId:guid}/invites")]
    public async Task<IActionResult> Invite(Guid organizationId, InviteMemberRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.InviteAsync(organizationId, request, cancellationToken));
    }

    [HttpPost("{organizationId:guid}/invites/{invitationId:guid}/regenerate")]
    public async Task<IActionResult> RegenerateInvite(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.RegenerateInvitationAsync(organizationId, invitationId, cancellationToken));
    }

    [HttpPatch("{organizationId:guid}/invites/{invitationId:guid}/revoke")]
    public async Task<IActionResult> RevokeInvite(Guid organizationId, Guid invitationId, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.RevokeInvitationAsync(organizationId, invitationId, cancellationToken));
    }

    [HttpPost("invites/accept")]
    public async Task<IActionResult> AcceptInvite(AcceptInvitationRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.AcceptInvitationAsync(request, cancellationToken));
    }

    [HttpPatch("{organizationId:guid}/members/{memberId:guid}/role")]
    public async Task<IActionResult> ChangeMemberRole(Guid organizationId, Guid memberId, ChangeMemberRoleRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.ChangeMemberRoleAsync(organizationId, memberId, request, cancellationToken));
    }

    [HttpPatch("{organizationId:guid}/members/{memberId:guid}/status")]
    public async Task<IActionResult> UpdateMemberStatus(Guid organizationId, Guid memberId, UpdateMemberStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.UpdateMemberStatusAsync(organizationId, memberId, request, cancellationToken));
    }
}
