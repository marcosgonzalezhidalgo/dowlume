namespace Dowlume.Core;

public interface IStateStore
{
    AppSettings? LoadSettings();
    void SaveSettings(AppSettings settings);
    IReadOnlyList<TrackedFile> GetFiles();
    IReadOnlyList<OperationRecord> GetOperations();
    void SaveFile(TrackedFile file);
    void SaveOperation(OperationRecord operation);
    void CompleteOperation(OperationRecord operation);
}
