use ApiForgePro;
go

declare @adminUser uniqueidentifier = '40000000-0000-0000-0000-000000000001';
declare @org uniqueidentifier = '50000000-0000-0000-0000-000000000001';
declare @workspace uniqueidentifier = '60000000-0000-0000-0000-000000000001';
declare @collection uniqueidentifier = '70000000-0000-0000-0000-000000000001';
declare @folder uniqueidentifier = '71000000-0000-0000-0000-000000000001';
declare @request uniqueidentifier = '72000000-0000-0000-0000-000000000001';
declare @environment uniqueidentifier = '73000000-0000-0000-0000-000000000001';
declare @ownerRole uniqueidentifier = (select top 1 id from dbo.roles where name = 'Owner' and isDeleted = 0);

if not exists (select 1 from dbo.users where email = 'admin@apiforge.local')
begin
    insert into dbo.users (id, email, passwordHash, fullName, avatarUrl, timeZone, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
    values (@adminUser, 'admin@apiforge.local', '$2a$12$SAhWOzEhuv8l9hrf6KYQLuyt6cknInKNE1Iarzv6K8rDbqAFhtHgS', 'ApiForge Admin', null, 'UTC', sysutcdatetime(), sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.organizations where id = @org)
begin
    insert into dbo.organizations (id, name, slug, productName, retentionDays, createdOn, createdBy, isDeleted, versionNumber)
    values (@org, 'Northstar Software House', 'northstar-software-house', 'ApiForge Pro', 365, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.organizationMembers where organizationId = @org and userId = @adminUser)
begin
    insert into dbo.organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
    values (newid(), @org, @adminUser, @ownerRole, 'Active', @adminUser, sysutcdatetime(), sysutcdatetime(), sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.workspaces where id = @workspace)
begin
    insert into dbo.workspaces (id, organizationId, name, slug, type, description, allowBodyLogging, createdOn, createdBy, isDeleted, versionNumber)
    values (@workspace, @org, 'Platform APIs', 'platform-apis', 'Team', 'Shared backend and QA API workspace.', 0, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.workspaceMembers where workspaceId = @workspace and userId = @adminUser)
begin
    insert into dbo.workspaceMembers (id, organizationId, workspaceId, userId, roleId, status, createdOn, createdBy, isDeleted, versionNumber)
    values (newid(), @org, @workspace, @adminUser, @ownerRole, 'Active', sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.collections where id = @collection)
begin
    insert into dbo.collections (id, organizationId, workspaceId, name, description, ownerUserId, createdOn, createdBy, isDeleted, versionNumber)
    values (@collection, @org, @workspace, 'Identity Service', 'Authentication and tenant setup APIs.', @adminUser, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.folders where id = @folder)
begin
    insert into dbo.folders (id, organizationId, workspaceId, collectionId, parentFolderId, name, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
    values (@folder, @org, @workspace, @collection, null, 'Auth', 1, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.requests where id = @request)
begin
    insert into dbo.requests
    (id, organizationId, workspaceId, collectionId, folderId, name, description, method, url, authType, authConfigJson, bodyType, preRequestScript, testScript, timeoutMs, followRedirects, sslVerification, ownerUserId, lastModifiedByUserId, createdOn, createdBy, isDeleted, versionNumber)
    values
    (@request, @org, @workspace, @collection, @folder, 'Health Check', 'Checks the local API health surface.', 'GET', '{{base_url}}/swagger/v1/swagger.json', null, null, 'none', null, null, 30000, 1, 1, @adminUser, @adminUser, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.environments where id = @environment)
begin
    insert into dbo.environments (id, organizationId, workspaceId, name, isDefault, createdOn, createdBy, isDeleted, versionNumber)
    values (@environment, @org, @workspace, 'Local', 1, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.environmentVariables where environmentId = @environment and [key] = 'base_url')
begin
    insert into dbo.environmentVariables (id, organizationId, workspaceId, collectionId, environmentId, userId, [key], [value], scope, isSecret, enabled, createdOn, createdBy, isDeleted, versionNumber)
    values (newid(), @org, @workspace, null, @environment, null, 'base_url', 'http://localhost:5108', 'Environment', 0, 1, sysutcdatetime(), @adminUser, 0, 1);
end
go
