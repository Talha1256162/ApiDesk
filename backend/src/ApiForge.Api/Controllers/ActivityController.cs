using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Activity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/activity")]
public sealed class ActivityController(IActivityService activityService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await activityService.GetActivityAsync(request, cancellationToken));
    }

    [HttpGet("manager-summary")]
    public async Task<IActionResult> ManagerSummary([FromQuery] Guid workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await activityService.GetManagerSummaryAsync(workspaceId, cancellationToken));
    }
}
