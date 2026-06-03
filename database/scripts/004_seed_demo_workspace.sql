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
declare @managerRole uniqueidentifier = (select top 1 id from dbo.roles where name = 'Manager' and isDeleted = 0);
declare @leadRole uniqueidentifier = (select top 1 id from dbo.roles where name = 'Team Lead' and isDeleted = 0);
declare @developerRole uniqueidentifier = (select top 1 id from dbo.roles where name = 'Developer' and isDeleted = 0);
declare @qaRole uniqueidentifier = (select top 1 id from dbo.roles where name = 'QA' and isDeleted = 0);
declare @managerUser uniqueidentifier = '40000000-0000-0000-0000-000000000002';
declare @leadUser uniqueidentifier = '40000000-0000-0000-0000-000000000003';
declare @qaUser uniqueidentifier = '40000000-0000-0000-0000-000000000004';
declare @developerUser uniqueidentifier = '40000000-0000-0000-0000-000000000005';
declare @billingFolder uniqueidentifier = '71000000-0000-0000-0000-000000000002';
declare @customerFolder uniqueidentifier = '71000000-0000-0000-0000-000000000003';
declare @loginRequest uniqueidentifier = '72000000-0000-0000-0000-000000000002';
declare @profileRequest uniqueidentifier = '72000000-0000-0000-0000-000000000003';
declare @invoiceRequest uniqueidentifier = '72000000-0000-0000-0000-000000000004';

