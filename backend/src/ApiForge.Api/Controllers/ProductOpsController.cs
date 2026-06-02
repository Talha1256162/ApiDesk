using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.ProductOps;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Route("api")]
public sealed class ProductOpsController(IProductOpsService productOpsService) : ApiControllerBase
{
    [Authorize]
    [HttpGet("workspaces/{workspaceId:guid}/mock-servers")]
    public async Task<IActionResult> GetMockServers(Guid workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.GetMockServersAsync(workspaceId, cancellationToken));
    }

    [Authorize]
    [HttpPost("workspaces/{workspaceId:guid}/mock-servers")]
    public async Task<IActionResult> CreateMockServer(Guid workspaceId, CreateMockServerRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.CreateMockServerAsync(workspaceId, request, cancellationToken));
    }

    [Authorize]
    [HttpGet("mock-servers/{mockServerId:guid}/routes")]
    public async Task<IActionResult> GetMockRoutes(Guid mockServerId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.GetMockRoutesAsync(mockServerId, cancellationToken));
    }

    [Authorize]
    [HttpGet("mock-servers/{mockServerId:guid}/logs")]
    public async Task<IActionResult> GetMockLogs(Guid mockServerId, [FromQuery] int count = 50, CancellationToken cancellationToken = default)
    {
        return FromResult(await productOpsService.GetMockLogsAsync(mockServerId, count, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("mock/{slug}/{**path}")]
    [HttpPost("mock/{slug}/{**path}")]
    [HttpPut("mock/{slug}/{**path}")]
    [HttpPatch("mock/{slug}/{**path}")]
    [HttpDelete("mock/{slug}/{**path}")]
    public async Task<IActionResult> ExecuteMock(string slug, string? path, CancellationToken cancellationToken)
    {
        var result = await productOpsService.ExecuteMockAsync(slug, Request.Method, "/" + (path ?? string.Empty), cancellationToken);
        if (!result.Succeeded || result.Data is null)
        {
            return NotFound(result);
        }

        return Content(result.Data.Body, result.Data.ContentType);
    }

    [Authorize]
    [HttpGet("workspaces/{workspaceId:guid}/monitors")]
    public async Task<IActionResult> GetMonitors(Guid workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.GetMonitorsAsync(workspaceId, cancellationToken));
    }

    [Authorize]
    [HttpPost("workspaces/{workspaceId:guid}/monitors")]
    public async Task<IActionResult> CreateMonitor(Guid workspaceId, CreateMonitorRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.CreateMonitorAsync(workspaceId, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("monitors/{monitorId:guid}/run")]
    public async Task<IActionResult> RunMonitor(Guid monitorId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.RunMonitorAsync(monitorId, cancellationToken));
    }

    [Authorize]
    [HttpGet("monitors/{monitorId:guid}/runs")]
    public async Task<IActionResult> GetMonitorRuns(Guid monitorId, [FromQuery] int count = 25, CancellationToken cancellationToken = default)
    {
        return FromResult(await productOpsService.GetMonitorRunsAsync(monitorId, count, cancellationToken));
    }

    [Authorize]
    [HttpGet("workspaces/{workspaceId:guid}/published-docs")]
    public async Task<IActionResult> GetPublishedDocs(Guid workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.GetPublishedDocsAsync(workspaceId, cancellationToken));
    }

    [Authorize]
    [HttpPost("workspaces/{workspaceId:guid}/published-docs")]
    public async Task<IActionResult> PublishDocs(Guid workspaceId, PublishDocsRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.PublishDocsAsync(workspaceId, request, cancellationToken));
    }

    [Authorize]
    [HttpDelete("published-docs/{docId:guid}")]
    public async Task<IActionResult> UnpublishDocs(Guid docId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.UnpublishDocsAsync(docId, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("docs/{slug}")]
    public async Task<IActionResult> GetDocumentation(string slug, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.GetDocumentationAsync(slug, cancellationToken));
    }

    [Authorize]
    [HttpGet("workspaces/{workspaceId:guid}/api-specs")]
    public async Task<IActionResult> GetApiSpecs(Guid workspaceId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.GetApiSpecsAsync(workspaceId, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("workspaces/{workspaceId:guid}/api-specs")]
    public async Task<IActionResult> UploadApiSpec(Guid workspaceId, UploadApiSpecRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.UploadApiSpecAsync(workspaceId, request, cancellationToken));
    }

    [Authorize]
    [HttpPost("api-specs/{specId:guid}/validate")]
    public async Task<IActionResult> ValidateApiSpec(Guid specId, CancellationToken cancellationToken)
    {
        return FromResult(await productOpsService.ValidateApiSpecAsync(specId, cancellationToken));
    }
}
