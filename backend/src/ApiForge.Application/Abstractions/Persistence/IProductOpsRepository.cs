using ApiForge.Application.DTOs.ProductOps;
using ApiForge.Shared.Pagination;

namespace ApiForge.Application.Abstractions.Persistence;

public interface IProductOpsRepository
{
    Task<(Guid OrganizationId, Guid WorkspaceId, string CollectionName)?> GetCollectionScopeAsync(Guid collectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MockServerDto>> GetMockServersAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<MockServerDto> CreateMockServerAsync(Guid workspaceId, CreateMockServerRequest request, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MockRouteDto>> GetMockRoutesAsync(Guid mockServerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MockLogDto>> GetMockLogsAsync(Guid mockServerId, int count, CancellationToken cancellationToken);
    Task<MockResponseDto?> MatchMockResponseAsync(string slug, string method, string path, CancellationToken cancellationToken);
    Task RecordMockLogAsync(string slug, Guid? routeId, string method, string path, int statusCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonitorDto>> GetMonitorsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<MonitorDto> CreateMonitorAsync(Guid workspaceId, CreateMonitorRequest request, Guid userId, CancellationToken cancellationToken);
    Task<MonitorDto?> GetMonitorAsync(Guid monitorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScheduledMonitorDto>> GetEnabledMonitorsAsync(CancellationToken cancellationToken);
    Task AddMonitorRunAsync(Guid monitorId, string status, int passedCount, int failedCount, long? latencyMs, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MonitorRunDto>> GetMonitorRunsAsync(Guid monitorId, int count, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedDocDto>> GetPublishedDocsAsync(Guid workspaceId, CancellationToken cancellationToken);
    Task<PublishedDocDto> PublishDocsAsync(Guid workspaceId, PublishDocsRequest request, string? passwordHash, Guid userId, CancellationToken cancellationToken);
    Task<bool> UnpublishDocsAsync(Guid docId, Guid userId, CancellationToken cancellationToken);
    Task<DocumentationDto?> GetDocumentationAsync(string slug, CancellationToken cancellationToken);

    Task<PagedResult<ApiSpecDto>> GetApiSpecsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken);
    Task<ApiSpecDto> UploadApiSpecAsync(Guid workspaceId, UploadApiSpecRequest request, string validationStatus, Guid userId, CancellationToken cancellationToken);
    Task<string?> GetApiSpecContentAsync(Guid specId, CancellationToken cancellationToken);
}
