using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Organizations;
using ApiForge.Domain.Constants;
using ApiForge.Shared.Pagination;
using Dapper;
using ApiForge.Persistence.Connection;

namespace ApiForge.Persistence.Repositories;

public sealed class OrganizationRepository(ISqlConnectionFactory connectionFactory) : IOrganizationRepository
{
    public async Task<IReadOnlyList<OrganizationDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var organizations = await connection.QueryAsync<OrganizationDto>(new CommandDefinition("""
            select o.id, o.name, o.slug, o.productName, o.createdOn
            from organizations o
            join organizationMembers om on om.organizationId = o.id and om.userId = @UserId and om.status = 'Active' and om.isDeleted = 0
            where o.isDeleted = 0
            order by o.name;
            """, new { UserId = userId }, cancellationToken: cancellationToken));
        return organizations.AsList();
    }

    public async Task<OrganizationDto> CreateAsync(Guid userId, CreateOrganizationRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var organizationId = Guid.NewGuid();
        var slug = $"{RepositoryUtility.Slugify(request.Name)}-{organizationId.ToString("N")[..6]}";
        var ownerRoleId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "select top 1 id from roles where name = @Name and isDeleted = 0;",
            new { Name = RoleNames.Owner },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into organizations (id, name, slug, productName, createdOn, createdBy, isDeleted, versionNumber)
            values (@OrganizationId, @Name, @Slug, coalesce(@ProductName, 'Apeiron'), sysutcdatetime(), @UserId, 0, 1);

            insert into organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @UserId, @OwnerRoleId, 'Active', @UserId, sysutcdatetime(), sysutcdatetime(), @UserId, 0, 1);
            """,
            new { OrganizationId = organizationId, request.Name, Slug = slug, request.ProductName, UserId = userId, OwnerRoleId = ownerRoleId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return new OrganizationDto(organizationId, request.Name, slug, request.ProductName ?? "Apeiron", DateTime.UtcNow);
    }

    public async Task<PagedResult<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, PagedRequest request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition("""
            select count(1)
            from organizationMembers om
            join users u on u.id = om.userId and u.isDeleted = 0
            where om.organizationId = @OrganizationId and om.isDeleted = 0
                and (@Search is null or u.fullName like '%' + @Search + '%' or u.email like '%' + @Search + '%');

            select om.id, om.userId, u.fullName, u.email, u.avatarUrl, om.status, r.name as roleName, coalesce(om.lastActiveOn, u.lastActiveOn) as lastActiveOn, om.createdOn
            from organizationMembers om
            join users u on u.id = om.userId and u.isDeleted = 0
            join roles r on r.id = om.roleId
            where om.organizationId = @OrganizationId and om.isDeleted = 0
                and (@Search is null or u.fullName like '%' + @Search + '%' or u.email like '%' + @Search + '%')
            order by om.createdOn desc
            offset @Offset rows fetch next @Count rows only;
            """,
            new { OrganizationId = organizationId, Search = request.SearchString, Offset = request.SafeOffset, Count = request.SafeCount },
            cancellationToken: cancellationToken));

        var total = await grid.ReadSingleAsync<int>();
        var items = (await grid.ReadAsync<OrganizationMemberDto>()).AsList();
        return new PagedResult<OrganizationMemberDto>(items, total, request.SafeOffset, request.SafeCount);
    }

    public async Task<IReadOnlyList<OrganizationRoleDto>> GetRolesAsync(CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var roles = await connection.QueryAsync<OrganizationRoleDto>(new CommandDefinition("""
            select id, name, scope
            from roles
            where isDeleted = 0 and isSystemRole = 1
            order by
                case name
                    when 'Owner' then 1
                    when 'Admin' then 2
                    when 'Manager' then 3
                    when 'Team Lead' then 4
                    when 'Developer' then 5
                    when 'QA' then 6
                    when 'Viewer' then 7
                    when 'Guest' then 8
                    else 99
                end;
            """, cancellationToken: cancellationToken));
        return roles.AsList();
    }

    public async Task<string?> GetRoleScopeAsync(Guid roleId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition("""
            select scope
            from roles
            where id = @RoleId and isDeleted = 0;
            """,
            new { RoleId = roleId },
            cancellationToken: cancellationToken));
    }

    public async Task<InvitationEmailContextDto?> GetInvitationEmailContextAsync(Guid organizationId, Guid? workspaceId, Guid inviterUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InvitationEmailContextDto>(new CommandDefinition("""
            select top 1
                o.id as organizationId,
                o.name as organizationName,
                w.id as workspaceId,
                w.name as workspaceName,
                u.fullName as inviterName,
                u.email as inviterEmail
            from organizations o
            join users u on u.id = @InviterUserId and u.isDeleted = 0
            left join workspaces w on w.organizationId = o.id
                and w.isDeleted = 0
                and (@WorkspaceId is null or w.id = @WorkspaceId)
            where o.id = @OrganizationId
                and o.isDeleted = 0
            order by case when w.id = @WorkspaceId then 0 else 1 end, w.createdOn;
            """,
            new { OrganizationId = organizationId, WorkspaceId = workspaceId, InviterUserId = inviterUserId },
            cancellationToken: cancellationToken));
    }

    public async Task<InvitationDto> InviteAsync(Guid organizationId, InviteMemberRequest request, Guid invitedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var invitationId = Guid.NewGuid();
        var expiresOn = DateTime.UtcNow.AddDays(7);
        var token = Guid.NewGuid().ToString("N");
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into invitations (id, organizationId, workspaceId, email, roleId, status, message, tokenHash, expiresOn, createdOn, createdBy, isDeleted, versionNumber)
            values (@InvitationId, @OrganizationId, @WorkspaceId, @Email, @RoleId, 'Invited', @Message, @TokenHash, @ExpiresOn, sysutcdatetime(), @InvitedByUserId, 0, 1);
            """,
            new
            {
                InvitationId = invitationId,
                OrganizationId = organizationId,
                request.WorkspaceId,
                request.Email,
                request.RoleId,
                request.Message,
                TokenHash = RepositoryUtility.Sha256(token),
                ExpiresOn = expiresOn,
                InvitedByUserId = invitedByUserId
            },
            cancellationToken: cancellationToken));

        return new InvitationDto(invitationId, request.Email, "Invited", expiresOn, token, request.WorkspaceId);
    }

    public async Task<InvitationDto?> RegenerateInvitationAsync(Guid organizationId, Guid invitationId, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var token = Guid.NewGuid().ToString("N");
        var expiresOn = DateTime.UtcNow.AddDays(7);
        var row = await connection.QuerySingleOrDefaultAsync<InvitationDto>(new CommandDefinition("""
            update invitations
            set tokenHash = @TokenHash,
                expiresOn = @ExpiresOn,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = versionNumber + 1
            output inserted.id, inserted.email, inserted.status, inserted.expiresOn, cast(null as nvarchar(100)) as inviteToken, inserted.workspaceId
            where id = @InvitationId
                and organizationId = @OrganizationId
                and status = 'Invited'
                and expiresOn > sysutcdatetime()
                and isDeleted = 0;
            """,
            new { OrganizationId = organizationId, InvitationId = invitationId, TokenHash = RepositoryUtility.Sha256(token), ExpiresOn = expiresOn, ModifiedByUserId = modifiedByUserId },
            cancellationToken: cancellationToken));

        return row is null ? null : row with { InviteToken = token };
    }

    public async Task<bool> RevokeInvitationAsync(Guid organizationId, Guid invitationId, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition("""
            update invitations
            set status = 'Revoked',
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = versionNumber + 1
            where id = @InvitationId
                and organizationId = @OrganizationId
                and status = 'Invited'
                and isDeleted = 0;
            """,
            new { OrganizationId = organizationId, InvitationId = invitationId, ModifiedByUserId = modifiedByUserId },
            cancellationToken: cancellationToken));
        return rows > 0;
    }

    public async Task<InvitationAcceptanceDto?> AcceptInvitationAsync(string token, Guid acceptingUserId, string acceptingEmail, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var invite = await connection.QuerySingleOrDefaultAsync<InvitationRow>(new CommandDefinition("""
            select top 1 id, organizationId, workspaceId, email, roleId
            from invitations
            where tokenHash = @TokenHash
                and status = 'Invited'
                and expiresOn > sysutcdatetime()
                and isDeleted = 0;
            """,
            new { TokenHash = RepositoryUtility.Sha256(token) },
            transaction,
            cancellationToken: cancellationToken));

        if (invite is null || !invite.Email.Equals(acceptingEmail, StringComparison.OrdinalIgnoreCase))
        {
            transaction.Rollback();
            return null;
        }

        var memberId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition("""
            select top 1 id
            from organizationMembers
            where organizationId = @OrganizationId and userId = @UserId and isDeleted = 0;
            """,
            new { invite.OrganizationId, UserId = acceptingUserId },
            transaction,
            cancellationToken: cancellationToken));

        if (memberId is null)
        {
            memberId = Guid.NewGuid();
            await connection.ExecuteAsync(new CommandDefinition("""
                insert into organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, createdOn, createdBy, isDeleted, versionNumber)
                values (@MemberId, @OrganizationId, @UserId, @RoleId, 'Active', null, sysutcdatetime(), sysutcdatetime(), @UserId, 0, 1);
                """,
                new { MemberId = memberId.Value, invite.OrganizationId, UserId = acceptingUserId, invite.RoleId },
                transaction,
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                update organizationMembers
                set roleId = @RoleId,
                    status = 'Active',
                    joinedOn = coalesce(joinedOn, sysutcdatetime()),
                    modifiedOn = sysutcdatetime(),
                    modifiedBy = @UserId,
                    versionNumber = versionNumber + 1
                where id = @MemberId;
                """,
                new { MemberId = memberId.Value, UserId = acceptingUserId, invite.RoleId },
                transaction,
                cancellationToken: cancellationToken));
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into workspaceMembers (id, organizationId, workspaceId, userId, roleId, status, createdOn, createdBy, isDeleted, versionNumber)
            select newid(), w.organizationId, w.id, @UserId, @RoleId, 'Active', sysutcdatetime(), @UserId, 0, 1
            from workspaces w
            where w.organizationId = @OrganizationId
                and (@WorkspaceId is null or w.id = @WorkspaceId)
                and w.isDeleted = 0
                and not exists (
                    select 1
                    from workspaceMembers wm
                    where wm.workspaceId = w.id
                        and wm.userId = @UserId
                        and wm.isDeleted = 0
                );

            update invitations
            set status = 'Accepted',
                acceptedOn = sysutcdatetime(),
                modifiedOn = sysutcdatetime(),
                modifiedBy = @UserId,
                versionNumber = versionNumber + 1
            where id = @InvitationId;
            """,
            new { InvitationId = invite.Id, UserId = acceptingUserId, invite.OrganizationId, invite.WorkspaceId, invite.RoleId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return new InvitationAcceptanceDto(invite.OrganizationId, memberId.Value, invite.Email, "Active");
    }

    public async Task<OrganizationMemberDto?> GetMemberAsync(Guid organizationId, Guid memberId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OrganizationMemberDto>(new CommandDefinition("""
            select om.id, om.userId, u.fullName, u.email, u.avatarUrl, om.status, r.name as roleName, coalesce(om.lastActiveOn, u.lastActiveOn) as lastActiveOn, om.createdOn
            from organizationMembers om
            join users u on u.id = om.userId and u.isDeleted = 0
            join roles r on r.id = om.roleId
            where om.id = @MemberId and om.organizationId = @OrganizationId and om.isDeleted = 0;
            """,
            new { OrganizationId = organizationId, MemberId = memberId },
            cancellationToken: cancellationToken));
    }

    public async Task<OrganizationMemberDto?> GetMemberByUserIdAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<OrganizationMemberDto>(new CommandDefinition("""
            select om.id, om.userId, u.fullName, u.email, u.avatarUrl, om.status, r.name as roleName, coalesce(om.lastActiveOn, u.lastActiveOn) as lastActiveOn, om.createdOn
            from organizationMembers om
            join users u on u.id = om.userId and u.isDeleted = 0
            join roles r on r.id = om.roleId
            where om.userId = @UserId and om.organizationId = @OrganizationId and om.isDeleted = 0;
            """,
            new { OrganizationId = organizationId, UserId = userId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> CountActiveOwnersAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
            select count(1)
            from organizationMembers om
            join roles r on r.id = om.roleId and r.isDeleted = 0
            where om.organizationId = @OrganizationId
                and om.status = 'Active'
                and om.isDeleted = 0
                and r.name = @OwnerRole;
            """,
            new { OrganizationId = organizationId, OwnerRole = RoleNames.Owner },
            cancellationToken: cancellationToken));
    }

    public async Task ChangeMemberRoleAsync(Guid organizationId, Guid memberId, Guid roleId, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            update organizationMembers
            set roleId = @RoleId,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = versionNumber + 1
            where id = @MemberId and organizationId = @OrganizationId and isDeleted = 0;

            update wm
            set roleId = @RoleId,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = versionNumber + 1
            from workspaceMembers wm
            join organizationMembers om on om.organizationId = wm.organizationId and om.userId = wm.userId and om.id = @MemberId
            where wm.organizationId = @OrganizationId and wm.isDeleted = 0;
            """,
            new { OrganizationId = organizationId, MemberId = memberId, RoleId = roleId, ModifiedByUserId = modifiedByUserId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
    }

    public async Task UpdateMemberStatusAsync(Guid organizationId, Guid memberId, string status, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition("""
            update organizationMembers
            set status = @Status,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = versionNumber + 1
            where id = @MemberId and organizationId = @OrganizationId and isDeleted = 0;

            update wm
            set status = @Status,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = wm.versionNumber + 1
            from workspaceMembers wm
            join organizationMembers om on om.organizationId = wm.organizationId and om.userId = wm.userId
            where om.id = @MemberId
                and wm.organizationId = @OrganizationId
                and wm.isDeleted = 0;
            """,
            new { OrganizationId = organizationId, MemberId = memberId, Status = status, ModifiedByUserId = modifiedByUserId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
    }

    private sealed class InvitationRow
    {
        public Guid Id { get; init; }
        public Guid OrganizationId { get; init; }
        public Guid? WorkspaceId { get; init; }
        public string Email { get; init; } = string.Empty;
        public Guid RoleId { get; init; }
    }
}
