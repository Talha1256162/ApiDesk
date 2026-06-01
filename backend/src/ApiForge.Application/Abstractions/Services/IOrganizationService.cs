using ApiForge.Application.DTOs.Organizations;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IOrganizationService
{
    Task<Result<IReadOnlyList<OrganizationDto>>> GetMyOrganizationsAsync(CancellationToken cancellationToken);
    Task<Result<OrganizationDto>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<OrganizationMemberDto>>> GetMembersAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken);
    Task<Result<InvitationDto>> InviteAsync(Guid organizationId, InviteMemberRequest request, CancellationToken cancellationToken);
    Task<Result> UpdateMemberStatusAsync(Guid organizationId, Guid memberId, UpdateMemberStatusRequest request, CancellationToken cancellationToken);
}
