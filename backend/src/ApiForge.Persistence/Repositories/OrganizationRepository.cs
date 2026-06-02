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
            values (@OrganizationId, @Name, @Slug, coalesce(@ProductName, 'API DESK'), sysutcdatetime(), @UserId, 0, 1);

            insert into organizationMembers (id, organizationId, userId, roleId, status, invitedByUserId, joinedOn, createdOn, createdBy, isDeleted, versionNumber)
            values (newid(), @OrganizationId, @UserId, @OwnerRoleId, 'Active', @UserId, sysutcdatetime(), sysutcdatetime(), @UserId, 0, 1);
            """,
            new { OrganizationId = organizationId, request.Name, Slug = slug, request.ProductName, UserId = userId, OwnerRoleId = ownerRoleId },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return new OrganizationDto(organizationId, request.Name, slug, request.ProductName ?? "API DESK", DateTime.UtcNow);
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

    public async Task<InvitationDto> InviteAsync(Guid organizationId, InviteMemberRequest request, Guid invitedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var invitationId = Guid.NewGuid();
        var expiresOn = DateTime.UtcNow.AddDays(7);
        await connection.ExecuteAsync(new CommandDefinition("""
            insert into invitations (id, organizationId, email, roleId, status, message, tokenHash, expiresOn, createdOn, createdBy, isDeleted, versionNumber)
            values (@InvitationId, @OrganizationId, @Email, @RoleId, 'Invited', @Message, @TokenHash, @ExpiresOn, sysutcdatetime(), @InvitedByUserId, 0, 1);
            """,
            new
            {
                InvitationId = invitationId,
                OrganizationId = organizationId,
                request.Email,
                request.RoleId,
                request.Message,
                TokenHash = RepositoryUtility.Sha256($"{invitationId}:{request.Email}:{expiresOn:O}"),
                ExpiresOn = expiresOn,
                InvitedByUserId = invitedByUserId
            },
            cancellationToken: cancellationToken));

        return new InvitationDto(invitationId, request.Email, "Invited", expiresOn);
    }

    public async Task UpdateMemberStatusAsync(Guid organizationId, Guid memberId, string status, Guid modifiedByUserId, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            update organizationMembers
            set status = @Status,
                modifiedOn = sysutcdatetime(),
                modifiedBy = @ModifiedByUserId,
                versionNumber = versionNumber + 1
            where id = @MemberId and organizationId = @OrganizationId and isDeleted = 0;
            """,
            new { OrganizationId = organizationId, MemberId = memberId, Status = status, ModifiedByUserId = modifiedByUserId },
            cancellationToken: cancellationToken));
    }
}
