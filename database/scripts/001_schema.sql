if db_id(N'ApiForgePro') is null
begin
    create database ApiForgePro;
end
go

use ApiForgePro;
go

if object_id(N'dbo.users', N'U') is null
begin
    create table dbo.users
    (
        id uniqueidentifier not null constraint pk_users primary key,
        email nvarchar(320) not null,
        passwordHash nvarchar(500) not null,
        fullName nvarchar(200) not null,
        avatarUrl nvarchar(1000) null,
        timeZone nvarchar(100) not null constraint df_users_timeZone default ('UTC'),
        lastActiveOn datetime2 null,
        createdOn datetime2 not null constraint df_users_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_users_isDeleted default (0),
        versionNumber int not null constraint df_users_version default (1)
    );
end
go

if object_id(N'dbo.organizations', N'U') is null
begin
    create table dbo.organizations
    (
        id uniqueidentifier not null constraint pk_organizations primary key,
        name nvarchar(200) not null,
        slug nvarchar(220) not null,
        productName nvarchar(120) not null constraint df_organizations_productName default ('Apeiron'),
        retentionDays int not null constraint df_organizations_retentionDays default (365),
        createdOn datetime2 not null constraint df_organizations_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_organizations_isDeleted default (0),
        versionNumber int not null constraint df_organizations_version default (1)
    );
end
go

if object_id(N'dbo.roles', N'U') is null
begin
    create table dbo.roles
    (
        id uniqueidentifier not null constraint pk_roles primary key,
        name nvarchar(80) not null,
        scope nvarchar(40) not null,
        isSystemRole bit not null constraint df_roles_isSystemRole default (1),
        createdOn datetime2 not null constraint df_roles_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_roles_isDeleted default (0),
        versionNumber int not null constraint df_roles_version default (1)
    );
end
go

if object_id(N'dbo.permissions', N'U') is null
begin
    create table dbo.permissions
    (
        id uniqueidentifier not null constraint pk_permissions primary key,
        [key] nvarchar(120) not null,
        description nvarchar(300) not null,
        createdOn datetime2 not null constraint df_permissions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_permissions_isDeleted default (0),
        versionNumber int not null constraint df_permissions_version default (1)
    );
end
go

if object_id(N'dbo.rolePermissions', N'U') is null
begin
    create table dbo.rolePermissions
    (
        id uniqueidentifier not null constraint pk_rolePermissions primary key,
        roleId uniqueidentifier not null,
        permissionId uniqueidentifier not null,
        createdOn datetime2 not null constraint df_rolePermissions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_rolePermissions_isDeleted default (0),
        versionNumber int not null constraint df_rolePermissions_version default (1)
    );
end
go

if object_id(N'dbo.organizationMembers', N'U') is null
begin
    create table dbo.organizationMembers
    (
        id uniqueidentifier not null constraint pk_organizationMembers primary key,
        organizationId uniqueidentifier not null,
        userId uniqueidentifier not null,
        roleId uniqueidentifier not null,
        status nvarchar(40) not null,
        invitedByUserId uniqueidentifier null,
        joinedOn datetime2 null,
        lastActiveOn datetime2 null,
        createdOn datetime2 not null constraint df_organizationMembers_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_organizationMembers_isDeleted default (0),
        versionNumber int not null constraint df_organizationMembers_version default (1)
    );
end
go

if object_id(N'dbo.workspaces', N'U') is null
begin
    create table dbo.workspaces
    (
        id uniqueidentifier not null constraint pk_workspaces primary key,
        organizationId uniqueidentifier not null,
        name nvarchar(200) not null,
        slug nvarchar(220) not null,
        type nvarchar(60) not null,
        description nvarchar(1000) null,
        allowBodyLogging bit not null constraint df_workspaces_allowBodyLogging default (0),
        createdOn datetime2 not null constraint df_workspaces_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_workspaces_isDeleted default (0),
        versionNumber int not null constraint df_workspaces_version default (1)
    );
end
go

