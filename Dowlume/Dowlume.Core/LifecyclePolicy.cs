namespace Dowlume.Core;

public static class LifecyclePolicy
{
    public static bool IsDue(TrackedFile file, AppSettings settings, DateTimeOffset now) =>
        file.Status switch
        {
            LifecycleStatus.Organized => now >= file.FirstSeenUtc.AddDays(settings.Categories.Single(rule => rule.Category == file.Category).ReviewAfterDays),
            LifecycleStatus.Snoozed => file.SnoozedUntilUtc is { } until && now >= until,
            _ => false
        };
}
