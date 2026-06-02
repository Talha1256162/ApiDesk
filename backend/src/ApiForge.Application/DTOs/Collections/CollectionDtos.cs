using ApiForge.Application.DTOs.Requests;

namespace ApiForge.Application.DTOs.Collections;

public sealed record CollectionDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Description,
    Guid OwnerUserId,
    string OwnerName,
    int RequestCount,
    int VersionNumber,
    DateTime CreatedOn,
    DateTime? ModifiedOn);

public sealed record CreateCollectionRequest(Guid WorkspaceId, string Name, string? Description);
public sealed record UpdateCollectionRequest(string Name, string? Description, int VersionNumber);

public sealed record FolderDto(Guid Id, Guid CollectionId, Guid? ParentFolderId, string Name, int SortOrder, DateTime CreatedOn);
public sealed record CreateFolderRequest(string Name, Guid? ParentFolderId, int SortOrder);

public sealed record ApiRequestSummaryDto(Guid Id, Guid CollectionId, Guid? FolderId, string Name, string Method, string Url, DateTime ModifiedOn);

public sealed record ApiRequestDetailDto(
    Guid Id,
    Guid WorkspaceId,
    Guid CollectionId,
    Guid? FolderId,
    string Name,
    string? Description,
    string Method,
    string Url,
    string? AuthType,
    string? AuthConfigJson,
    string BodyType,
    string? BodyContent,
    int TimeoutMs,
    bool FollowRedirects,
    bool SslVerification,
    IReadOnlyList<KeyValueItemDto> Headers,
    IReadOnlyList<KeyValueItemDto> QueryParams,
    IReadOnlyList<KeyValueItemDto> PathParams,
    int VersionNumber,
    DateTime CreatedOn,
    DateTime? ModifiedOn);

public sealed record SaveApiRequestRequest(
    Guid WorkspaceId,
    Guid CollectionId,
    string Name,
    string? Description,
    string Method,
    string Url,
    string? AuthType,
    string? AuthConfigJson,
    string BodyType,
    string? BodyContent,
    string? PreRequestScript,
    string? TestScript,
    int TimeoutMs,
    bool FollowRedirects,
    bool SslVerification,
    IReadOnlyList<KeyValueItemDto> Headers,
    IReadOnlyList<KeyValueItemDto> QueryParams,
    IReadOnlyList<KeyValueItemDto> PathParams,
    int VersionNumber);

public sealed record ImportApiRequestRequest(
    string Name,
    string? Description,
    string Method,
    string Url,
    string? AuthType,
    string? AuthConfigJson,
    string BodyType,
    string? BodyContent,
    string? PreRequestScript,
    string? TestScript,
    int TimeoutMs,
    bool FollowRedirects,
    bool SslVerification,
    IReadOnlyList<KeyValueItemDto> Headers,
    IReadOnlyList<KeyValueItemDto> QueryParams,
    IReadOnlyList<KeyValueItemDto> PathParams);

public sealed record ImportCollectionRequest(string Name, string? Description, IReadOnlyList<ImportApiRequestRequest> Requests);
public sealed record CollectionImportResultDto(Guid CollectionId, string Name, int RequestCount);

public sealed record ApiRequestExportDto(
    Guid Id,
    string Name,
    string? Description,
    string Method,
    string Url,
    string? AuthType,
    string? AuthConfigJson,
    string BodyType,
    string? BodyContent,
    string? PreRequestScript,
    string? TestScript,
    int TimeoutMs,
    bool FollowRedirects,
    bool SslVerification,
    IReadOnlyList<KeyValueItemDto> Headers,
    IReadOnlyList<KeyValueItemDto> QueryParams,
    IReadOnlyList<KeyValueItemDto> PathParams);

public sealed record CollectionExportDto(string FormatVersion, CollectionDto Collection, IReadOnlyList<ApiRequestExportDto> Requests);