if object_id(N'dbo.workspaceMembers', N'U') is null
begin
    create table dbo.workspaceMembers
    (
        id uniqueidentifier not null constraint pk_workspaceMembers primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        userId uniqueidentifier not null,
        roleId uniqueidentifier not null,
        status nvarchar(40) not null,
        createdOn datetime2 not null constraint df_workspaceMembers_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_workspaceMembers_isDeleted default (0),
        versionNumber int not null constraint df_workspaceMembers_version default (1)
    );
end
go

if object_id(N'dbo.collections', N'U') is null
begin
    create table dbo.collections
    (
        id uniqueidentifier not null constraint pk_collections primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        name nvarchar(200) not null,
        description nvarchar(max) null,
        ownerUserId uniqueidentifier not null,
        createdOn datetime2 not null constraint df_collections_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_collections_isDeleted default (0),
        versionNumber int not null constraint df_collections_version default (1)
    );
end
go

if object_id(N'dbo.folders', N'U') is null
begin
    create table dbo.folders
    (
        id uniqueidentifier not null constraint pk_folders primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier not null,
        parentFolderId uniqueidentifier null,
        name nvarchar(200) not null,
        sortOrder int not null constraint df_folders_sortOrder default (0),
        createdOn datetime2 not null constraint df_folders_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_folders_isDeleted default (0),
        versionNumber int not null constraint df_folders_version default (1)
    );
end
go

if object_id(N'dbo.requests', N'U') is null
begin
    create table dbo.requests
    (
        id uniqueidentifier not null constraint pk_requests primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier not null,
        folderId uniqueidentifier null,
        name nvarchar(200) not null,
        description nvarchar(max) null,
        method nvarchar(20) not null,
        url nvarchar(2000) not null,
        authType nvarchar(50) null,
        authConfigJson nvarchar(max) null,
        bodyType nvarchar(50) not null constraint df_requests_bodyType default ('none'),
        preRequestScript nvarchar(max) null,
        testScript nvarchar(max) null,
        timeoutMs int not null constraint df_requests_timeout default (30000),
        followRedirects bit not null constraint df_requests_followRedirects default (1),
        sslVerification bit not null constraint df_requests_sslVerification default (1),
        ownerUserId uniqueidentifier not null,
        lastModifiedByUserId uniqueidentifier not null,
        createdOn datetime2 not null constraint df_requests_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requests_isDeleted default (0),
        versionNumber int not null constraint df_requests_version default (1)
    );
end
go

if object_id(N'dbo.requestVersions', N'U') is null
begin
    create table dbo.requestVersions
    (
        id uniqueidentifier not null constraint pk_requestVersions primary key,
        requestId uniqueidentifier not null,
        versionNumber int not null,
        snapshotJson nvarchar(max) not null,
        createdOn datetime2 not null constraint df_requestVersions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestVersions_isDeleted default (0)
    );
end
go

if object_id(N'dbo.requestExamples', N'U') is null
begin
    create table dbo.requestExamples
    (
        id uniqueidentifier not null constraint pk_requestExamples primary key,
        requestId uniqueidentifier not null,
        name nvarchar(200) not null,
        statusCode int null,
        headersJson nvarchar(max) null,
        body nvarchar(max) null,
        createdOn datetime2 not null constraint df_requestExamples_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestExamples_isDeleted default (0),
        versionNumber int not null constraint df_requestExamples_version default (1)
    );
end
go

if object_id(N'dbo.requestHeaders', N'U') is null
begin
    create table dbo.requestHeaders
    (
        id uniqueidentifier not null constraint pk_requestHeaders primary key,
        requestId uniqueidentifier not null,
        [key] nvarchar(300) not null,
        [value] nvarchar(max) null,
        enabled bit not null constraint df_requestHeaders_enabled default (1),
        isSecret bit not null constraint df_requestHeaders_isSecret default (0),
        sortOrder int not null constraint df_requestHeaders_sortOrder default (0),
        createdOn datetime2 not null constraint df_requestHeaders_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestHeaders_isDeleted default (0),
        versionNumber int not null constraint df_requestHeaders_version default (1)
    );
end
go

