declare @systemUser uniqueidentifier = '00000000-0000-0000-0000-000000000001';
declare @editorRole uniqueidentifier = '10000000-0000-0000-0000-000000000009';

if not exists (select 1 from dbo.roles where name = 'Editor' and scope = 'Workspace' and isDeleted = 0)
begin
    insert into dbo.roles (id, name, scope, isSystemRole, createdOn, createdBy, isDeleted, versionNumber)
    values (@editorRole, 'Editor', 'Workspace', 1, sysutcdatetime(), @systemUser, 0, 1);
end
else
begin
    select @editorRole = id from dbo.roles where name = 'Editor' and scope = 'Workspace' and isDeleted = 0;
end

insert into dbo.rolePermissions (id, roleId, permissionId, createdOn, createdBy, isDeleted, versionNumber)
select newid(), @editorRole, p.id, sysutcdatetime(), @systemUser, 0, 1
from dbo.permissions p
where p.[key] in ('collection.create','collection.edit','collection.delete','request.run','request.history.view','environment.manage','collection.export','collection.import','mock.manage')
  and p.isDeleted = 0
  and not exists (
      select 1
      from dbo.rolePermissions rp
      where rp.roleId = @editorRole and rp.permissionId = p.id and rp.isDeleted = 0
  );
go
