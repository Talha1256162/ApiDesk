namespace ApiForge.Shared.Pagination;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Offset, int Count);
