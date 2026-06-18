using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Persistence.Connection;
using Dapper;

namespace ApiForge.Persistence.Repositories;

public sealed class EmailDeliveryRepository(ISqlConnectionFactory connectionFactory) : IEmailDeliveryRepository
{
    public async Task LogInvitationEmailAsync(
        Guid invitationId,
        Guid organizationId,
        Guid? workspaceId,
        string recipientEmail,
        string subject,
        string provider,
        string status,
        string? errorMessage,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into emailDeliveryLogs
                (id, organizationId, workspaceId, invitationId, recipientEmail, subject, provider, status, errorMessage, sentOn, createdOn, createdBy, isDeleted, versionNumber)
            values
                (newid(), @OrganizationId, @WorkspaceId, @InvitationId, @RecipientEmail, @Subject, @Provider, @Status, @ErrorMessage,
                 case when @Status = 'Sent' then sysutcdatetime() else null end, sysutcdatetime(), @ActorUserId, 0, 1);
            """,
            new
            {
                InvitationId = invitationId,
                OrganizationId = organizationId,
                WorkspaceId = workspaceId,
                RecipientEmail = recipientEmail,
                Subject = subject,
                Provider = provider,
                Status = status,
                ErrorMessage = errorMessage,
                ActorUserId = actorUserId
            },
            cancellationToken: cancellationToken));
    }
}
