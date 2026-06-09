using ApiForge.Application.DTOs.BetaFeedback;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IBetaFeedbackRepository
{
    Task<BetaFeedbackDto> CreateAsync(CreateBetaFeedbackRequest request, Guid actorUserId, string actorName, string actorEmail, CancellationToken cancellationToken);
    Task<Guid?> GetOrganizationIdAsync(Guid feedbackId, CancellationToken cancellationToken);
    Task<PagedResult<BetaFeedbackDto>> GetByOrganizationAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken);
    Task<BetaFeedbackDto?> UpdateStatusAsync(Guid feedbackId, UpdateBetaFeedbackStatusRequest request, Guid modifiedBy, CancellationToken cancellationToken);
    Task<BetaChecklistSignals> GetChecklistSignalsAsync(Guid organizationId, Guid? workspaceId, Guid actorUserId, CancellationToken cancellationToken);
}
