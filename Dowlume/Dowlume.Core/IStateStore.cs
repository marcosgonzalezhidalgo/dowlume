namespace Dowlume.Core;

public interface IStateStore
{
    AppSettings? LoadSettings();
    void SaveSettings(AppSettings settings);
    IReadOnlyList<TrackedFile> GetFiles();
    IReadOnlyList<OperationRecord> GetOperations();
    OperationHistoryPage GetOperationHistory(OperationHistoryQuery query);
    int CountOperationsRequiringAttention();
    void SaveFile(TrackedFile file);
    void SaveOperation(OperationRecord operation);
    void CompleteOperation(OperationRecord operation);
}
