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

    [HttpPatch("{organizationId:guid}/members/{memberId:guid}/status")]
    public async Task<IActionResult> UpdateMemberStatus(Guid organizationId, Guid memberId, UpdateMemberStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await organizationService.UpdateMemberStatusAsync(organizationId, memberId, request, cancellationToken));
    }
}
