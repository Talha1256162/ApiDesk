set quoted_identifier on;
go

if object_id('dbo.betaFeedback', 'U') is null
begin
    create table dbo.betaFeedback
    (
        id uniqueidentifier not null constraint pk_betaFeedback primary key,
        organizationId uniqueidentifier not null,
        workspaceId uniqueidentifier null,
        actorUserId uniqueidentifier not null,
        actorName nvarchar(160) not null,
        actorEmail nvarchar(256) not null,
        category nvarchar(40) not null,
        sentiment nvarchar(30) not null,
        rating int null,
        title nvarchar(180) not null,
        message nvarchar(2000) not null,
        route nvarchar(300) null,
        browserInfo nvarchar(500) null,
        status nvarchar(30) not null constraint df_betaFeedback_status default ('New'),
        adminNotes nvarchar(1000) null,
        createdOn datetime2(7) not null constraint df_betaFeedback_createdOn default (sysutcdatetime()),
        createdBy uniqueidentifier null,
        modifiedOn datetime2(7) null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_betaFeedback_isDeleted default (0),
        versionNumber int not null constraint df_betaFeedback_versionNumber default (1),
        constraint fk_betaFeedback_organizations foreign key (organizationId) references dbo.organizations(id),
        constraint fk_betaFeedback_workspaces foreign key (workspaceId) references dbo.workspaces(id),
        constraint fk_betaFeedback_users foreign key (actorUserId) references dbo.users(id),
        constraint ck_betaFeedback_rating check (rating is null or rating between 1 and 5),
        constraint ck_betaFeedback_category check (category in ('Bug', 'UX', 'Feature', 'Pricing', 'Other')),
        constraint ck_betaFeedback_sentiment check (sentiment in ('Positive', 'Neutral', 'Negative')),
        constraint ck_betaFeedback_status check (status in ('New', 'Reviewed', 'Planned', 'Resolved', 'Closed'))
    );
end
go

if not exists (select 1 from sys.indexes where name = 'ix_betaFeedback_org_created' and object_id = object_id('dbo.betaFeedback'))
begin
    create index ix_betaFeedback_org_created
    on dbo.betaFeedback (organizationId, createdOn desc)
    include (workspaceId, actorUserId, category, status)
    where isDeleted = 0;
end
go

if not exists (select 1 from sys.indexes where name = 'ix_betaFeedback_workspace_status' and object_id = object_id('dbo.betaFeedback'))
begin
    create index ix_betaFeedback_workspace_status
    on dbo.betaFeedback (workspaceId, status, createdOn desc)
    where isDeleted = 0;
end
go

if not exists (select 1 from sys.indexes where name = 'ix_betaFeedback_actor_created' and object_id = object_id('dbo.betaFeedback'))
begin
    create index ix_betaFeedback_actor_created
    on dbo.betaFeedback (actorUserId, createdOn desc)
    where isDeleted = 0;
end
go
