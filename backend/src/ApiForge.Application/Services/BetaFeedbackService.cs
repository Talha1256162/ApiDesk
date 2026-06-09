using ApiForge.Application.Abstractions.Auth;
using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.Abstractions.Services;
using ApiForge.Application.DTOs.BetaFeedback;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using ApiForge.Shared.Responses;

namespace ApiForge.Application.Services;

public sealed class BetaFeedbackService(
    IBetaFeedbackRepository betaFeedbackRepository,
    IWorkspaceRepository workspaceRepository,
    IPermissionService permissionService,
    ICurrentUserContext currentUserContext,
    IActivityRepository activityRepository) : ServiceBase(currentUserContext, activityRepository), IBetaFeedbackService
{
    private static readonly HashSet<string> Categories = new(StringComparer.OrdinalIgnoreCase) { "Bug", "UX", "Feature", "Pricing", "Other" };
    private static readonly HashSet<string> Sentiments = new(StringComparer.OrdinalIgnoreCase) { "Positive", "Neutral", "Negative" };
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase) { "New", "Reviewed", "Planned", "Resolved", "Closed" };

    public async Task<Result<BetaFeedbackDto>> CreateAsync(CreateBetaFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<BetaFeedbackDto>();
        }

        var validation = await ValidateScopeAsync(request.OrganizationId, request.WorkspaceId, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var normalized = Normalize(request);
        var contentError = ValidateCreate(normalized);
        if (contentError is not null)
        {
            return contentError;
        }

        var feedback = await betaFeedbackRepository.CreateAsync(
            normalized,
            CurrentUser.UserId,
            CurrentUser.Name,
            CurrentUser.Email,
            cancellationToken);

        await RecordActivityAsync(
            feedback.OrganizationId,
            feedback.WorkspaceId,
            "BetaFeedbackCreated",
            "BetaFeedback",
            feedback.Id,
            feedback.Title,
            "Create",
            "Success",
            feedback.Category.Equals("Bug", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Info",
            $"Closed beta feedback submitted: {feedback.Category}.",
            null,
            cancellationToken);

        return Result<BetaFeedbackDto>.Success(feedback, "Feedback submitted.");
    }

    public async Task<Result<PagedResult<BetaFeedbackDto>>> GetByOrganizationAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<PagedResult<BetaFeedbackDto>>();
        }

        if (!await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId, null, PermissionKeys.ViewTeamActivity, cancellationToken))
        {
            return Forbidden<PagedResult<BetaFeedbackDto>>(PermissionKeys.ViewTeamActivity);
        }

        return Result<PagedResult<BetaFeedbackDto>>.Success(await betaFeedbackRepository.GetByOrganizationAsync(organizationId, request, cancellationToken));
    }

    public async Task<Result<BetaFeedbackDto>> UpdateStatusAsync(Guid feedbackId, UpdateBetaFeedbackStatusRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<BetaFeedbackDto>();
        }

        var organizationId = await betaFeedbackRepository.GetOrganizationIdAsync(feedbackId, cancellationToken);
        if (organizationId is null)
        {
            return Result<BetaFeedbackDto>.Failure("Feedback item was not found.", new ErrorDetail("feedback.not_found", "Feedback item was not found."));
        }

        if (!await permissionService.HasPermissionAsync(CurrentUser.UserId, organizationId.Value, null, PermissionKeys.ViewTeamActivity, cancellationToken))
        {
            return Forbidden<BetaFeedbackDto>(PermissionKeys.ViewTeamActivity);
        }

        var status = NormalizeStatus(request.Status);
        if (!Statuses.Contains(status))
        {
            return Result<BetaFeedbackDto>.Failure("Invalid feedback status.", new ErrorDetail("feedback.status", "Status must be New, Reviewed, Planned, Resolved, or Closed.", nameof(request.Status)));
        }

        var updated = await betaFeedbackRepository.UpdateStatusAsync(feedbackId, new UpdateBetaFeedbackStatusRequest(status, TrimTo(request.AdminNotes, 1000)), CurrentUser.UserId, cancellationToken);
        if (updated is null)
        {
            return Result<BetaFeedbackDto>.Failure("Feedback item was not found.", new ErrorDetail("feedback.not_found", "Feedback item was not found."));
        }

        await RecordActivityAsync(
            updated.OrganizationId,
            updated.WorkspaceId,
            "BetaFeedbackStatusChanged",
            "BetaFeedback",
            updated.Id,
            updated.Title,
            "Update",
            "Success",
            "Info",
            $"Closed beta feedback moved to {updated.Status}.",
            null,
            cancellationToken);

        return Result<BetaFeedbackDto>.Success(updated, "Feedback status updated.");
    }

    public async Task<Result<BetaChecklistDto>> GetChecklistAsync(Guid organizationId, Guid? workspaceId, CancellationToken cancellationToken)
    {
        if (CurrentUser is null)
        {
            return Unauthorized<BetaChecklistDto>();
        }

        if (!await permissionService.IsOrganizationMemberAsync(CurrentUser.UserId, organizationId, cancellationToken))
        {
            return Forbidden<BetaChecklistDto>("organization.member");
        }

        if (workspaceId is not null)
        {
            var workspaceOrganizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId.Value, cancellationToken);
            if (workspaceOrganizationId != organizationId)
            {
                return Result<BetaChecklistDto>.Failure("Workspace does not belong to the selected organization.", new ErrorDetail("workspace.scope", "Workspace does not belong to the selected organization.", nameof(workspaceId)));
            }
        }

        var signals = await betaFeedbackRepository.GetChecklistSignalsAsync(organizationId, workspaceId, CurrentUser.UserId, cancellationToken);
        var items = new[]
        {
            new BetaChecklistItemDto("import-collection", "Import or create a collection", "Bring in a Postman collection or create your first API collection.", signals.HasCollection, "collections"),
            new BetaChecklistItemDto("create-environment", "Create an environment", "Add baseUrl, tokens, and team variables before running requests.", signals.HasEnvironment, "environments"),
            new BetaChecklistItemDto("send-request", "Send the first request", "Run a saved request and inspect status, headers, body, and timing.", signals.HasRequestRun, "api-client"),
            new BetaChecklistItemDto("invite-team", "Invite a teammate", "Share the workspace with another engineer, QA member, or manager.", signals.HasInviteOrMember, "team"),
            new BetaChecklistItemDto("submit-feedback", "Send beta feedback", "Tell us what blocked, confused, or impressed your team.", signals.HasFeedback, "beta-feedback")
        };

        return Result<BetaChecklistDto>.Success(new BetaChecklistDto(items.Count(item => item.Completed), items.Length, items));
    }

    private async Task<Result<BetaFeedbackDto>?> ValidateScopeAsync(Guid organizationId, Guid? workspaceId, CancellationToken cancellationToken)
    {
        if (!await permissionService.IsOrganizationMemberAsync(CurrentUser!.UserId, organizationId, cancellationToken))
        {
            return Forbidden<BetaFeedbackDto>("organization.member");
        }

        if (workspaceId is null)
        {
            return null;
        }

        var workspaceOrganizationId = await workspaceRepository.GetOrganizationIdAsync(workspaceId.Value, cancellationToken);
        if (workspaceOrganizationId != organizationId)
        {
            return Result<BetaFeedbackDto>.Failure("Workspace does not belong to the selected organization.", new ErrorDetail("workspace.scope", "Workspace does not belong to the selected organization.", nameof(CreateBetaFeedbackRequest.WorkspaceId)));
        }

        return null;
    }

    private static Result<BetaFeedbackDto>? ValidateCreate(CreateBetaFeedbackRequest request)
    {
        if (!Categories.Contains(request.Category))
        {
            return Result<BetaFeedbackDto>.Failure("Invalid feedback category.", new ErrorDetail("feedback.category", "Category must be Bug, UX, Feature, Pricing, or Other.", nameof(request.Category)));
        }

        if (!Sentiments.Contains(request.Sentiment))
        {
            return Result<BetaFeedbackDto>.Failure("Invalid feedback sentiment.", new ErrorDetail("feedback.sentiment", "Sentiment must be Positive, Neutral, or Negative.", nameof(request.Sentiment)));
        }

        if (request.Rating is < 1 or > 5)
        {
            return Result<BetaFeedbackDto>.Failure("Invalid feedback rating.", new ErrorDetail("feedback.rating", "Rating must be between 1 and 5.", nameof(request.Rating)));
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 180)
        {
            return Result<BetaFeedbackDto>.Failure("Feedback title is required.", new ErrorDetail("feedback.title", "Title is required and must be 180 characters or fewer.", nameof(request.Title)));
        }

        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
        {
            return Result<BetaFeedbackDto>.Failure("Feedback message is required.", new ErrorDetail("feedback.message", "Message is required and must be 2000 characters or fewer.", nameof(request.Message)));
        }

        return null;
    }

    private static CreateBetaFeedbackRequest Normalize(CreateBetaFeedbackRequest request)
    {
        return request with
        {
            Category = NormalizeChoice(request.Category),
            Sentiment = NormalizeChoice(request.Sentiment),
            Title = TrimTo(request.Title, 180) ?? string.Empty,
            Message = TrimTo(request.Message, 2000) ?? string.Empty,
            Route = TrimTo(request.Route, 300),
            BrowserInfo = TrimTo(request.BrowserInfo, 500)
        };
    }

    private static string NormalizeChoice(string value)
    {
        var trimmed = value.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? trimmed
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private static string NormalizeStatus(string status)
    {
        return NormalizeChoice(status).Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string? TrimTo(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
