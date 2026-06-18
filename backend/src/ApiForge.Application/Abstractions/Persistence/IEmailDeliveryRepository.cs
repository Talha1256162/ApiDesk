namespace ApiForge.Application.Abstractions.Persistence;

public interface IEmailDeliveryRepository
{
    Task LogInvitationEmailAsync(
        Guid invitationId,
        Guid organizationId,
        Guid? workspaceId,
        string recipientEmail,
        string subject,
        string provider,
        string status,
        string? errorMessage,
        Guid actorUserId,
        CancellationToken cancellationToken);
}
