using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Environments;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class EnvironmentsController(IEnvironmentService environmentService) : ApiControllerBase
{
    [HttpGet("workspaces/{workspaceId:guid}/environments")]
    public async Task<IActionResult> GetEnvironments(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.GetEnvironmentsAsync(workspaceId, request, cancellationToken));
    }

    [HttpPost("environments")]
    public async Task<IActionResult> Create(CreateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.CreateAsync(request, cancellationToken));
    }

    [HttpPut("environments/{environmentId:guid}")]
    public async Task<IActionResult> Update(Guid environmentId, UpdateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.UpdateAsync(environmentId, request, cancellationToken));
    }

    [HttpPost("environments/{environmentId:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid environmentId, DuplicateEnvironmentRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.DuplicateAsync(environmentId, request, cancellationToken));
    }

    [HttpDelete("environments/{environmentId:guid}")]
    public async Task<IActionResult> Delete(Guid environmentId, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.DeleteAsync(environmentId, cancellationToken));
    }

    [HttpGet("environments/{environmentId:guid}/variables")]
    public async Task<IActionResult> GetVariables(Guid environmentId, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.GetVariablesAsync(environmentId, cancellationToken));
    }

    [HttpPut("environments/{environmentId:guid}/variables")]
    public async Task<IActionResult> UpsertVariables(Guid environmentId, UpsertEnvironmentVariablesRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.UpsertVariablesAsync(environmentId, request, cancellationToken));
    }
}
