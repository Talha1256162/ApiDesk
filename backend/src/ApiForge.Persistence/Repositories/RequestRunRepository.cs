using ApiForge.Application.Abstractions.Persistence;
using ApiForge.Application.DTOs.Requests;
using ApiForge.Shared.Security;
using Dapper;
using ApiForge.Persistence.Connection;

namespace ApiForge.Persistence.Repositories;

public sealed class RequestRunRepository(ISqlConnectionFactory connectionFactory) : IRequestRunRepository
{
    public async Task<Guid> CreateRunAsync(Guid requestId, Guid workspaceId, Guid? environmentId, Guid userId, DateTime startedOnUtc, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var organizationId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "select organizationId from requests where id = @RequestId;",
            new { RequestId = requestId },
            cancellationToken: cancellationToken));
        var runId = Guid.NewGuid();

        await connection.ExecuteAsync(new CommandDefinition("""
            insert into requestRuns
            (id, organizationId, workspaceId, requestId, environmentId, userId, status, startedOn, createdOn, createdBy, isDeleted, versionNumber)
            values
            (@RunId, @OrganizationId, @WorkspaceId, @RequestId, @EnvironmentId, @UserId, 'Running', @StartedOn, sysutcdatetime(), @UserId, 0, 1);
            """,
            new { RunId = runId, OrganizationId = organizationId, WorkspaceId = workspaceId, RequestId = requestId, EnvironmentId = environmentId, UserId = userId, StartedOn = startedOnUtc },
            cancellationToken: cancellationToken));

        return runId;
    }

    public async Task CompleteRunAsync(Guid runId, ApiResponseDto response, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            update requestRuns
            set status = 'Completed',
                succeeded = @Succeeded,
                statusCode = @StatusCode,
                elapsedMs = @ElapsedMs,
                sizeBytes = @SizeBytes,
                completedOn = @CompletedOnUtc,
                modifiedOn = sysutcdatetime(),
                versionNumber = versionNumber + 1
            where id = @RunId;

            insert into requestRunResults
            (id, requestRunId, statusCode, headersJson, cookiesJson, bodyPreview, contentType, createdOn, createdBy, isDeleted, versionNumber)
            values
            (newid(), @RunId, @StatusCode, @HeadersJson, @CookiesJson, @BodyPreview, @ContentType, sysutcdatetime(),
             (select userId from requestRuns where id = @RunId), 0, 1);
            """,
            new
            {
                RunId = runId,
                response.Succeeded,
                response.StatusCode,
                response.ElapsedMs,
                response.SizeBytes,
                response.CompletedOnUtc,
                HeadersJson = SensitiveDataMasker.MaskJson(System.Text.Json.JsonSerializer.Serialize(response.Headers)),
                CookiesJson = SensitiveDataMasker.MaskJson(System.Text.Json.JsonSerializer.Serialize(response.Cookies)),
                response.BodyPreview,
                response.ContentType
            },
            cancellationToken: cancellationToken));
    }

    public async Task FailRunAsync(Guid runId, string errorMessage, long elapsedMs, DateTime completedOnUtc, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            update requestRuns
            set status = 'Failed',
                succeeded = 0,
                errorMessage = @ErrorMessage,
                elapsedMs = @ElapsedMs,
                completedOn = @CompletedOnUtc,
                modifiedOn = sysutcdatetime(),
                versionNumber = versionNumber + 1
            where id = @RunId;
            """,
            new { RunId = runId, ErrorMessage = errorMessage, ElapsedMs = elapsedMs, CompletedOnUtc = completedOnUtc },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RequestRunDto>> GetHistoryAsync(Guid requestId, int count, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RequestRunDto>(new CommandDefinition("""
            select top (@Count)
                rr.id,
                rr.requestId,
                r.name as requestName,
                r.method,
                r.url,
                u.fullName as actorName,
                rr.status,
                rr.userId,
                rr.statusCode,
                rr.succeeded,
                rr.elapsedMs,
                rr.sizeBytes,
                rr.errorMessage,
                rrr.bodyPreview,
                rr.startedOn,
                rr.completedOn,
                rr.createdOn
            from requestRuns rr
            join requests r on r.id = rr.requestId and r.isDeleted = 0
            join users u on u.id = rr.userId and u.isDeleted = 0
            left join requestRunResults rrr on rrr.requestRunId = rr.id and rrr.isDeleted = 0
            where rr.requestId = @RequestId and rr.isDeleted = 0
            order by rr.createdOn desc;
            """,
            new { RequestId = requestId, Count = Math.Clamp(count, 1, 100) },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
