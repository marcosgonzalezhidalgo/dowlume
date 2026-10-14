namespace Dowlume.Core;

public enum OperationKind { Organize, Review, Keep, Snooze, Move, Recycle, Undo }
public enum OperationStatus { Pending, Completed, Failed, NeedsAttention }

public sealed record OperationRecord(
    Guid Id,
    OperationKind Kind,
    OperationStatus Status,
    DateTimeOffset TimestampUtc,
    TrackedFile? Before,
    TrackedFile After,
    string SourcePath,
    string? DestinationPath,
    string? Error = null,
    Guid? UndoesOperationId = null)
{
    public string Description => $"{Kind}: {Path.GetFileName(SourcePath)}";
    public bool CanUndo => Status == OperationStatus.Completed && Kind is not (OperationKind.Recycle or OperationKind.Undo);
}
