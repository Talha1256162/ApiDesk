use ApiForgePro;
go

if col_length('dbo.invitations', 'workspaceId') is null
begin
    alter table dbo.invitations add workspaceId uniqueidentifier null;
end
go

if object_id(N'dbo.emailDeliveryLogs', N'U') is null
begin
    create table dbo.emailDeliveryLogs
    (
        id uniqueidentifier not null constraint pk_emailDeliveryLogs primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        invitationId uniqueidentifier null,
        recipientEmail nvarchar(320) not null,
        subject nvarchar(300) not null,
        provider nvarchar(80) not null,
        status nvarchar(40) not null,
        errorMessage nvarchar(1000) null,
        sentOn datetime2 null,
        createdOn datetime2 not null constraint df_emailDeliveryLogs_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_emailDeliveryLogs_isDeleted default (0),
        versionNumber int not null constraint df_emailDeliveryLogs_version default (1)
    );
end
go

if not exists (select 1 from sys.indexes where name = 'ix_emailDeliveryLogs_invitation')
    create index ix_emailDeliveryLogs_invitation on dbo.emailDeliveryLogs(invitationId, createdOn desc) include(status, provider) where isDeleted = 0;
go

if not exists (select 1 from sys.indexes where name = 'ix_emailDeliveryLogs_org_workspace')
    create index ix_emailDeliveryLogs_org_workspace on dbo.emailDeliveryLogs(organizationId, workspaceId, createdOn desc) include(recipientEmail, status) where isDeleted = 0;
go
