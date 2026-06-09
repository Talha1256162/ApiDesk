namespace ApiForge.Application.DTOs.BetaFeedback;

public sealed record CreateBetaFeedbackRequest(
    Guid OrganizationId,
    Guid? WorkspaceId,
    string Category,
    string Sentiment,
    int? Rating,
    string Title,
    string Message,
    string? Route,
    string? BrowserInfo);

public sealed record UpdateBetaFeedbackStatusRequest(
    string Status,
    string? AdminNotes);

public sealed record BetaFeedbackDto(
    Guid Id,
    Guid OrganizationId,
    Guid? WorkspaceId,
    Guid ActorUserId,
    string ActorName,
    string ActorEmail,
    string Category,
    string Sentiment,
    int? Rating,
    string Title,
    string Message,
    string? Route,
    string? BrowserInfo,
    string Status,
    string? AdminNotes,
    DateTime CreatedOn,
    DateTime? ModifiedOn,
    int VersionNumber);

public sealed record BetaChecklistDto(
    int CompletedCount,
    int TotalCount,
    IReadOnlyList<BetaChecklistItemDto> Items);

public sealed record BetaChecklistItemDto(
    string Key,
    string Label,
    string Description,
    bool Completed,
    string ActionView);

public sealed record BetaChecklistSignals(
    bool HasCollection,
    bool HasEnvironment,
    bool HasRequestRun,
    bool HasInviteOrMember,
    bool HasFeedback);
