using ApiForge.Application.DTOs.BetaFeedback;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IBetaFeedbackService
{
    Task<Result<BetaFeedbackDto>> CreateAsync(CreateBetaFeedbackRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<BetaFeedbackDto>>> GetByOrganizationAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken);
    Task<Result<BetaFeedbackDto>> UpdateStatusAsync(Guid feedbackId, UpdateBetaFeedbackStatusRequest request, CancellationToken cancellationToken);
    Task<Result<BetaChecklistDto>> GetChecklistAsync(Guid organizationId, Guid? workspaceId, CancellationToken cancellationToken);
}
