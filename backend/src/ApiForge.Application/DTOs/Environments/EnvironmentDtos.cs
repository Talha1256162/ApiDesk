namespace ApiForge.Application.DTOs.Environments;

public sealed record EnvironmentDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    bool IsDefault,
    int VariableCount,
    int SecretCount,
    int VersionNumber,
    DateTime CreatedOn,
    DateTime? ModifiedOn);

public sealed record CreateEnvironmentRequest(Guid WorkspaceId, string Name, bool IsDefault);

public sealed record UpdateEnvironmentRequest(string Name, bool IsDefault);

public sealed record DuplicateEnvironmentRequest(string? Name, bool IsDefault);

public sealed record UpsertEnvironmentVariablesRequest(IReadOnlyList<EnvironmentVariableUpsertDto> Variables);

public sealed record EnvironmentVariableUpsertDto(string Key, string? Value, string Scope, bool IsSecret, bool Enabled);

public sealed record EnvironmentVariableDto(
    Guid Id,
    string Key,
    string? Value,
    string Scope,
    bool IsSecret,
    bool Enabled,
    DateTime CreatedOn,
    DateTime? ModifiedOn);
