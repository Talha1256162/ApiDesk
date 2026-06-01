namespace ApiForge.Application.DTOs.Rbac;

public sealed record RoleDto(Guid Id, string Name, string Scope, bool IsSystemRole);
public sealed record PermissionDto(Guid Id, string Key, string Description);