if object_id(N'dbo.requestParams', N'U') is null
begin
    create table dbo.requestParams
    (
        id uniqueidentifier not null constraint pk_requestParams primary key,
        requestId uniqueidentifier not null,
        paramType nvarchar(20) not null,
        [key] nvarchar(300) not null,
        [value] nvarchar(max) null,
        enabled bit not null constraint df_requestParams_enabled default (1),
        isSecret bit not null constraint df_requestParams_isSecret default (0),
        sortOrder int not null constraint df_requestParams_sortOrder default (0),
        createdOn datetime2 not null constraint df_requestParams_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestParams_isDeleted default (0),
        versionNumber int not null constraint df_requestParams_version default (1)
    );
end
go

if object_id(N'dbo.requestBodies', N'U') is null
begin
    create table dbo.requestBodies
    (
        id uniqueidentifier not null constraint pk_requestBodies primary key,
        requestId uniqueidentifier not null,
        bodyType nvarchar(50) not null,
        content nvarchar(max) null,
        createdOn datetime2 not null constraint df_requestBodies_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestBodies_isDeleted default (0),
        versionNumber int not null constraint df_requestBodies_version default (1)
    );
end
go

if object_id(N'dbo.environments', N'U') is null
begin
    create table dbo.environments
    (
        id uniqueidentifier not null constraint pk_environments primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        name nvarchar(160) not null,
        isDefault bit not null constraint df_environments_isDefault default (0),
        createdOn datetime2 not null constraint df_environments_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_environments_isDeleted default (0),
        versionNumber int not null constraint df_environments_version default (1)
    );
end
go

if object_id(N'dbo.variableScopes', N'U') is null
begin
    create table dbo.variableScopes
    (
        id uniqueidentifier not null constraint pk_variableScopes primary key,
        name nvarchar(80) not null,
        priority int not null,
        createdOn datetime2 not null constraint df_variableScopes_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_variableScopes_isDeleted default (0),
        versionNumber int not null constraint df_variableScopes_version default (1)
    );
end
go

if object_id(N'dbo.environmentVariables', N'U') is null
begin
    create table dbo.environmentVariables
    (
        id uniqueidentifier not null constraint pk_environmentVariables primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier null,
        environmentId uniqueidentifier null,
        userId uniqueidentifier null,
        [key] nvarchar(300) not null,
        [value] nvarchar(max) null,
        scope nvarchar(80) not null,
        isSecret bit not null constraint df_environmentVariables_isSecret default (0),
        enabled bit not null constraint df_environmentVariables_enabled default (1),
        createdOn datetime2 not null constraint df_environmentVariables_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_environmentVariables_isDeleted default (0),
        versionNumber int not null constraint df_environmentVariables_version default (1)
    );
end
go

if object_id(N'dbo.environmentVersions', N'U') is null
begin
    create table dbo.environmentVersions
    (
        id uniqueidentifier not null constraint pk_environmentVersions primary key,
        environmentId uniqueidentifier not null,
        snapshotJson nvarchar(max) not null,
        createdOn datetime2 not null constraint df_environmentVersions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_environmentVersions_isDeleted default (0),
        versionNumber int not null constraint df_environmentVersions_version default (1)
    );
end
go

if object_id(N'dbo.requestRuns', N'U') is null
begin
    create table dbo.requestRuns
    (
        id uniqueidentifier not null constraint pk_requestRuns primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        requestId uniqueidentifier not null,
        environmentId uniqueidentifier null,
        userId uniqueidentifier not null,
        status nvarchar(40) not null,
        succeeded bit null,
        statusCode int null,
        elapsedMs bigint null,
        sizeBytes bigint null,
        errorMessage nvarchar(max) null,
        startedOn datetime2 not null,
        completedOn datetime2 null,
        createdOn datetime2 not null constraint df_requestRuns_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestRuns_isDeleted default (0),
        versionNumber int not null constraint df_requestRuns_version default (1)
    );
end
go

if object_id(N'dbo.requestRunResults', N'U') is null
begin
    create table dbo.requestRunResults
    (
        id uniqueidentifier not null constraint pk_requestRunResults primary key,
        requestRunId uniqueidentifier not null,
        statusCode int null,
        headersJson nvarchar(max) null,
        cookiesJson nvarchar(max) null,
        bodyPreview nvarchar(max) null,
        contentType nvarchar(300) null,
        createdOn datetime2 not null constraint df_requestRunResults_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_requestRunResults_isDeleted default (0),
        versionNumber int not null constraint df_requestRunResults_version default (1)
    );
