namespace ApiForge.Persistence.Queries;

public static class AuthQueries
{
    public const string GetUserByEmail = """
        select id, email, fullName, avatarUrl, timeZone
        from users
        where lower(email) = lower(@Email) and isDeleted = 0;
        """;

    public const string GetUserById = """
        select id, email, fullName, avatarUrl, timeZone
        from users
        where id = @UserId and isDeleted = 0;
        """;

    public const string GetPasswordHash = """
        select passwordHash
        from users
        where id = @UserId and isDeleted = 0;
        """;

    public const string GetDefaultTenant = """
        select top 1
            om.organizationId,
            wm.workspaceId
        from organizationMembers om
        outer apply (
            select top 1 workspaceId
            from workspaceMembers
            where userId = om.userId and organizationId = om.organizationId and status = 'Active' and isDeleted = 0
            order by createdOn
        ) wm
        where om.userId = @UserId
            and om.status = 'Active'
            and om.isDeleted = 0
            and (@OrganizationId is null or om.organizationId = @OrganizationId)
            and (@WorkspaceId is null or wm.workspaceId = @WorkspaceId)
        order by om.createdOn;
        """;

    public const string GetRefreshTokenContext = """
        select top 1
            rt.userId,
            om.organizationId,
            wm.workspaceId
        from refreshTokens rt
        join organizationMembers om on om.userId = rt.userId and om.status = 'Active' and om.isDeleted = 0
        outer apply (
            select top 1 workspaceId
            from workspaceMembers
            where userId = rt.userId and organizationId = om.organizationId and status = 'Active' and isDeleted = 0
            order by createdOn
        ) wm
        where rt.tokenHash = @TokenHash
            and rt.revokedOn is null
            and rt.expiresOn > sysutcdatetime()
            and rt.isDeleted = 0
        order by rt.createdOn desc;
        """;
}
