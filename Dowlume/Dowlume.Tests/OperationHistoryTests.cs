using Dowlume.Core;
using Dowlume.Infrastructure;

namespace Dowlume.Tests;

public sealed class OperationHistoryTests : IDisposable
{
    private readonly TemporaryWorkspace _workspace = new();
    private readonly SqliteStateStore _store;

    public OperationHistoryTests() => _store = new SqliteStateStore(_workspace.Database);

    [Fact]
    public void EveryOperationIsReachableBeyondTheFormerTwoHundredLimit()
    {
        OperationRecord[] operations = Enumerable.Range(0, 251).Select(index => Save($"report-{index}.pdf")).ToArray();
        var query = new OperationHistoryQuery();
        var found = new List<Guid>();
        OperationHistoryPage page;
        do
        {
            page = _store.GetOperationHistory(query);
            found.AddRange(page.Items.Select(operation => operation.Id));
            Assert.InRange(page.Items.Count, 1, 50);
            query = query with
            {
                PageNumber = page.PageNumber + 1,
                ThroughSequence = page.ThroughSequence
            };
        } while (page.HasNextPage);

        Assert.Equal(251, page.TotalCount);
        Assert.Equal(6, page.PageCount);
        Assert.Equal(operations.Reverse().Select(operation => operation.Id), found);
        Assert.Equal(251, found.Distinct().Count());
        var reopened = new SqliteStateStore(_workspace.Database);
        Assert.Equal(found.Take(50), reopened.GetOperationHistory(new OperationHistoryQuery()).Items.Select(operation => operation.Id));
    }

    [Fact]
    public void AppendedOperationsDoNotShiftOlderPagesAndUpdatesKeepTheirPosition()
    {
        OperationRecord first = Save("first.pdf", status: OperationStatus.Pending);
        Save("second.pdf");
        OperationHistoryPage initial = _store.GetOperationHistory(new OperationHistoryQuery(PageSize: 1));
        OperationRecord newest = Save("newest.pdf");
        _store.SaveOperation(first with
        {
            Status = OperationStatus.Completed
        });

        OperationHistoryPage older = _store.GetOperationHistory(new OperationHistoryQuery(PageNumber: 2, PageSize: 1, ThroughSequence: initial.ThroughSequence));
        Assert.Equal(2, older.TotalCount);
        Assert.Equal(first.Id, Assert.Single(older.Items).Id);
        Assert.Equal(OperationStatus.Completed, older.Items[0].Status);
        Assert.Equal(newest.Id, _store.GetOperationHistory(new OperationHistoryQuery()).Items[0].Id);
    }

    [Fact]
    public void SearchCombinesFiltersAndTreatsUnicodeAndSqlCharactersLiterally()
    {
        OperationRecord expected = Save("RÉSUMÉ_100%'--.pdf", OperationKind.Move, OperationStatus.Failed);
        Save("RÉSUMÉ_100%'--.pdf", OperationKind.Move);
        Save("RÉSUMÉ_100%'--.pdf", OperationKind.Keep, OperationStatus.Failed);
        Save("RÉSUMÉX100 other.pdf", OperationKind.Move, OperationStatus.Failed);
        var query = new OperationHistoryQuery("résumé_100%'--", OperationKind.Move, OperationStatus.Failed);

        Assert.Equal(expected.Id, Assert.Single(_store.GetOperationHistory(query).Items).Id);
        Assert.Empty(_store.GetOperationHistory(query with
        {
            SearchText = "' OR 1=1 --"
        }).Items);
        Assert.Equal(4, _store.GetOperations().Count);
    }

    [Fact]
    public void SearchIncludesDestinationsAndHandlesOperationsWithoutOne()
    {
        OperationRecord moved = Save("source.pdf", OperationKind.Move);
        _store.SaveOperation(moved with
        {
            DestinationPath = Path.Combine(_workspace.Root, "Archive", "renamed.pdf")
        });
        OperationRecord recycled = Save("recycled.pdf", OperationKind.Recycle);
        _store.SaveOperation(recycled with
        {
            DestinationPath = null
        });

        Assert.Equal(moved.Id, Assert.Single(_store.GetOperationHistory(new OperationHistoryQuery("archive")).Items).Id);
        Assert.Equal(recycled.Id, Assert.Single(_store.GetOperationHistory(new OperationHistoryQuery("RECYCLED")).Items).Id);
        Assert.Equal(2, _store.GetOperationHistory(new OperationHistoryQuery()).TotalCount);
    }

    [Fact]
    public void EmptyAndShrinkingResultsClampThePageWithoutHidingGlobalAttention()
    {
        OperationHistoryPage empty = _store.GetOperationHistory(new OperationHistoryQuery(PageNumber: int.MaxValue));
        Assert.Empty(empty.Items);
        Assert.Equal(1, empty.PageNumber);
        Assert.False(empty.HasNextPage);
        Assert.False(empty.HasPreviousPage);

        OperationRecord pending = Save("pending.pdf", status: OperationStatus.Pending);
        Save("attention.pdf", status: OperationStatus.NeedsAttention);
        Save("completed.pdf");
        Assert.Equal(2, _store.CountOperationsRequiringAttention());
        OperationHistoryPage filtered = _store.GetOperationHistory(new OperationHistoryQuery(Status: OperationStatus.Pending, PageNumber: 2, PageSize: 1));
        Assert.Equal(1, filtered.PageNumber);
        Assert.Equal(pending.Id, Assert.Single(filtered.Items).Id);
        _store.SaveOperation(pending with
        {
            Status = OperationStatus.Completed
        });
        Assert.Empty(_store.GetOperationHistory(new OperationHistoryQuery(Status: OperationStatus.Pending)).Items);
        Assert.Equal(1, _store.CountOperationsRequiringAttention());
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    public void InvalidPagingIsRejected(int pageNumber, int pageSize) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _store.GetOperationHistory(new OperationHistoryQuery(PageNumber: pageNumber, PageSize: pageSize)));

    [Fact]
    public void InvalidFiltersAndBoundariesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => _store.GetOperationHistory(new OperationHistoryQuery(Kind: (OperationKind)999)));
        Assert.Throws<ArgumentException>(() => _store.GetOperationHistory(new OperationHistoryQuery(Status: (OperationStatus)999)));
        Assert.Throws<ArgumentException>(() => _store.GetOperationHistory(new OperationHistoryQuery(ThroughSequence: -1)));
    }

    private OperationRecord Save(string name, OperationKind kind = OperationKind.Organize, OperationStatus status = OperationStatus.Completed)
    {
        string source = Path.Combine(_workspace.Downloads, name);
        var file = new TrackedFile(Guid.NewGuid(), source, source, FileCategory.Documents,
            DateTimeOffset.UtcNow, LifecycleStatus.Organized, "test-hash");
        var operation = new OperationRecord(Guid.NewGuid(), kind, status, file.FirstSeenUtc, null, file, source, source);
        _store.SaveOperation(operation);
        return operation;
    }

    public void Dispose() => _workspace.Dispose();
}
