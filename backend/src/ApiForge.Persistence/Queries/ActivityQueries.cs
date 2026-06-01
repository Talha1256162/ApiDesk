namespace ApiForge.Persistence.Queries;

public static class ActivityQueries
{
    public const string InsertActivity = """
        insert into activityEvents
        (
            id, organizationId, workspaceId, actorUserId, actorName, actorEmail, eventType,
            entityType, entityId, entityName, action, status, severity, summary, metadataJson,
            ipAddress, userAgent, correlationId, createdOn, createdBy, isDeleted
        )
        values
        (
            newid(), @OrganizationId, @WorkspaceId, @ActorUserId, @ActorName, @ActorEmail, @EventType,
            @EntityType, @EntityId, @EntityName, @Action, @Status, @Severity, @Summary, @MetadataJson,
            @IpAddress, @UserAgent, @CorrelationId, sysutcdatetime(), @ActorUserId, 0
        );
        """;
}