end
go

if object_id(N'dbo.activityEvents', N'U') is null
begin
    create table dbo.activityEvents
    (
        id uniqueidentifier not null constraint pk_activityEvents primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        actorUserId uniqueidentifier not null,
        actorName nvarchar(200) not null,
        actorEmail nvarchar(320) not null,
        eventType nvarchar(120) not null,
        entityType nvarchar(120) not null,
        entityId uniqueidentifier null,
        entityName nvarchar(300) null,
        action nvarchar(120) not null,
        status nvarchar(40) not null,
        severity nvarchar(40) not null,
        summary nvarchar(1000) null,
        metadataJson nvarchar(max) null,
        ipAddress nvarchar(80) null,
        userAgent nvarchar(1000) null,
        correlationId nvarchar(100) not null,
        createdOn datetime2 not null constraint df_activityEvents_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_activityEvents_isDeleted default (0),
        versionNumber int not null constraint df_activityEvents_version default (1)
    );
end
go

if object_id(N'dbo.auditLogs', N'U') is null
begin
    create table dbo.auditLogs
    (
        id uniqueidentifier not null constraint pk_auditLogs primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        actorUserId uniqueidentifier not null,
        actorName nvarchar(200) not null,
        actorEmail nvarchar(320) not null,
        eventType nvarchar(120) not null,
        entityType nvarchar(120) not null,
        entityId uniqueidentifier null,
        entityName nvarchar(300) null,
        action nvarchar(120) not null,
        oldValueJson nvarchar(max) null,
        newValueJson nvarchar(max) null,
        ipAddress nvarchar(80) null,
        userAgent nvarchar(1000) null,
        severity nvarchar(40) not null,
        correlationId nvarchar(100) not null,
        createdOn datetime2 not null constraint df_auditLogs_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_auditLogs_isDeleted default (0),
        versionNumber int not null constraint df_auditLogs_version default (1)
    );
end
go

if object_id(N'dbo.invitations', N'U') is null
begin
    create table dbo.invitations
    (
        id uniqueidentifier not null constraint pk_invitations primary key,
        organizationId uniqueidentifier not null,
        email nvarchar(320) not null,
        roleId uniqueidentifier not null,
        status nvarchar(40) not null,
        message nvarchar(1000) null,
        tokenHash nvarchar(200) not null,
        expiresOn datetime2 not null,
        acceptedOn datetime2 null,
        createdOn datetime2 not null constraint df_invitations_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_invitations_isDeleted default (0),
        versionNumber int not null constraint df_invitations_version default (1)
    );
end
go

if object_id(N'dbo.refreshTokens', N'U') is null
begin
    create table dbo.refreshTokens
    (
        id uniqueidentifier not null constraint pk_refreshTokens primary key,
        userId uniqueidentifier not null,
        tokenHash nvarchar(200) not null,
        expiresOn datetime2 not null,
        revokedOn datetime2 null,
        ipAddress nvarchar(80) null,
        userAgent nvarchar(1000) null,
        createdOn datetime2 not null constraint df_refreshTokens_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_refreshTokens_isDeleted default (0),
        versionNumber int not null constraint df_refreshTokens_version default (1)
    );
end
go

if object_id(N'dbo.userSessions', N'U') is null
begin
    create table dbo.userSessions
    (
        id uniqueidentifier not null constraint pk_userSessions primary key,
        userId uniqueidentifier not null,
        organizationId uniqueidentifier null,
        workspaceId uniqueidentifier null,
        ipAddress nvarchar(80) null,
        userAgent nvarchar(1000) null,
        startedOn datetime2 not null,
        endedOn datetime2 null,
        createdOn datetime2 not null constraint df_userSessions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_userSessions_isDeleted default (0),
        versionNumber int not null constraint df_userSessions_version default (1)
    );
end
go

if object_id(N'dbo.testAssertions', N'U') is null
begin
    create table dbo.testAssertions
    (
        id uniqueidentifier not null constraint pk_testAssertions primary key,
        requestId uniqueidentifier not null,
        assertionType nvarchar(80) not null,
        expectedValue nvarchar(max) null,
        createdOn datetime2 not null constraint df_testAssertions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_testAssertions_isDeleted default (0),
        versionNumber int not null constraint df_testAssertions_version default (1)
    );
