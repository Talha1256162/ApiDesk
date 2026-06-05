namespace ApiForge.Persistence.Queries;

public static class RbacQueries
{
    public const string HasPermission = """
        select cast(case when exists (
            select 1
            from organizationMembers om
            join roles r on r.id = om.roleId and r.isDeleted = 0
            join rolePermissions rp on rp.roleId = r.id and rp.isDeleted = 0
            join permissions p on p.id = rp.permissionId and p.isDeleted = 0
            where om.userId = @UserId
                and om.organizationId = @OrganizationId
                and om.status = 'Active'
                and om.isDeleted = 0
                and p.[key] = @PermissionKey
        ) or exists (
            select 1
            from workspaceMembers wm
            join organizationMembers om on om.organizationId = wm.organizationId
                and om.userId = wm.userId
                and om.status = 'Active'
                and om.isDeleted = 0
            join roles r on r.id = wm.roleId and r.isDeleted = 0
            join rolePermissions rp on rp.roleId = r.id and rp.isDeleted = 0
            join permissions p on p.id = rp.permissionId and p.isDeleted = 0
            where wm.userId = @UserId
                and wm.organizationId = @OrganizationId
                and (@WorkspaceId is null or wm.workspaceId = @WorkspaceId)
                and wm.status = 'Active'
                and wm.isDeleted = 0
                and p.[key] = @PermissionKey
        ) then 1 else 0 end as bit);
        """;

    public const string IsOrganizationMember = """
        select cast(case when exists (
            select 1
            from organizationMembers om
            where om.userId = @UserId
                and om.organizationId = @OrganizationId
                and om.status = 'Active'
                and om.isDeleted = 0
        ) then 1 else 0 end as bit);
        """;

    public const string IsWorkspaceMember = """
        select cast(case when exists (
            select 1
            from workspaceMembers wm
            join organizationMembers om on om.organizationId = wm.organizationId
                and om.userId = wm.userId
                and om.status = 'Active'
                and om.isDeleted = 0
            where wm.userId = @UserId
                and wm.organizationId = @OrganizationId
                and wm.workspaceId = @WorkspaceId
                and wm.status = 'Active'
                and wm.isDeleted = 0
        ) then 1 else 0 end as bit);
        """;
}
