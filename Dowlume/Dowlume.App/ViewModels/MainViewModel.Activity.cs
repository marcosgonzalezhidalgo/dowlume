using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dowlume.Core;

namespace Dowlume.App.ViewModels;

public partial class MainViewModel
{
    public static IReadOnlyList<HistoryFilter<OperationKind>> ActivityKinds
    {
        get;
    } =
        [new("All actions", null), .. Enum.GetValues<OperationKind>().Select(kind => new HistoryFilter<OperationKind>(kind.ToString(), kind))];

    public static IReadOnlyList<HistoryFilter<OperationStatus>> ActivityStatuses
    {
        get;
    } =
        [new("All statuses", null), .. Enum.GetValues<OperationStatus>().Select(status =>
            new HistoryFilter<OperationStatus>(status == OperationStatus.NeedsAttention ? "Needs attention" : status.ToString(), status))];

    private OperationHistoryQuery _historyQuery = new();
    private OperationHistoryPage? _historyPage;

    [ObservableProperty] private string _activitySearch = string.Empty;
    [ObservableProperty] private HistoryFilter<OperationKind> _selectedActivityKind = ActivityKinds[0];
    [ObservableProperty] private HistoryFilter<OperationStatus> _selectedActivityStatus = ActivityStatuses[0];
    [ObservableProperty] private string _activityPageLabel = "No operations yet.";
    [ObservableProperty] private bool _hasPreviousActivityPage;
    [ObservableProperty] private bool _hasNextActivityPage;
    [ObservableProperty] private bool _isActivityEmpty = true;

    [RelayCommand]
    private Task SearchActivityAsync() => RunAsync(async () =>
    {
        _historyQuery = new OperationHistoryQuery(ActivitySearch, SelectedActivityKind.Value, SelectedActivityStatus.Value);
        await RefreshAsync();
    });

    [RelayCommand]
    private Task ClearActivityFiltersAsync() => RunAsync(async () =>
    {
        ActivitySearch = string.Empty;
        SelectedActivityKind = ActivityKinds[0];
        SelectedActivityStatus = ActivityStatuses[0];
        _historyQuery = new OperationHistoryQuery();
        await RefreshAsync();
    });

    [RelayCommand]
    private Task PreviousActivityPageAsync() => ChangeActivityPageAsync(-1);

    [RelayCommand]
    private Task NextActivityPageAsync() => ChangeActivityPageAsync(1);

    private Task ChangeActivityPageAsync(int direction) => RunAsync(async () =>
    {
        if (_historyPage is null || direction < 0 && !_historyPage.HasPreviousPage || direction > 0 && !_historyPage.HasNextPage)
        {
            return;
        }
        int pageNumber = _historyPage.PageNumber + direction;
        _historyQuery = _historyQuery with
        {
            PageNumber = pageNumber,
            ThroughSequence = pageNumber == 1 ? null : _historyPage.ThroughSequence
        };
        await RefreshAsync();
    });

    private void ShowActivityPage(OperationHistoryPage page)
    {
        Guid? selectedId = SelectedOperation?.Id;
        Replace(Activity, page.Items);
        SelectedOperation = Activity.FirstOrDefault(operation => operation.Id == selectedId);
        _historyPage = page;
        _historyQuery = _historyQuery with
        {
            PageNumber = page.PageNumber,
            ThroughSequence = page.PageNumber == 1 ? null : _historyQuery.ThroughSequence
        };
        HasPreviousActivityPage = page.HasPreviousPage;
        HasNextActivityPage = page.HasNextPage;
        IsActivityEmpty = page.TotalCount == 0;
        ActivityPageLabel = page.TotalCount == 0 ? "No matching operations." :
            $"Page {page.PageNumber} of {page.PageCount} · {page.TotalCount} matching operations";
    }
}
