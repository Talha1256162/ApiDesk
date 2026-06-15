update dbo.users
set fullName = 'Apeiron Admin',
    modifiedOn = sysutcdatetime(),
    modifiedBy = id,
    versionNumber = versionNumber + 1
where email = 'admin@apiforge.local'
  and fullName in ('API DESK Admin', 'API Desk Admin', 'ApiDesk Admin', 'ApiForge Admin');

update dbo.activityEvents
set actorName = 'Apeiron Admin'
where actorEmail = 'admin@apiforge.local'
  and actorName in ('API DESK Admin', 'API Desk Admin', 'ApiDesk Admin', 'ApiForge Admin');

update dbo.auditLogs
set actorName = 'Apeiron Admin'
where actorEmail = 'admin@apiforge.local'
  and actorName in ('API DESK Admin', 'API Desk Admin', 'ApiDesk Admin', 'ApiForge Admin');

update dbo.collections
set name = replace(name, 'API Desk', 'Apeiron'),
    modifiedOn = sysutcdatetime(),
    modifiedBy = coalesce(modifiedBy, createdBy),
    versionNumber = versionNumber + 1
where name like '%API Desk%';

update dbo.activityEvents
set entityName = replace(entityName, 'API Desk', 'Apeiron')
where entityName like '%API Desk%';

update dbo.auditLogs
set entityName = replace(entityName, 'API Desk', 'Apeiron')
where entityName like '%API Desk%';
