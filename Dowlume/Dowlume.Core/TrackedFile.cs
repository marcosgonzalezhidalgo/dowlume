namespace Dowlume.Core;

public enum LifecycleStatus
{
    Organized,
    InReview,
    Kept,
    Snoozed,
    Moved,
    Recycled,
    Missing
}

public sealed record TrackedFile(
    Guid Id,
    string OriginalPath,
    string CurrentPath,
    FileCategory Category,
    DateTimeOffset FirstSeenUtc,
    LifecycleStatus Status,
    string ContentHash,
    DateTimeOffset? SnoozedUntilUtc = null)
{
    public string Name => Path.GetFileName(CurrentPath);
    public bool IsManaged => Status is not (LifecycleStatus.Moved or LifecycleStatus.Recycled or LifecycleStatus.Missing);
}