end
go

if object_id(N'dbo.testResults', N'U') is null
begin
    create table dbo.testResults
    (
        id uniqueidentifier not null constraint pk_testResults primary key,
        requestRunId uniqueidentifier not null,
        assertionId uniqueidentifier null,
        passed bit not null,
        message nvarchar(1000) null,
        createdOn datetime2 not null constraint df_testResults_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_testResults_isDeleted default (0),
        versionNumber int not null constraint df_testResults_version default (1)
    );
end
go

if object_id(N'dbo.mockServers', N'U') is null
begin
    create table dbo.mockServers
    (
        id uniqueidentifier not null constraint pk_mockServers primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier not null,
        name nvarchar(200) not null,
        slug nvarchar(220) not null,
        isPublic bit not null constraint df_mockServers_isPublic default (0),
        apiKeyRequired bit not null constraint df_mockServers_apiKeyRequired default (0),
        delayMs int not null constraint df_mockServers_delayMs default (0),
        createdOn datetime2 not null constraint df_mockServers_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_mockServers_isDeleted default (0),
        versionNumber int not null constraint df_mockServers_version default (1)
    );
end
go

if object_id(N'dbo.mockRoutes', N'U') is null
begin
    create table dbo.mockRoutes
    (
        id uniqueidentifier not null constraint pk_mockRoutes primary key,
        mockServerId uniqueidentifier not null,
        method nvarchar(20) not null,
        path nvarchar(1000) not null,
        requestExampleId uniqueidentifier null,
        createdOn datetime2 not null constraint df_mockRoutes_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_mockRoutes_isDeleted default (0),
        versionNumber int not null constraint df_mockRoutes_version default (1)
    );
end
go

if object_id(N'dbo.mockLogs', N'U') is null
begin
    create table dbo.mockLogs
    (
        id uniqueidentifier not null constraint pk_mockLogs primary key,
        mockServerId uniqueidentifier not null,
        mockRouteId uniqueidentifier null,
        method nvarchar(20) not null,
        path nvarchar(1000) not null,
        statusCode int not null,
        createdOn datetime2 not null constraint df_mockLogs_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_mockLogs_isDeleted default (0),
        versionNumber int not null constraint df_mockLogs_version default (1)
    );
end
go

if object_id(N'dbo.monitors', N'U') is null
begin
    create table dbo.monitors
    (
        id uniqueidentifier not null constraint pk_monitors primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier not null,
        environmentId uniqueidentifier null,
        name nvarchar(200) not null,
        scheduleExpression nvarchar(120) not null,
        isEnabled bit not null constraint df_monitors_isEnabled default (1),
        createdOn datetime2 not null constraint df_monitors_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_monitors_isDeleted default (0),
        versionNumber int not null constraint df_monitors_version default (1)
    );
end
go

if object_id(N'dbo.monitorRuns', N'U') is null
begin
    create table dbo.monitorRuns
    (
        id uniqueidentifier not null constraint pk_monitorRuns primary key,
        monitorId uniqueidentifier not null,
        status nvarchar(40) not null,
        passedCount int not null constraint df_monitorRuns_passed default (0),
        failedCount int not null constraint df_monitorRuns_failed default (0),
        latencyMs bigint null,
        createdOn datetime2 not null constraint df_monitorRuns_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_monitorRuns_isDeleted default (0),
        versionNumber int not null constraint df_monitorRuns_version default (1)
    );
end
go

if object_id(N'dbo.comments', N'U') is null
begin
    create table dbo.comments
    (
        id uniqueidentifier not null constraint pk_comments primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        entityType nvarchar(80) not null,
        entityId uniqueidentifier not null,
        body nvarchar(max) not null,
        createdOn datetime2 not null constraint df_comments_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_comments_isDeleted default (0),
        versionNumber int not null constraint df_comments_version default (1)
    );
end
go

