using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.BetaFeedback;
using ApiForge.Shared.Pagination;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class BetaFeedbackController(IBetaFeedbackService betaFeedbackService) : ApiControllerBase
{
    [HttpPost("beta-feedback")]
    public async Task<IActionResult> Create(CreateBetaFeedbackRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await betaFeedbackService.CreateAsync(request, cancellationToken));
    }

    [HttpGet("organizations/{organizationId:guid}/beta-feedback")]
    public async Task<IActionResult> GetByOrganization(Guid organizationId, [FromQuery] PagedRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await betaFeedbackService.GetByOrganizationAsync(organizationId, request, cancellationToken));
    }

    [HttpPatch("beta-feedback/{feedbackId:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid feedbackId, UpdateBetaFeedbackStatusRequest request, CancellationToken cancellationToken)
    {
        return FromResult(await betaFeedbackService.UpdateStatusAsync(feedbackId, request, cancellationToken));
    }

    [HttpGet("organizations/{organizationId:guid}/beta-checklist")]
    public async Task<IActionResult> Checklist(Guid organizationId, [FromQuery] Guid? workspaceId, CancellationToken cancellationToken)
    {
        return FromResult(await betaFeedbackService.GetChecklistAsync(organizationId, workspaceId, cancellationToken));
    }
}