if not exists (select 1 from dbo.users where email = 'admin@apiforge.local')
begin
    insert into dbo.users (id, email, passwordHash, fullName, avatarUrl, timeZone, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
    values (@adminUser, 'admin@apiforge.local', '$2a$12$SAhWOzEhuv8l9hrf6KYQLuyt6cknInKNE1Iarzv6K8rDbqAFhtHgS', 'API DESK Admin', null, 'UTC', sysutcdatetime(), sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.organizations where id = @org)
begin
    insert into dbo.organizations (id, name, slug, productName, retentionDays, createdOn, createdBy, isDeleted, versionNumber)
    values (@org, 'Northstar Software House', 'northstar-software-house', 'API DESK', 365, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.organizationMembers where organizationId = @org and userId = @adminUser)
begin
    insert into dbo.organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
    values (newid(), @org, @adminUser, @ownerRole, 'Active', @adminUser, sysutcdatetime(), sysutcdatetime(), sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.users where id = @managerUser)
begin
    insert into dbo.users (id, email, passwordHash, fullName, avatarUrl, timeZone, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
    values
    (@managerUser, 'manager@apiforge.local', '$2a$12$SAhWOzEhuv8l9hrf6KYQLuyt6cknInKNE1Iarzv6K8rDbqAFhtHgS', 'Maira Khan', null, 'Asia/Karachi', dateadd(hour, -2, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1),
    (@leadUser, 'lead@apiforge.local', '$2a$12$SAhWOzEhuv8l9hrf6KYQLuyt6cknInKNE1Iarzv6K8rDbqAFhtHgS', 'Talha Ahmed', null, 'Asia/Karachi', dateadd(minute, -34, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1),
    (@qaUser, 'qa@apiforge.local', '$2a$12$SAhWOzEhuv8l9hrf6KYQLuyt6cknInKNE1Iarzv6K8rDbqAFhtHgS', 'Sara QA Lead', null, 'Asia/Karachi', dateadd(minute, -18, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1),
    (@developerUser, 'dev@apiforge.local', '$2a$12$SAhWOzEhuv8l9hrf6KYQLuyt6cknInKNE1Iarzv6K8rDbqAFhtHgS', 'Hamza Backend', null, 'Asia/Karachi', dateadd(minute, -7, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.organizationMembers where organizationId = @org and userId = @managerUser)
begin
    insert into dbo.organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, lastActiveOn, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @org, @managerUser, @managerRole, 'Active', @adminUser, sysutcdatetime(), dateadd(hour, -2, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @leadUser, @leadRole, 'Active', @adminUser, sysutcdatetime(), dateadd(minute, -34, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @qaUser, @qaRole, 'Active', @adminUser, sysutcdatetime(), dateadd(minute, -18, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @developerUser, @developerRole, 'Active', @adminUser, sysutcdatetime(), dateadd(minute, -7, sysutcdatetime()), sysutcdatetime(), @adminUser, 0, 1);
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

if not exists (select 1 from dbo.workspaceMembers where workspaceId = @workspace and userId = @managerUser)
begin
    insert into dbo.workspaceMembers (id, organizationId, workspaceId, userId, roleId, status, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @org, @workspace, @managerUser, @managerRole, 'Active', sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @workspace, @leadUser, @leadRole, 'Active', sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @workspace, @qaUser, @qaRole, 'Active', sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @workspace, @developerUser, @developerRole, 'Active', sysutcdatetime(), @adminUser, 0, 1);
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

if not exists (select 1 from dbo.folders where id = @billingFolder)
begin
    insert into dbo.folders (id, organizationId, workspaceId, collectionId, parentFolderId, name, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
    values
    (@billingFolder, @org, @workspace, @collection, null, 'Billing', 2, sysutcdatetime(), @adminUser, 0, 1),
    (@customerFolder, @org, @workspace, @collection, null, 'Customers', 3, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.requests where id = @request)
begin
    insert into dbo.requests
    (id, organizationId, workspaceId, collectionId, folderId, name, description, method, url, authType, authConfigJson, bodyType, preRequestScript, testScript, timeoutMs, followRedirects, sslVerification, ownerUserId, lastModifiedByUserId, createdOn, createdBy, isDeleted, versionNumber)
    values
    (@request, @org, @workspace, @collection, @folder, 'Health Check', 'Checks the local API health surface.', 'GET', '{{base_url}}/swagger/v1/swagger.json', null, null, 'none', null, null, 30000, 1, 1, @adminUser, @adminUser, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.requests where id = @loginRequest)
begin
    insert into dbo.requests
    (id, organizationId, workspaceId, collectionId, folderId, name, description, method, url, authType, authConfigJson, bodyType, preRequestScript, testScript, timeoutMs, followRedirects, sslVerification, ownerUserId, lastModifiedByUserId, createdOn, createdBy, isDeleted, versionNumber)
    values
    (@loginRequest, @org, @workspace, @collection, @folder, 'Login with email', 'Issues an access token for API DESK users.', 'POST', '{{base_url}}/api/auth/login', null, null, 'rawJson', null, null, 30000, 1, 1, @leadUser, @leadUser, sysutcdatetime(), @leadUser, 0, 1),
    (@profileRequest, @org, @workspace, @collection, @customerFolder, 'Customer profile lookup', 'Loads customer profile by username.', 'GET', '{{base_url}}/api/customers/{{username}}', 'Bearer', '{"token":"{{access_token}}"}', 'none', null, null, 30000, 1, 1, @developerUser, @developerUser, sysutcdatetime(), @developerUser, 0, 1),
    (@invoiceRequest, @org, @workspace, @collection, @billingFolder, 'Create invoice draft', 'Creates a draft invoice for QA validation.', 'POST', '{{base_url}}/api/billing/invoices', 'ApiKey', '{"name":"X-API-Key","value":"{{api_key}}","location":"header"}', 'rawJson', null, null, 30000, 1, 1, @qaUser, @qaUser, sysutcdatetime(), @qaUser, 0, 1);
end

if not exists (select 1 from dbo.requestBodies where requestId = @loginRequest)
begin
    insert into dbo.requestBodies (id, requestId, bodyType, content, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @loginRequest, 'rawJson', '{ "email": "admin@apiforge.local", "password": "Password123!" }', sysutcdatetime(), @leadUser, 0, 1),
    (newid(), @invoiceRequest, 'rawJson', '{ "customerId": "{{customer_id}}", "currency": "USD", "lineItems": [{ "name": "Platform subscription", "amount": 149 }] }', sysutcdatetime(), @qaUser, 0, 1);
end

if not exists (select 1 from dbo.requestHeaders where requestId = @profileRequest)
begin
    insert into dbo.requestHeaders (id, requestId, [key], [value], enabled, isSecret, sortOrder, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @profileRequest, 'Authorization', 'Bearer {{access_token}}', 1, 1, 1, sysutcdatetime(), @developerUser, 0, 1),
    (newid(), @invoiceRequest, 'X-API-Key', '{{api_key}}', 1, 1, 1, sysutcdatetime(), @qaUser, 0, 1),
    (newid(), @invoiceRequest, 'Content-Type', 'application/json', 1, 0, 2, sysutcdatetime(), @qaUser, 0, 1);
end

if not exists (select 1 from dbo.requestExamples where requestId = @profileRequest)
begin
    insert into dbo.requestExamples (id, requestId, name, statusCode, headersJson, body, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @profileRequest, 'Customer profile - active', 200, '{"content-type":["application/json"]}', '{"id":"cus_1001","name":"Northstar Client","status":"active","plan":"Business"}', sysutcdatetime(), @developerUser, 0, 1);
end

if not exists (select 1 from dbo.environments where id = @environment)
begin
    insert into dbo.environments (id, organizationId, workspaceId, name, isDefault, createdOn, createdBy, isDeleted, versionNumber)
    values (@environment, @org, @workspace, 'Local', 1, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.environmentVariables where environmentId = @environment and [key] = 'base_url')
begin
    insert into dbo.environmentVariables (id, organizationId, workspaceId, collectionId, environmentId, userId, [key], [value], scope, isSecret, enabled, createdOn, createdBy, isDeleted, versionNumber)
    values (newid(), @org, @workspace, null, @environment, null, 'base_url', 'https://apidesk.tryasp.net', 'Environment', 0, 1, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.environmentVariables where environmentId = @environment and [key] = 'access_token')
begin
    insert into dbo.environmentVariables (id, organizationId, workspaceId, collectionId, environmentId, userId, [key], [value], scope, isSecret, enabled, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @org, @workspace, null, @environment, null, 'access_token', 'demo-token-value', 'Environment', 1, 1, sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @workspace, null, @environment, null, 'api_key', 'demo-api-key-value', 'Environment', 1, 1, sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @workspace, null, @environment, null, 'username', 'northstar.user', 'Environment', 0, 1, sysutcdatetime(), @adminUser, 0, 1),
    (newid(), @org, @workspace, null, @environment, null, 'customer_id', 'cus_1001', 'Environment', 0, 1, sysutcdatetime(), @adminUser, 0, 1);
end

if not exists (select 1 from dbo.activityEvents where organizationId = @org and eventType = 'DemoWorkspaceSeeded')
begin
    insert into dbo.activityEvents
    (id, organizationId, workspaceId, actorUserId, actorName, actorEmail, eventType, entityType, entityId, entityName, action, status, severity, summary, metadataJson, ipAddress, userAgent, correlationId, createdOn, createdBy, isDeleted, versionNumber)
    values
    (newid(), @org, @workspace, @leadUser, 'Talha Ahmed', 'lead@apiforge.local', 'RequestUpdated', 'Request', @loginRequest, 'Login with email', 'Update', 'Success', 'Info', 'Updated auth request body and test notes.', '{}', null, 'seed', newid(), dateadd(minute, -42, sysutcdatetime()), @leadUser, 0, 1),
    (newid(), @org, @workspace, @qaUser, 'Sara QA Lead', 'qa@apiforge.local', 'RequestSent', 'Request', @invoiceRequest, 'Create invoice draft', 'Run', 'Success', 'Info', 'Sent invoice draft request against Local environment.', '{}', null, 'seed', newid(), dateadd(minute, -23, sysutcdatetime()), @qaUser, 0, 1),
    (newid(), @org, @workspace, @managerUser, 'Maira Khan', 'manager@apiforge.local', 'DemoWorkspaceSeeded', 'Workspace', @workspace, 'Platform APIs', 'Create', 'Success', 'Info', 'Demo workspace initialized for sales walkthrough.', '{}', null, 'seed', newid(), dateadd(minute, -12, sysutcdatetime()), @managerUser, 0, 1);
end
go
