using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api/requests")]
public sealed class RequestRunnerController(IRequestRunnerService requestRunnerService) : ApiControllerBase
{
    [HttpPost("{requestId:guid}/send")]
    public async Task<IActionResult> Send(Guid requestId, SendApiRequestRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await requestRunnerService.SendAsync(requestId, request, cancellationToken));
    }

    [HttpGet("{requestId:guid}/history")]
    public async Task<IActionResult> History(Guid requestId, [FromQuery] int count, CancellationToken cancellationToken)
    {
        return FromResult(await requestRunnerService.GetHistoryAsync(requestId, count, cancellationToken));
    }

    [HttpPost("/api/collections/{collectionId:guid}/run")]
    public async Task<IActionResult> RunCollection(Guid collectionId, RunCollectionRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await requestRunnerService.RunCollectionAsync(collectionId, request, cancellationToken));
    }
}
