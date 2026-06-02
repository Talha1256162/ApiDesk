using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Api.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/requests")]
public sealed class RequestRunnerController(IRequestRunnerService requestRunnerService, IHubContext<CollaborationHub> hubContext) : ApiControllerBase
{
    [HttpPost("{requestId:guid}/send")]
    public async Task<IActionResult> Send(Guid requestId, SendApiRequestRequest request, CancellationToken cancellationToken)
    {
        var result = await requestRunnerService.SendAsync(requestId, request, cancellationToken);
        if (result.Succeeded && result.Data is not null)
        {
            await hubContext.Clients.All.SendAsync("requestRunCompleted", result.Data, cancellationToken);
        }
        return FromResult(result);
    }

    [HttpGet("{requestId:guid}/history")]
    public async Task<IActionResult> History(Guid requestId, [FromQuery] int count, CancellationToken cancellationToken)
    {
        return FromResult(await requestRunnerService.GetHistoryAsync(requestId, count, cancellationToken));
    }

    [HttpPost("/api/collections/{collectionId:guid}/run")]
    public async Task<IActionResult> RunCollection(Guid collectionId, RunCollectionRequest request, CancellationToken cancellationToken)
    {
        var result = await requestRunnerService.RunCollectionAsync(collectionId, request, cancellationToken);
        if (result.Succeeded && result.Data is not null)
        {
            await hubContext.Clients.All.SendAsync("collectionRunCompleted", result.Data, cancellationToken);
        }
        return FromResult(result);
    }
}
