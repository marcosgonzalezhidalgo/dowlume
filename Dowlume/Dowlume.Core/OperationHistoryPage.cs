namespace Dowlume.Core;

public sealed record OperationHistoryPage(
    IReadOnlyList<OperationRecord> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    long ThroughSequence)
{
    public int PageCount => TotalCount == 0 ? 1 : (TotalCount - 1) / PageSize + 1;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < PageCount;
}
