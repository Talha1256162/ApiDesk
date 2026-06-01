using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Workspaces;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceService workspaceService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByOrganization([FromQuery] Guid organizationId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await workspaceService.GetWorkspacesAsync(organizationId, request, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await workspaceService.CreateAsync(request, cancellationToken));
    }

    [HttpGet("{workspaceId:guid}/dashboard")]
    public async Task<IActionResult> Dashboard(Guid workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await workspaceService.GetDashboardAsync(workspaceId, cancellationToken));
    }

    [HttpPut("{workspaceId:guid}")]
    public async Task<IActionResult> Update(Guid workspaceId, UpdateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await workspaceService.UpdateAsync(workspaceId, request, cancellationToken));
    }

    [HttpDelete("{workspaceId:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await workspaceService.DeleteAsync(workspaceId, cancellationToken));
    }
}
