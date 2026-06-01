namespace ApiForge.Domain.Constants;

public static class PermissionKeys
{
    public const string ManageOrganization = "organization.manage";
    public const string ManageBillingSettings = "billing.settings.manage";
    public const string InviteMembers = "members.invite";
    public const string CreateWorkspace = "workspace.create";
    public const string EditWorkspace = "workspace.edit";
    public const string DeleteWorkspace = "workspace.delete";
    public const string CreateCollection = "collection.create";
    public const string EditCollection = "collection.edit";
    public const string DeleteCollection = "collection.delete";
    public const string RunRequests = "request.run";
    public const string ViewRequestHistory = "request.history.view";
    public const string ViewTeamActivity = "activity.team.view";
    public const string ViewAuditLogs = "audit.view";
    public const string ManageEnvironments = "environment.manage";
    public const string ViewSecrets = "secret.view";
    public const string EditSecrets = "secret.edit";
    public const string ExportCollections = "collection.export";
    public const string ImportCollections = "collection.import";
    public const string ManageMockServers = "mock.manage";
    public const string ManageMonitors = "monitor.manage";
    public const string ApproveApiChanges = "api.approve";

    public static readonly string[] All =
    [
        ManageOrganization,
        ManageBillingSettings,
        InviteMembers,
        CreateWorkspace,
        EditWorkspace,
        DeleteWorkspace,
        CreateCollection,
        EditCollection,
        DeleteCollection,
        RunRequests,
        ViewRequestHistory,
        ViewTeamActivity,
        ViewAuditLogs,
        ManageEnvironments,
        ViewSecrets,
        EditSecrets,
        ExportCollections,
        ImportCollections,
        ManageMockServers,
        ManageMonitors,
        ApproveApiChanges
    ];
}
