using ApiForge.Application.DTOs.Collections;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface ICollectionService
{
    Task<Result<PagedResult<CollectionDto>>> GetCollectionsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<ApiRequestSummaryDto>>> GetCollectionRequestsAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<Result<CollectionDto>> CreateCollectionAsync(CreateCollectionRequest request, CancellationToken cancellationToken);
    Task<Result<CollectionDto>> UpdateCollectionAsync(Guid collectionId, UpdateCollectionRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<Result<FolderDto>> CreateFolderAsync(Guid collectionId, CreateFolderRequest request, CancellationToken cancellationToken);
    Task<Result<ApiRequestDetailDto>> CreateRequestAsync(Guid folderId, SaveApiRequestRequest request, CancellationToken cancellationToken);
    Task<Result<ApiRequestDetailDto>> CreateRequestInCollectionAsync(Guid collectionId, SaveApiRequestRequest request, CancellationToken cancellationToken);
    Task<Result<ApiRequestDetailDto>> GetRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task<Result<ApiRequestDetailDto>> UpdateRequestAsync(Guid requestId, SaveApiRequestRequest request, CancellationToken cancellationToken);
    Task<Result<CollectionExportDto>> ExportCollectionAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<Result<CollectionImportResultDto>> ImportCollectionAsync(Guid workspaceId, ImportCollectionRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<CommentDto>>> GetCommentsAsync(Guid workspaceId, string entityType, Guid entityId, CancellationToken cancellationToken);
    Task<Result<CommentDto>> CreateCommentAsync(Guid workspaceId, CreateCommentRequest request, CancellationToken cancellationToken);
    Task<Result<RequestExampleDto>> SaveResponseExampleAsync(Guid requestId, SaveResponseExampleRequest request, CancellationToken cancellationToken);
}
