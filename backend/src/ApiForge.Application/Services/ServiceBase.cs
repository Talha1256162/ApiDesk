using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public abstract class ServiceBase(ICurrentUserContext currentUserContext, IActivityRepository activityRepository)
{
    protected CurrentUser? CurrentUser => currentUserContext.User;

    protected Result<T> Unauthorized<T>() => Result<T>.Failure("Authentication is required.", new ErrorDetail("auth.required", "Authentication is required."));

    protected Result Unauthorized() => Result.Failure("Authentication is required.", new ErrorDetail("auth.required", "Authentication is required."));

    protected Result<T> Forbidden<T>(string permissionKey) => Result<T>.Failure("You do not have permission to perform this action.", new ErrorDetail("permission.denied", $"Missing permission: {permissionKey}."));

    protected Result Forbidden(string permissionKey) => Result.Failure("You do not have permission to perform this action.", new ErrorDetail("permission.denied", $"Missing permission: {permissionKey}."));

    protected Task RecordActivityAsync(
        Guid organizationId,
        Guid? workspaceId,
        string eventType,
        string entityType,
        Guid? entityId,
        string? entityName,
        string action,
        string status,
        string severity,
        string? summary,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        var user = CurrentUser;
        if (user is null)
        {
            return Task.CompletedTask;
        }

        return activityRepository.RecordAsync(new ActivityWriteModel(
            organizationId,
            workspaceId,
            user.UserId,
            user.Name,
            user.Email,
            eventType,
            entityType,
            entityId,
            entityName,
            action,
            status,
            severity,
            summary,
            metadataJson,
            currentUserContext.IpAddress,
            currentUserContext.UserAgent,
            currentUserContext.CorrelationId), cancellationToken);
    }
}
