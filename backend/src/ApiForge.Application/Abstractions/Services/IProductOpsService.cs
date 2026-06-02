using ApiForge.Application.DTOs.ProductOps;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Abstractions.Services;

public interface IProductOpsService
{
    Task<Result<IReadOnlyList<MockServerDto>>> GetMockServersAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<MockServerDto>> CreateMockServerAsync(Guid workspaceId, CreateMockServerRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MockRouteDto>>> GetMockRoutesAsync(Guid mockServerId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MockLogDto>>> GetMockLogsAsync(Guid mockServerId, int count, CancellationToken cancellationToken);
    Task<Result<MockResponseDto>> ExecuteMockAsync(string slug, string method, string path, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<MonitorDto>>> GetMonitorsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<MonitorDto>> CreateMonitorAsync(Guid workspaceId, CreateMonitorRequest request, CancellationToken cancellationToken);
    Task<Result<CollectionRunResultDto>> RunMonitorAsync(Guid monitorId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<MonitorRunDto>>> GetMonitorRunsAsync(Guid monitorId, int count, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<PublishedDocDto>>> GetPublishedDocsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<Result<PublishedDocDto>> PublishDocsAsync(Guid workspaceId, PublishDocsRequest request, CancellationToken cancellationToken);
    Task<Result> UnpublishDocsAsync(Guid docId, CancellationToken cancellationToken);
    Task<Result<DocumentationDto>> GetDocumentationAsync(string slug, CancellationToken cancellationToken);

    Task<Result<PagedResult<ApiSpecDto>>> GetApiSpecsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken);
    Task<Result<ApiSpecValidationDto>> UploadApiSpecAsync(Guid workspaceId, UploadApiSpecRequest request, CancellationToken cancellationToken);
    Task<Result<ApiSpecValidationDto>> ValidateApiSpecAsync(Guid specId, CancellationToken cancellationToken);
}
