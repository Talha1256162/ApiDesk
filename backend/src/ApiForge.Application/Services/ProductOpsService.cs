using System.Diagnostics;
using System.Text.Json;
using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Security;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.ProductOps;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class ProductOpsService(
    IProductOpsRepository productOpsRepository,
    ICollectionRepository collectionRepository,
    IRequestRunnerService requestRunnerService,
    IPermissionService permissionService,
    IPasswordHasher passwordHasher,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IProductOpsService
{
    public async Task<Result<IReadOnlyList<MockServerDto>>> GetMockServersAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ManageMockServers, cancellationToken);
        if (organizationId is null)
        {
            return CurrentUser is null ? Unauthorized<IReadOnlyList<MockServerDto>>() : Forbidden<IReadOnlyList<MockServerDto>>(PermissionKeys.ManageMockServers);
        }

        return Result<IReadOnlyList<MockServerDto>>.Success(await productOpsRepository.GetMockServersAsync(workspaceId, cancellationToken));
    }

    public async Task<Result<MockServerDto>> CreateMockServerAsync(Guid workspaceId, CreateMockServerRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<MockServerDto>();
        }

        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ManageMockServers, cancellationToken);
        if (organizationId is null)
        {
            return Forbidden<MockServerDto>(PermissionKeys.ManageMockServers);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<MockServerDto>.Failure("Mock server name is required.", new ErrorDetail("mock.name_required", "Mock server name is required."));
        }

        var server = await productOpsRepository.CreateMockServerAsync(workspaceId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, workspaceId, "MockServerCreated", "MockServer", server.Id, server.Name, "Create", "Success", "Info", $"{server.RouteCount} mock routes generated from collection.", null, cancellationToken);
        return Result<MockServerDto>.Success(server, "Mock server created.");
    }

    public async Task<Result<IReadOnlyList<MockRouteDto>>> GetMockRoutesAsync(Guid mockServerId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<MockRouteDto>>();
        }

        var scope = await productOpsRepository.GetMockServerScopeAsync(mockServerId, cancellationToken);
        if (scope is null)
        {
            return Result<IReadOnlyList<MockRouteDto>>.Failure("Mock server was not found.", new ErrorDetail("mock.not_found", "Mock server was not found."));
        }

        if (!await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, cancellationToken))
        {
            return Forbidden<IReadOnlyList<MockRouteDto>>("workspace.member");
        }

        return Result<IReadOnlyList<MockRouteDto>>.Success(await productOpsRepository.GetMockRoutesAsync(mockServerId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<MockLogDto>>> GetMockLogsAsync(Guid mockServerId, int count, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<MockLogDto>>();
        }

        var scope = await productOpsRepository.GetMockServerScopeAsync(mockServerId, cancellationToken);
        if (scope is null)
        {
            return Result<IReadOnlyList<MockLogDto>>.Failure("Mock server was not found.", new ErrorDetail("mock.not_found", "Mock server was not found."));
        }

        if (!await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, cancellationToken))
        {
            return Forbidden<IReadOnlyList<MockLogDto>>("workspace.member");
        }

        return Result<IReadOnlyList<MockLogDto>>.Success(await productOpsRepository.GetMockLogsAsync(mockServerId, count, cancellationToken));
    }

    public async Task<Result<MockResponseDto>> ExecuteMockAsync(string slug, string method, string path, string? apiKey, CancellationToken cancellationToken)
    {
        var access = await productOpsRepository.GetMockServerAccessAsync(slug, cancellationToken);
        if (access is null)
        {
            await productOpsRepository.RecordMockLogAsync(slug, null, method, path, 404, cancellationToken);
            return Result<MockResponseDto>.Failure("Mock server was not found.", new ErrorDetail("mock.not_found", "Mock server was not found."));
        }

        if (!access.IsPublic && string.IsNullOrWhiteSpace(apiKey))
        {
            await productOpsRepository.RecordMockLogAsync(slug, null, method, path, 401, cancellationToken);
            return Result<MockResponseDto>.Failure("This private mock server requires an API key.", new ErrorDetail("auth.required", "X-API-Desk-Key is required for this mock server."));
        }

        if (access.ApiKeyRequired || !access.IsPublic)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                await productOpsRepository.RecordMockLogAsync(slug, null, method, path, 401, cancellationToken);
                return Result<MockResponseDto>.Failure("This mock server requires an API key.", new ErrorDetail("auth.required", "X-API-Desk-Key is required for this mock server."));
            }

            var validKey = await productOpsRepository.ValidateApiKeyAsync(access.OrganizationId, access.WorkspaceId, apiKey, cancellationToken);
            if (!validKey)
            {
                await productOpsRepository.RecordMockLogAsync(slug, null, method, path, 403, cancellationToken);
                return Result<MockResponseDto>.Failure("The provided mock API key is invalid.", new ErrorDetail("permission.denied", "The provided mock API key is invalid."));
            }
        }

        var response = await productOpsRepository.MatchMockResponseAsync(slug, method, path, cancellationToken);
        if (response is null)
        {
            await productOpsRepository.RecordMockLogAsync(slug, null, method, path, 404, cancellationToken);
            return Result<MockResponseDto>.Failure("No mock route matched this request.", new ErrorDetail("mock.route_not_found", "No mock route matched this request."));
        }

        await productOpsRepository.RecordMockLogAsync(slug, null, method, path, response.StatusCode, cancellationToken);
        return Result<MockResponseDto>.Success(response, "Mock response matched.");
    }

    public async Task<Result<IReadOnlyList<MonitorDto>>> GetMonitorsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ManageMonitors, cancellationToken);
        if (organizationId is null)
        {
            return CurrentUser is null ? Unauthorized<IReadOnlyList<MonitorDto>>() : Forbidden<IReadOnlyList<MonitorDto>>(PermissionKeys.ManageMonitors);
        }

        return Result<IReadOnlyList<MonitorDto>>.Success(await productOpsRepository.GetMonitorsAsync(workspaceId, cancellationToken));
    }

    public async Task<Result<MonitorDto>> CreateMonitorAsync(Guid workspaceId, CreateMonitorRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<MonitorDto>();
        }

        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ManageMonitors, cancellationToken);
        if (organizationId is null)
        {
            return Forbidden<MonitorDto>(PermissionKeys.ManageMonitors);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ScheduleExpression))
        {
            return Result<MonitorDto>.Failure("Monitor name and schedule are required.", new ErrorDetail("monitor.invalid", "Monitor name and schedule are required."));
        }

        var monitor = await productOpsRepository.CreateMonitorAsync(workspaceId, request, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, workspaceId, "MonitorCreated", "Monitor", monitor.Id, monitor.Name, "Create", "Success", "Info", monitor.ScheduleExpression, null, cancellationToken);
        return Result<MonitorDto>.Success(monitor, "Monitor created.");
    }

    public async Task<Result<CollectionRunResultDto>> RunMonitorAsync(Guid monitorId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<CollectionRunResultDto>();
        }

        var monitor = await productOpsRepository.GetMonitorAsync(monitorId, cancellationToken);
        if (monitor is null)
        {
            return Result<CollectionRunResultDto>.Failure("Monitor was not found.", new ErrorDetail("monitor.not_found", "Monitor was not found."));
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(monitor.WorkspaceId, cancellationToken);
        if (organizationId is null || !await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, organizationId.Value, monitor.WorkspaceId, cancellationToken))
        {
            return Forbidden<CollectionRunResultDto>("workspace.member");
        }

        var started = Stopwatch.StartNew();
        var result = await requestRunnerService.RunCollectionAsync(monitor.CollectionId, new RunCollectionRequest(monitor.EnvironmentId), cancellationToken);
        started.Stop();
        if (result.Succeeded && result.Data is not null)
        {
            await productOpsRepository.AddMonitorRunAsync(monitorId, result.Data.Failed == 0 ? "Passed" : "Failed", result.Data.Passed, result.Data.Failed, started.ElapsedMilliseconds, CurrentUser.UserId, cancellationToken);
            await RecordActivityAsync((await productOpsRepository.GetCollectionScopeAsync(monitor.CollectionId, cancellationToken))!.Value.OrganizationId, monitor.WorkspaceId, result.Data.Failed == 0 ? "MonitorCompleted" : "MonitorFailed", "Monitor", monitor.Id, monitor.Name, "Run", result.Data.Failed == 0 ? "Success" : "Failure", result.Data.Failed == 0 ? "Info" : "Warning", $"{result.Data.Passed}/{result.Data.TotalRequests} requests passed.", null, cancellationToken);
        }

        return result;
    }

    public async Task<Result<IReadOnlyList<MonitorRunDto>>> GetMonitorRunsAsync(Guid monitorId, int count, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<IReadOnlyList<MonitorRunDto>>();
        }

        var monitor = await productOpsRepository.GetMonitorAsync(monitorId, cancellationToken);
        if (monitor is null)
        {
            return Result<IReadOnlyList<MonitorRunDto>>.Failure("Monitor was not found.", new ErrorDetail("monitor.not_found", "Monitor was not found."));
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(monitor.WorkspaceId, cancellationToken);
        if (organizationId is null || !await permissionService.IsWorkspaceMemberAsync(CurrentUser.UserId, organizationId.Value, monitor.WorkspaceId, cancellationToken))
        {
            return Forbidden<IReadOnlyList<MonitorRunDto>>("workspace.member");
        }

        return Result<IReadOnlyList<MonitorRunDto>>.Success(await productOpsRepository.GetMonitorRunsAsync(monitorId, count, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<PublishedDocDto>>> GetPublishedDocsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ApproveApiChanges, cancellationToken);
        if (organizationId is null)
        {
            return CurrentUser is null ? Unauthorized<IReadOnlyList<PublishedDocDto>>() : Forbidden<IReadOnlyList<PublishedDocDto>>(PermissionKeys.ApproveApiChanges);
        }

        return Result<IReadOnlyList<PublishedDocDto>>.Success(await productOpsRepository.GetPublishedDocsAsync(workspaceId, cancellationToken));
    }

    public async Task<Result<PublishedDocDto>> PublishDocsAsync(Guid workspaceId, PublishDocsRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<PublishedDocDto>();
        }

        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ApproveApiChanges, cancellationToken);
        if (organizationId is null)
        {
            return Forbidden<PublishedDocDto>(PermissionKeys.ApproveApiChanges);
        }

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            return Result<PublishedDocDto>.Failure("Documentation slug is required.", new ErrorDetail("docs.slug_required", "Documentation slug is required."));
        }

        var passwordHash = string.IsNullOrWhiteSpace(request.Password) ? null : passwordHasher.Hash(request.Password);
        var doc = await productOpsRepository.PublishDocsAsync(workspaceId, request, passwordHash, CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, workspaceId, "DocumentationPublished", "PublishedDocs", doc.Id, doc.Slug, "Publish", "Success", "Info", doc.CollectionName, null, cancellationToken);
        return Result<PublishedDocDto>.Success(doc, "Documentation published.");
    }

    public async Task<Result> UnpublishDocsAsync(Guid docId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized();
        }

        var scope = await productOpsRepository.GetPublishedDocScopeAsync(docId, cancellationToken);
        if (scope is null)
        {
            return Result.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."));
        }

        if (!await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ApproveApiChanges, cancellationToken))
        {
            return Forbidden(PermissionKeys.ApproveApiChanges);
        }

        var deleted = await productOpsRepository.UnpublishDocsAsync(docId, CurrentUser.UserId, cancellationToken);
        return deleted ? Result.Success("Documentation unpublished.") : Result.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."));
    }

    public async Task<Result<DocumentationDto>> GetDocumentationAsync(string slug, CancellationToken cancellationToken)
    {
        var access = await productOpsRepository.GetDocumentationAccessAsync(slug, cancellationToken);
        if (access is null)
        {
            return Result<DocumentationDto>.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."));
        }

        if (!access.Value.IsPublic)
        {
            return Result<DocumentationDto>.Failure("Documentation is private.", new ErrorDetail("permission.denied", "Documentation is private."));
        }

        if (!string.IsNullOrWhiteSpace(access.Value.PasswordHash))
        {
            return Result<DocumentationDto>.Failure("Documentation password is required.", new ErrorDetail("auth.required", "Documentation password is required."));
        }

        var docs = await productOpsRepository.GetDocumentationAsync(slug, cancellationToken);
        return docs is null
            ? Result<DocumentationDto>.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."))
            : Result<DocumentationDto>.Success(docs);
    }

    public async Task<Result<DocumentationDto>> UnlockDocumentationAsync(string slug, UnlockDocumentationRequest request, CancellationToken cancellationToken)
    {
        var access = await productOpsRepository.GetDocumentationAccessAsync(slug, cancellationToken);
        if (access is null)
        {
            return Result<DocumentationDto>.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."));
        }

        if (!access.Value.IsPublic)
        {
            return Result<DocumentationDto>.Failure("Documentation is private.", new ErrorDetail("permission.denied", "Documentation is private."));
        }

        if (string.IsNullOrWhiteSpace(access.Value.PasswordHash))
        {
            var openDocs = await productOpsRepository.GetDocumentationAsync(slug, cancellationToken);
            return openDocs is null
                ? Result<DocumentationDto>.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."))
                : Result<DocumentationDto>.Success(openDocs);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || !passwordHasher.Verify(request.Password, access.Value.PasswordHash))
        {
            return Result<DocumentationDto>.Failure("Documentation password is invalid.", new ErrorDetail("permission.denied", "Documentation password is invalid."));
        }

        var docs = await productOpsRepository.GetDocumentationAsync(slug, cancellationToken);
        return docs is null
            ? Result<DocumentationDto>.Failure("Documentation was not found.", new ErrorDetail("docs.not_found", "Documentation was not found."))
            : Result<DocumentationDto>.Success(docs);
    }

    public async Task<Result<PagedResult<ApiSpecDto>>> GetApiSpecsAsync(Guid workspaceId, PagedRequest request, CancellationToken cancellationToken)
    {
        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ApproveApiChanges, cancellationToken);
        if (organizationId is null)
        {
            return CurrentUser is null ? Unauthorized<PagedResult<ApiSpecDto>>() : Forbidden<PagedResult<ApiSpecDto>>(PermissionKeys.ApproveApiChanges);
        }

        return Result<PagedResult<ApiSpecDto>>.Success(await productOpsRepository.GetApiSpecsAsync(workspaceId, request, cancellationToken));
    }

    public async Task<Result<ApiSpecValidationDto>> UploadApiSpecAsync(Guid workspaceId, UploadApiSpecRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiSpecValidationDto>();
        }

        var organizationId = await RequireWorkspacePermissionAsync(workspaceId, PermissionKeys.ApproveApiChanges, cancellationToken);
        if (organizationId is null)
        {
            return Forbidden<ApiSpecValidationDto>(PermissionKeys.ApproveApiChanges);
        }

        var findings = ValidateSpecContent(request.Content, request.Format);
        var spec = await productOpsRepository.UploadApiSpecAsync(workspaceId, request, findings.Any(item => item.Severity == "Error") ? "Failed" : "Passed", CurrentUser.UserId, cancellationToken);
        await RecordActivityAsync(organizationId.Value, workspaceId, findings.Any(item => item.Severity == "Error") ? "ApiSpecValidationFailed" : "ApiSpecValidated", "ApiSpec", spec.Id, spec.Name, "Validate", findings.Any(item => item.Severity == "Error") ? "Failure" : "Success", findings.Any(item => item.Severity == "Error") ? "Warning" : "Info", $"{findings.Count} governance findings.", null, cancellationToken);
        return Result<ApiSpecValidationDto>.Success(new ApiSpecValidationDto(spec, findings), "API spec uploaded and validated.");
    }

    public async Task<Result<ApiSpecValidationDto>> ValidateApiSpecAsync(Guid specId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<ApiSpecValidationDto>();
        }

        var scope = await productOpsRepository.GetApiSpecScopeAsync(specId, cancellationToken);
        if (scope is null)
        {
            return Result<ApiSpecValidationDto>.Failure("API spec was not found.", new ErrorDetail("spec.not_found", "API spec was not found."));
        }

        if (!await permissionService.HasPermissionAsync(CurrentUser.UserId, scope.Value.OrganizationId, scope.Value.WorkspaceId, PermissionKeys.ApproveApiChanges, cancellationToken))
        {
            return Forbidden<ApiSpecValidationDto>(PermissionKeys.ApproveApiChanges);
        }

        var content = await productOpsRepository.GetApiSpecContentAsync(specId, cancellationToken);
        if (content is null)
        {
            return Result<ApiSpecValidationDto>.Failure("API spec was not found.", new ErrorDetail("spec.not_found", "API spec was not found."));
        }

        var findings = ValidateSpecContent(content, "json");
        var spec = new ApiSpecDto(specId, Guid.Empty, null, "API spec", "json", findings.Any(item => item.Severity == "Error") ? "Failed" : "Passed", DateTime.UtcNow);
        return Result<ApiSpecValidationDto>.Success(new ApiSpecValidationDto(spec, findings));
    }

    private async Task<Guid?> RequireWorkspacePermissionAsync(Guid workspaceId, string permission, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return null;
        }

        var organizationId = await collectionRepository.GetWorkspaceOrganizationIdAsync(workspaceId, cancellationToken);
        if (organizationId is null)
        {
            return null;
        }

        return await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, workspaceId, permission, cancellationToken) ? organizationId : null;
    }

    private static IReadOnlyList<GovernanceFindingDto> ValidateSpecContent(string content, string format)
    {
        var findings = new List<GovernanceFindingDto>();
        if (string.IsNullOrWhiteSpace(content))
        {
            findings.Add(new GovernanceFindingDto("spec.content.required", "Error", "Spec content is required.", null));
            return findings;
        }

        if (!format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(new GovernanceFindingDto("spec.format.yaml.basic", "Info", "YAML upload was stored; detailed YAML parsing is available after adding a YAML parser package.", null));
            return findings;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (!root.TryGetProperty("openapi", out _))
            {
                findings.Add(new GovernanceFindingDto("spec.openapi.required", "Error", "OpenAPI version field is missing.", "$.openapi"));
            }

            if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            {
                findings.Add(new GovernanceFindingDto("spec.paths.required", "Error", "OpenAPI paths object is required.", "$.paths"));
                return findings;
            }

            foreach (var path in paths.EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (!operation.Value.TryGetProperty("description", out _) && !operation.Value.TryGetProperty("summary", out _))
                    {
                        findings.Add(new GovernanceFindingDto("endpoint.description.required", "Warning", "Endpoint should include a description or summary.", $"$.paths.{path.Name}.{operation.Name}"));
                    }

                    if (!operation.Value.TryGetProperty("security", out _) && !root.TryGetProperty("security", out _))
                    {
                        findings.Add(new GovernanceFindingDto("endpoint.auth.required", "Warning", "Endpoint should define auth/security.", $"$.paths.{path.Name}.{operation.Name}.security"));
                    }

                    if (operation.Value.TryGetProperty("responses", out var responses))
                    {
                        foreach (var code in new[] { "400", "401", "500" })
                        {
                            if (!responses.TryGetProperty(code, out _))
                            {
                                findings.Add(new GovernanceFindingDto("endpoint.error_response.required", "Warning", $"Response {code} should be documented.", $"$.paths.{path.Name}.{operation.Name}.responses"));
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            findings.Add(new GovernanceFindingDto("spec.json.invalid", "Error", ex.Message, null));
        }

        return findings;
    }
}
