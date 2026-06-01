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

    [HttpPut("environments/{environmentId:guid}/variables")]
    public async Task<IActionResult> UpsertVariables(Guid environmentId, UpsertEnvironmentVariablesRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await environmentService.UpsertVariablesAsync(environmentId, request, cancellationToken));
    }
}
