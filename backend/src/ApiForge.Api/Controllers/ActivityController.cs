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

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        var result = await activityService.ExportActivityCsvAsync(request, cancellationToken);
        return result.Succeeded
            ? File(System.Text.Encoding.UTF8.GetBytes(result.Data ?? string.Empty), "text/csv", "activity.csv")
            : FromResult(result);
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await activityService.GetAuditLogsAsync(request, cancellationToken));
    }

    [HttpGet("audit/export.csv")]
    public async Task<IActionResult> AuditExportCsv([FromQuery] ActivityFilterRequest request, CancellationToken cancellationToken)
    {
        var result = await activityService.ExportAuditCsvAsync(request, cancellationToken);
        return result.Succeeded
            ? File(System.Text.Encoding.UTF8.GetBytes(result.Data ?? string.Empty), "text/csv", "audit.csv")
            : FromResult(result);
    }
}
