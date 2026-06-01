namespace ApiForge.Shared.Pagination;

public sealed class PagedRequest
{
    public int Offset { get; init; } = 0;
    public int Count { get; init; } = 25;
    public string? SearchString { get; init; }
    public string? Sorting { get; init; }
    public string? SearchFilter { get; init; }

    public int SafeOffset => Math.Max(0, Offset);
    public int SafeCount => Count is < 1 or > 200 ? 25 : Count;
}