if object_id(N'dbo.notifications', N'U') is null
begin
    create table dbo.notifications
    (
        id uniqueidentifier not null constraint pk_notifications primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        userId uniqueidentifier not null,
        type nvarchar(80) not null,
        title nvarchar(300) not null,
        body nvarchar(1000) null,
        readOn datetime2 null,
        createdOn datetime2 not null constraint df_notifications_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_notifications_isDeleted default (0),
        versionNumber int not null constraint df_notifications_version default (1)
    );
end
go

if object_id(N'dbo.apiSpecs', N'U') is null
begin
    create table dbo.apiSpecs
    (
        id uniqueidentifier not null constraint pk_apiSpecs primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier null,
        name nvarchar(200) not null,
        format nvarchar(40) not null,
        content nvarchar(max) not null,
        validationStatus nvarchar(40) not null,
        createdOn datetime2 not null constraint df_apiSpecs_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_apiSpecs_isDeleted default (0),
        versionNumber int not null constraint df_apiSpecs_version default (1)
    );
end
go

if object_id(N'dbo.publishedDocs', N'U') is null
begin
    create table dbo.publishedDocs
    (
        id uniqueidentifier not null constraint pk_publishedDocs primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier not null,
        slug nvarchar(220) not null,
        isPublic bit not null constraint df_publishedDocs_isPublic default (0),
        passwordHash nvarchar(500) null,
        brandJson nvarchar(max) null,
        publishedOn datetime2 null,
        createdOn datetime2 not null constraint df_publishedDocs_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_publishedDocs_isDeleted default (0),
        versionNumber int not null constraint df_publishedDocs_version default (1)
    );
end
go

if object_id(N'dbo.apiKeys', N'U') is null
begin
    create table dbo.apiKeys
    (
        id uniqueidentifier not null constraint pk_apiKeys primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        name nvarchar(200) not null,
        keyHash nvarchar(200) not null,
        expiresOn datetime2 null,
        lastUsedOn datetime2 null,
        createdOn datetime2 not null constraint df_apiKeys_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_apiKeys_isDeleted default (0),
        versionNumber int not null constraint df_apiKeys_version default (1)
    );
end
go

if object_id(N'dbo.collectionRuns', N'U') is null
begin
    create table dbo.collectionRuns
    (
        id uniqueidentifier not null constraint pk_collectionRuns primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        collectionId uniqueidentifier not null,
        environmentId uniqueidentifier null,
        status nvarchar(40) not null,
        createdOn datetime2 not null constraint df_collectionRuns_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_collectionRuns_isDeleted default (0),
        versionNumber int not null constraint df_collectionRuns_version default (1)
    );
end
go

if object_id(N'dbo.apiReviewItems', N'U') is null
begin
    create table dbo.apiReviewItems
    (
        id uniqueidentifier not null constraint pk_apiReviewItems primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier not null,
        requestId uniqueidentifier null,
        status nvarchar(40) not null,
        notes nvarchar(max) null,
        createdOn datetime2 not null constraint df_apiReviewItems_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_apiReviewItems_isDeleted default (0),
        versionNumber int not null constraint df_apiReviewItems_version default (1)
    );
end
go

if object_id(N'dbo.jsonSnippets', N'U') is null
begin
    create table dbo.jsonSnippets
    (
        id uniqueidentifier not null constraint pk_jsonSnippets primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        ownerUserId uniqueidentifier not null,
        name nvarchar(200) not null,
        content nvarchar(max) not null,
        visibility nvarchar(40) not null,
        createdOn datetime2 not null constraint df_jsonSnippets_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_jsonSnippets_isDeleted default (0),
        versionNumber int not null constraint df_jsonSnippets_version default (1)
    );
end
go

if object_id(N'dbo.jsonSnippetShares', N'U') is null
begin
    create table dbo.jsonSnippetShares
    (
        id uniqueidentifier not null constraint pk_jsonSnippetShares primary key,
        jsonSnippetId uniqueidentifier not null,
        userId uniqueidentifier null,
        workspaceId uniqueidentifier null,
        createdOn datetime2 not null constraint df_jsonSnippetShares_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_jsonSnippetShares_isDeleted default (0),
        versionNumber int not null constraint df_jsonSnippetShares_version default (1)
    );
end
go
