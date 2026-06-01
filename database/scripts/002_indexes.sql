use ApiForgePro;
go

set quoted_identifier on;
set ansi_nulls on;
set ansi_padding on;
set ansi_warnings on;
set arithabort on;
set concat_null_yields_null on;
set numeric_roundabort off;
go

if not exists (select 1 from sys.indexes where name = 'ux_users_email')
    create unique index ux_users_email on dbo.users(email) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_organizationMembers_org_user')
    create index ix_organizationMembers_org_user on dbo.organizationMembers(organizationId, userId, status) include(roleId, createdOn) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_workspaceMembers_workspace_user')
    create index ix_workspaceMembers_workspace_user on dbo.workspaceMembers(workspaceId, userId, status) include(roleId, organizationId) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_workspaces_org')
    create index ix_workspaces_org on dbo.workspaces(organizationId, createdOn) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_collections_workspace')
    create index ix_collections_workspace on dbo.collections(workspaceId, createdOn) include(name, ownerUserId) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_folders_collection')
    create index ix_folders_collection on dbo.folders(collectionId, sortOrder) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_requests_collection')
    create index ix_requests_collection on dbo.requests(collectionId, folderId, modifiedOn) include(method, url, name) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_requestRuns_workspace_created')
    create index ix_requestRuns_workspace_created on dbo.requestRuns(workspaceId, createdOn) include(requestId, statusCode, succeeded, elapsedMs, environmentId, userId) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_activityEvents_org_workspace_created')
    create index ix_activityEvents_org_workspace_created on dbo.activityEvents(organizationId, workspaceId, createdOn desc) include(actorUserId, eventType, status, entityType, entityId) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_activityEvents_event_user')
    create index ix_activityEvents_event_user on dbo.activityEvents(eventType, actorUserId, createdOn desc) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_auditLogs_org_created')
    create index ix_auditLogs_org_created on dbo.auditLogs(organizationId, workspaceId, createdOn desc) include(actorUserId, eventType, entityType) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_environmentVariables_resolution')
    create index ix_environmentVariables_resolution on dbo.environmentVariables(workspaceId, collectionId, environmentId, userId, scope) include([key], [value], enabled, isSecret) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ux_permissions_key')
    create unique index ux_permissions_key on dbo.permissions([key]) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ux_roles_name_scope')
    create unique index ux_roles_name_scope on dbo.roles(name, scope) where isDeleted = 0;
go
