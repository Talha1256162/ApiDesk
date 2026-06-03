using ApiForge.Application.DTOs.Organizations;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IOrganizationRepository
{
    Task<IReadOnlyList<OrganizationDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<OrganizationDto> CreateAsync(Guid userId, CreateOrganizationRequest request, CancellationToken cancellationToken);
    Task<PagedResult<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrganizationRoleDto>> GetRolesAsync(CancellationToken cancellationToken);
    Task<InvitationDto> InviteAsync(Guid organizationId, InviteMemberRequest request, Guid invitedByUserId, CancellationToken cancellationToken);
    Task UpdateMemberStatusAsync(Guid organizationId, Guid memberId, string status, Guid modifiedByUserId, CancellationToken cancellationToken);
}
