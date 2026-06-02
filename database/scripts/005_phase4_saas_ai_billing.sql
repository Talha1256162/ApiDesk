if object_id(N'dbo.aiAssistantConfigs', N'U') is null
begin
    create table dbo.aiAssistantConfigs
    (
        id uniqueidentifier not null constraint pk_aiAssistantConfigs primary key,
        organizationId uniqueidentifier not null,
        provider nvarchar(80) not null,
        modelName nvarchar(160) null,
        endpointUrl nvarchar(500) null,
        deploymentName nvarchar(160) null,
        isEnabled bit not null constraint df_aiAssistantConfigs_isEnabled default (0),
        createdOn datetime2 not null constraint df_aiAssistantConfigs_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_aiAssistantConfigs_isDeleted default (0),
        versionNumber int not null constraint df_aiAssistantConfigs_version default (1)
    );
end
go

if object_id(N'dbo.billingPlans', N'U') is null
begin
    create table dbo.billingPlans
    (
        id uniqueidentifier not null constraint pk_billingPlans primary key,
        code nvarchar(80) not null,
        name nvarchar(160) not null,
        monthlyPrice decimal(18,2) not null,
        includedRequests int not null,
        includedMembers int null,
        featuresJson nvarchar(max) not null,
        isActive bit not null constraint df_billingPlans_isActive default (1),
        createdOn datetime2 not null constraint df_billingPlans_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_billingPlans_isDeleted default (0),
        versionNumber int not null constraint df_billingPlans_version default (1)
    );
end
go

if object_id(N'dbo.organizationSubscriptions', N'U') is null
begin
    create table dbo.organizationSubscriptions
    (
        id uniqueidentifier not null constraint pk_organizationSubscriptions primary key,
        organizationId uniqueidentifier not null,
        billingPlanId uniqueidentifier not null,
        status nvarchar(40) not null,
        currentPeriodStart datetime2 not null,
        currentPeriodEnd datetime2 not null,
        createdOn datetime2 not null constraint df_organizationSubscriptions_createdOn default sysutcdatetime(),
        createdBy uniqueidentifier not null,
        modifiedOn datetime2 null,
        modifiedBy uniqueidentifier null,
        isDeleted bit not null constraint df_organizationSubscriptions_isDeleted default (0),
        versionNumber int not null constraint df_organizationSubscriptions_version default (1)
    );
end
go

declare @systemUser uniqueidentifier = '00000000-0000-0000-0000-000000000001';

if not exists (select 1 from dbo.billingPlans where code = 'team' and isDeleted = 0)
begin
    insert into dbo.billingPlans (id, code, name, monthlyPrice, includedRequests, includedMembers, featuresJson, createdOn, createdBy, isDeleted, versionNumber)
    values
    ('91000000-0000-0000-0000-000000000001', 'team', 'Team', 49.00, 50000, null, '["Unlimited members","Team activity","Mock servers","Monitors"]', sysutcdatetime(), @systemUser, 0, 1),
    ('91000000-0000-0000-0000-000000000002', 'business', 'Business', 149.00, 250000, null, '["Governance","Published docs","Audit export","Advanced analytics"]', sysutcdatetime(), @systemUser, 0, 1),
    ('91000000-0000-0000-0000-000000000003', 'enterprise', 'Enterprise', 499.00, 1000000, null, '["SAML-ready structure","Custom retention","AI provider config","Priority support"]', sysutcdatetime(), @systemUser, 0, 1);
end
go

if not exists (select 1 from sys.indexes where name = 'ix_aiAssistantConfigs_org')
    create index ix_aiAssistantConfigs_org on dbo.aiAssistantConfigs(organizationId, isDeleted);
go

if not exists (select 1 from sys.indexes where name = 'ix_organizationSubscriptions_org')
    create index ix_organizationSubscriptions_org on dbo.organizationSubscriptions(organizationId, isDeleted);
go
