use ApiForgePro;
go

declare @systemUser uniqueidentifier = '00000000-0000-0000-0000-000000000001';

insert into dbo.roles (id, name, scope, isSystemRole, createdOn, createdBy, isDeleted, versionNumber)
select roleId, name, scope, 1, sysutcdatetime(), @systemUser, 0, 1
from (values
    ('10000000-0000-0000-0000-000000000001', 'Owner', 'Organization'),
    ('10000000-0000-0000-0000-000000000002', 'Admin', 'Organization'),
    ('10000000-0000-0000-0000-000000000003', 'Manager', 'Organization'),
    ('10000000-0000-0000-0000-000000000004', 'Team Lead', 'Workspace'),
    ('10000000-0000-0000-0000-000000000005', 'Developer', 'Workspace'),
    ('10000000-0000-0000-0000-000000000006', 'QA', 'Workspace'),
    ('10000000-0000-0000-0000-000000000007', 'Viewer', 'Workspace'),
    ('10000000-0000-0000-0000-000000000008', 'Guest', 'Workspace')
) seed(roleId, name, scope)
where not exists (select 1 from dbo.roles r where r.name = seed.name and r.scope = seed.scope and r.isDeleted = 0);

insert into dbo.permissions (id, [key], description, createdOn, createdBy, isDeleted, versionNumber)
select permissionId, [key], description, sysutcdatetime(), @systemUser, 0, 1
from (values
    ('20000000-0000-0000-0000-000000000001', 'organization.manage', 'Can manage organization'),
    ('20000000-0000-0000-0000-000000000002', 'billing.settings.manage', 'Can manage billing and settings'),
    ('20000000-0000-0000-0000-000000000003', 'members.invite', 'Can invite members'),
    ('20000000-0000-0000-0000-000000000004', 'workspace.create', 'Can create workspace'),
    ('20000000-0000-0000-0000-000000000005', 'workspace.edit', 'Can edit workspace'),
    ('20000000-0000-0000-0000-000000000006', 'workspace.delete', 'Can delete workspace'),
    ('20000000-0000-0000-0000-000000000007', 'collection.create', 'Can create collection'),
    ('20000000-0000-0000-0000-000000000008', 'collection.edit', 'Can edit collection'),
    ('20000000-0000-0000-0000-000000000009', 'collection.delete', 'Can delete collection'),
    ('20000000-0000-0000-0000-000000000010', 'request.run', 'Can run requests'),
    ('20000000-0000-0000-0000-000000000011', 'request.history.view', 'Can view request history'),
    ('20000000-0000-0000-0000-000000000012', 'activity.team.view', 'Can view team activity'),
    ('20000000-0000-0000-0000-000000000013', 'audit.view', 'Can view audit logs'),
    ('20000000-0000-0000-0000-000000000014', 'environment.manage', 'Can manage environments'),
    ('20000000-0000-0000-0000-000000000015', 'secret.view', 'Can view secrets'),
    ('20000000-0000-0000-0000-000000000016', 'secret.edit', 'Can edit secrets'),
    ('20000000-0000-0000-0000-000000000017', 'collection.export', 'Can export collections'),
    ('20000000-0000-0000-0000-000000000018', 'collection.import', 'Can import collections'),
    ('20000000-0000-0000-0000-000000000019', 'mock.manage', 'Can manage mock servers'),
    ('20000000-0000-0000-0000-000000000020', 'monitor.manage', 'Can manage monitors'),
    ('20000000-0000-0000-0000-000000000021', 'api.approve', 'Can approve API changes')
) seed(permissionId, [key], description)
where not exists (select 1 from dbo.permissions p where p.[key] = seed.[key] and p.isDeleted = 0);

insert into dbo.variableScopes (id, name, priority, createdOn, createdBy, isDeleted, versionNumber)
select id, name, priority, sysutcdatetime(), @systemUser, 0, 1
from (values
    ('30000000-0000-0000-0000-000000000001', 'Global', 1),
    ('30000000-0000-0000-0000-000000000002', 'Workspace', 2),
    ('30000000-0000-0000-0000-000000000003', 'Collection', 3),
    ('30000000-0000-0000-0000-000000000004', 'Environment', 4),
    ('30000000-0000-0000-0000-000000000005', 'LocalPrivate', 5)
) seed(id, name, priority)
where not exists (select 1 from dbo.variableScopes vs where vs.name = seed.name and vs.isDeleted = 0);

insert into dbo.rolePermissions (id, roleId, permissionId, createdOn, createdBy, isDeleted, versionNumber)
select newid(), r.id, p.id, sysutcdatetime(), @systemUser, 0, 1
from dbo.roles r
cross join dbo.permissions p
where r.name in ('Owner', 'Admin')
  and not exists (select 1 from dbo.rolePermissions rp where rp.roleId = r.id and rp.permissionId = p.id and rp.isDeleted = 0);

insert into dbo.rolePermissions (id, roleId, permissionId, createdOn, createdBy, isDeleted, versionNumber)
select newid(), r.id, p.id, sysutcdatetime(), @systemUser, 0, 1
from dbo.roles r
join dbo.permissions p on p.[key] in ('workspace.create','workspace.edit','collection.create','collection.edit','collection.delete','request.run','request.history.view','activity.team.view','environment.manage','collection.export','collection.import','mock.manage','monitor.manage','api.approve')
where r.name in ('Manager', 'Team Lead')
  and not exists (select 1 from dbo.rolePermissions rp where rp.roleId = r.id and rp.permissionId = p.id and rp.isDeleted = 0);

insert into dbo.rolePermissions (id, roleId, permissionId, createdOn, createdBy, isDeleted, versionNumber)
select newid(), r.id, p.id, sysutcdatetime(), @systemUser, 0, 1
from dbo.roles r
join dbo.permissions p on p.[key] in ('collection.create','collection.edit','request.run','request.history.view','environment.manage','collection.export','collection.import')
where r.name in ('Developer', 'QA')
  and not exists (select 1 from dbo.rolePermissions rp where rp.roleId = r.id and rp.permissionId = p.id and rp.isDeleted = 0);

insert into dbo.rolePermissions (id, roleId, permissionId, createdOn, createdBy, isDeleted, versionNumber)
select newid(), r.id, p.id, sysutcdatetime(), @systemUser, 0, 1
from dbo.roles r
join dbo.permissions p on p.[key] in ('request.history.view')
where r.name in ('Viewer', 'Guest')
  and not exists (select 1 from dbo.rolePermissions rp where rp.roleId = r.id and rp.permissionId = p.id and rp.isDeleted = 0);
go
