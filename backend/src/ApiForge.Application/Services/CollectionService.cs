using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.Collections;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class CollectionService(
    ICollectionRepository collectionRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), ICollectionService
{
    public async Task<Result<PagedResult<CollectionDto>>> GetCollectionsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<PagedResult<CollectionDto>>();
        }

        var collections = await collectionRepository.GetCollectionsAsync(workspaceId, request, cancellationToken);
        return Result<PagedResult<CollectionDto>>.Success(collections);
    }

    public async Task<Result<IReadOnlyList<ApiRequestSummaryDto>>> GetCollectionRequestsAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<ApiRequestSummaryDto>>();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result<IReadOnlyList<ApiRequestSummaryDto>>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        var requests = await collectionRepository.GetCollectionRequestsAsync(collectionId, cancellationToken);
        return Result<IReadOnlyList<ApiRequestSummaryDto>>.Success(requests);
    }

    public async Task<Result<CollectionDto>> CreateCollectionAsync(CreateCollectionRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<CollectionDto>();
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(request.WorkspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<CollectionDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, request.WorkspaceId, PermissionKeys.CreateCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden<CollectionDto>(PermissionKeys.CreateCollection);
        }

        var collection = await collectionRepository.CreateCollectionAsync(request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, request.WorkspaceId, "CollectionCreated", "Collection", collection.Id, collection.Name, "Create", "Success", "Info", "Collection created.", null, cancellationToken);
        return Result<CollectionDto>.Success(collection, "Collection created.");
    }

    public async Task<Result<CollectionDto>> UpdateCollectionAsync(Guid collectionId, UpdateCollectionRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<CollectionDto>();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result<CollectionDto>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.EditCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden<CollectionDto>(PermissionKeys.EditCollection);
        }

        var collection = await collectionRepository.UpdateCollectionAsync(collectionId, request, CurrentUser.UserId, cancellationToken);
        if (collection is null)
        {
            return Result<CollectionDto>.Failure("Collection update failed. It may have changed since you loaded it.", new ErrorDetail("collection.version_conflict", "Version conflict or missing collection."));
        }

        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "CollectionUpdated", "Collection", collection.Id, collection.Name, "Update", "Success", "Info", "Collection updated.", null, cancellationToken);
        return Result<CollectionDto>.Success(collection, "Collection updated.");
    }

    public async Task<Result> DeleteCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.DeleteCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden(PermissionKeys.DeleteCollection);
        }

        await collectionRepository.DeleteCollectionAsync(collectionId, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "CollectionDeleted", "Collection", collectionId, "Collection", "Delete", "Success", "Warning", "Collection deleted.", null, cancellationToken);
        return Result.Success("Collection deleted.");
    }

    public async Task<Result<FolderDto>> CreateFolderAsync(Guid collectionId, CreateFolderRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<FolderDto>();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result<FolderDto>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.EditCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden<FolderDto>(PermissionKeys.EditCollection);
        }

        var folder = await collectionRepository.CreateFolderAsync(collectionId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "FolderCreated", "Folder", folder.Id, folder.Name, "Create", "Success", "Info", "Folder created.", null, cancellationToken);
        return Result<FolderDto>.Success(folder, "Folder created.");
    }

    public async Task<Result<ApiRequestDetailDto>> CreateRequestAsync(Guid folderId, SaveApiRequestRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiRequestDetailDto>();
        }

        var scope = await collectionRepository.GetFolderScopeAsync(folderId, cancellationToken);
        if (scope is null)
        {
            return Result<ApiRequestDetailDto>.Failure("Folder was not found.", new ErrorDetail("folder.not_found", "Folder was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.EditCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden<ApiRequestDetailDto>(PermissionKeys.EditCollection);
        }

        var apiRequest = await collectionRepository.CreateRequestAsync(folderId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "RequestCreated", "Request", apiRequest.Id, apiRequest.Name, "Create", "Success", "Info", "Request created.", null, cancellationToken);
        return Result<ApiRequestDetailDto>.Success(apiRequest, "Request created.");
    }

    public async Task<Result<ApiRequestDetailDto>> CreateRequestInCollectionAsync(Guid collectionId, SaveApiRequestRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiRequestDetailDto>();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result<ApiRequestDetailDto>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        if (request.CollectionId != collectionId || request.WorkspaceId != scope.Value.WorkspaceId)
        {
            return Result<ApiRequestDetailDto>.Failure("Request scope does not match the selected collection.", new ErrorDetail("request.scope_mismatch", "Request scope does not match the selected collection."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.EditCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden<ApiRequestDetailDto>(PermissionKeys.EditCollection);
        }

        var apiRequest = await collectionRepository.CreateRequestAsync(null, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "RequestCreated", "Request", apiRequest.Id, apiRequest.Name, "Create", "Success", "Info", "Request created.", null, cancellationToken);
        return Result<ApiRequestDetailDto>.Success(apiRequest, "Request created.");
    }

    public async Task<Result<ApiRequestDetailDto>> GetRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiRequestDetailDto>();
        }

        var apiRequest = await collectionRepository.GetRequestAsync(requestId, cancellationToken);
        return apiRequest is null
            ? Result<ApiRequestDetailDto>.Failure("Request was not found.", new ErrorDetail("request.not_found", "Request was not found."))
            : Result<ApiRequestDetailDto>.Success(apiRequest);
    }

    public async Task<Result<ApiRequestDetailDto>> UpdateRequestAsync(Guid requestId, SaveApiRequestRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiRequestDetailDto>();
        }

        var scope = await collectionRepository.GetRequestScopeAsync(requestId, cancellationToken);
        if (scope is null)
        {
            return Result<ApiRequestDetailDto>.Failure("Request was not found.", new ErrorDetail("request.not_found", "Request was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.EditCollection, cancellationToken);
        if (!allowed)
        {
            return Forbidden<ApiRequestDetailDto>(PermissionKeys.EditCollection);
        }

        var apiRequest = await collectionRepository.UpdateRequestAsync(requestId, request, CurrentUser.UserId, cancellationToken);
        if (apiRequest is null)
        {
            return Result<ApiRequestDetailDto>.Failure("Request update failed. It may have changed since you loaded it.", new ErrorDetail("request.version_conflict", "Version conflict or missing request."));
        }

        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "RequestUpdated", "Request", requestId, apiRequest.Name, "Update", "Success", "Info", "Request updated.", null, cancellationToken);
        return Result<ApiRequestDetailDto>.Success(apiRequest, "Request updated.");
    }

    public async Task<Result<CollectionExportDto>> ExportCollectionAsync(Guid collectionId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<CollectionExportDto>();
        }

        var scope = await collectionRepository.GetCollectionScopeAsync(collectionId, cancellationToken);
        if (scope is null)
        {
            return Result<CollectionExportDto>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ExportCollections, cancellationToken);
        if (!allowed)
        {
            return Forbidden<CollectionExportDto>(PermissionKeys.ExportCollections);
        }

        var export = await collectionRepository.ExportCollectionAsync(collectionId, cancellationToken);
        if (export is null)
        {
            return Result<CollectionExportDto>.Failure("Collection was not found.", new ErrorDetail("collection.not_found", "Collection was not found."));
        }

        await RecordActivityAsync(scope.Value.OrganizationId, scope.Value.WorkspaceId, "CollectionExported", "Collection", collectionId, export.Collection.Name, "Export", "Success", "Info", "Collection exported.", null, cancellationToken);
        return Result<CollectionExportDto>.Success(export, "Collection exported.");
    }

    public async Task<Result<CollectionImportResultDto>> ImportCollectionAsync(Guid workspaceId, ImportCollectionRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<CollectionImportResultDto>();
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return Result<CollectionImportResultDto>.Failure("Workspace was not found.", new ErrorDetail("workspace.not_found", "Workspace was not found."));
        }

        var allowed = await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, workspaceId, PermissionKeys.ImportCollections, cancellationToken);
        if (!allowed)
        {
            return Forbidden<CollectionImportResultDto>(PermissionKeys.ImportCollections);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Requests.Count == 0)
        {
            return Result<CollectionImportResultDto>.Failure("Import file must include a collection name and at least one request.", new ErrorDetail("collection.import_invalid", "Import file must include a collection name and at least one request."));
        }

        var imported = await collectionRepository.ImportCollectionAsync(workspaceId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, workspaceId, "CollectionImported", "Collection", imported.CollectionId, imported.Name, "Import", "Success", "Info", $"{imported.RequestCount} requests imported.", null, cancellationToken);
        return Result<CollectionImportResultDto>.Success(imported, "Collection imported.");
    }
}
