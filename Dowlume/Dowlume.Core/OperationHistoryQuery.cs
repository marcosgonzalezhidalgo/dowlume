namespace Dowlume.Core;

public sealed record OperationHistoryQuery(
    string SearchText = "",
    OperationKind? Kind = null,
    OperationStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 50,
    long? ThroughSequence = null)
{
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(SearchText);
        ArgumentOutOfRangeException.ThrowIfLessThan(PageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PageSize, 200);
        if (ThroughSequence < 0 || Kind is { } kind && !Enum.IsDefined(kind) || Status is { } status && !Enum.IsDefined(status))
        {
            throw new ArgumentException("Choose valid history filters and a nonnegative history boundary.");
        }
    }
}
