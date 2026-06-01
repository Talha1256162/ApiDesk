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
}
