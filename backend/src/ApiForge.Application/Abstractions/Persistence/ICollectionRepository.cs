using ApiForge.Application.DTOs.Collections;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface ICollectionRepository
{
    Task<Guid?> GetWorkspaceOrganizationIdAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<(Guid WorkspaceId, Guid OrganizationId)?> GetCollectionScopeAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<(Guid WorkspaceId, Guid OrganizationId)?> GetFolderScopeAsync(Guid folderId, CancellationToken cancellationToken);
    Task<(Guid WorkspaceId, Guid OrganizationId)?> GetRequestScopeAsync(Guid requestId, CancellationToken cancellationToken);
    Task<PagedResult<CollectionDto>> GetCollectionsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApiRequestSummaryDto>> GetCollectionRequestsAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<CollectionDto> CreateCollectionAsync(CreateCollectionRequest request, Guid userId, CancellationToken cancellationToken);
    Task<CollectionDto?> UpdateCollectionAsync(Guid collectionId, UpdateCollectionRequest request, Guid userId, CancellationToken cancellationToken);
    Task<bool> DeleteCollectionAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken);
    Task<FolderDto> CreateFolderAsync(Guid collectionId, CreateFolderRequest request, Guid userId, CancellationToken cancellationToken);
    Task<ApiRequestDetailDto> CreateRequestAsync(Guid? folderId, SaveApiRequestRequest request, Guid userId, CancellationToken cancellationToken);
    Task<ApiRequestDetailDto?> GetRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<ApiRequestDetailDto?> UpdateRequestAsync(Guid requestId, SaveApiRequestRequest request, Guid userId, CancellationToken cancellationToken);
    Task<CollectionExportDto?> ExportCollectionAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<CollectionImportResultDto> ImportCollectionAsync(Guid workspaceId, ImportCollectionRequest request, Guid userId, CancellationToken cancellationToken);
}
