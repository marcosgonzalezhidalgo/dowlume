using Dowlume.Core;
using Dowlume.Infrastructure;

namespace Dowlume.Tests;

public sealed class SafetyTests
{
    [Fact]
    public void SiblingWithMatchingPrefixIsOutsideManagedFolder()
    {
        using var workspace = new TemporaryWorkspace();
        Assert.False(PathSafety.IsWithin(workspace.Downloads, workspace.Downloads + "-other/file.txt"));
        Assert.Throws<IOException>(() => PathSafety.RequireWithin(workspace.Downloads, Path.Combine(workspace.Downloads, "..", "outside.txt")));
    }

    [Fact]
    public void LockedSourceAndExistingDestinationArePreserved()
    {
        using var workspace = new TemporaryWorkspace();
        string source = workspace.CreateFile("a.txt", "Source");
        string destination = workspace.CreateFile("b.txt", "Destination");
        var mover = new SafeFileMover();
        string hash = mover.Hash(source);
        using (var locked = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() => mover.Move(source, destination, hash));
        }
        Assert.Throws<IOException>(() => mover.Move(source, destination, hash));
        Assert.Equal("Source", File.ReadAllText(source));
        Assert.Equal("Destination", File.ReadAllText(destination));
    }

    [Fact]
    public void SourceSurvivesAnInvalidDestination()
    {
        using var workspace = new TemporaryWorkspace();
        string source = workspace.CreateFile("source.txt");
        string blockingFile = workspace.CreateFile("not-a-folder");
        var mover = new SafeFileMover();
        Assert.ThrowsAny<IOException>(() => mover.Move(source, Path.Combine(blockingFile, "file.txt"), mover.Hash(source)));
        Assert.True(File.Exists(source));
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(0x80u, true)]
    [InlineData(0x84u, true)]
    public void RecycleSinkRejectsPermanentDeletion(uint flags, bool allowed)
    {
        var sink = new RecycleOnlyProgressSink();
        Assert.Equal(allowed, sink.PreDeleteItem(flags, IntPtr.Zero) == 0);
    }

    [Fact]
    public void RecycleSinkCancelsWhenTheSourceChangesBeforeShellDeletion()
    {
        using var workspace = new TemporaryWorkspace();
        string source = workspace.CreateFile("review.txt", "Reviewed content");
        var mover = new SafeFileMover();
        string hash = mover.Hash(source);
        var sink = new RecycleOnlyProgressSink(() =>
        {
            using FileStream verified = mover.OpenVerifiedRead(source, hash);
        });
        File.WriteAllText(source, "Unreviewed replacement");
        Assert.True(sink.PreDeleteItem(0x80, IntPtr.Zero) < 0);
        Assert.False(sink.Recycled);
        Assert.Equal("Unreviewed replacement", File.ReadAllText(source));
    }

    [Theory]
    [InlineData(0x80u, 0, true, true)]
    [InlineData(0x80u, 0x00270008, true, true)]
    [InlineData(0x80u, 0, false, false)]
    [InlineData(0x80u, 1, true, false)]
    [InlineData(0x80u, unchecked((int)0x80070070), false, false)]
    [InlineData(0u, 0, true, false)]
    public void RecycleSuccessRequiresExplicitShellConfirmation(uint flags, int result, bool hasDestination, bool expected)
    {
        var sink = new RecycleOnlyProgressSink();
        sink.PostDeleteItem(flags, IntPtr.Zero, result, hasDestination ? new IntPtr(1) : IntPtr.Zero);
        Assert.Equal(expected, sink.Recycled);
        Assert.Equal(result, sink.Result);
    }

    [Fact]
    public void DiagnosticsDoNotIncludePersonalPathsOrExceptionMessages()
    {
        using var workspace = new TemporaryWorkspace();
        string path = Path.Combine(workspace.Root, "log.jsonl");
        new DiagnosticLog(path).Write("test_failure", new IOException("secret-user/private-file.pdf"));
        string log = File.ReadAllText(path);
        Assert.DoesNotContain("secret-user", log);
        Assert.Contains("IOException", log);
    }

    [Fact]
    [Trait("Category", "WindowsShell")]
    public async Task WindowsShellRefusesContentChangedSinceReview()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string path = workspace.CreateFile("changed-review.txt", "Reviewed");
        string reviewedHash = new SafeFileMover().Hash(path);
        File.WriteAllText(path, "Changed before recycling");
        await Assert.ThrowsAsync<IOException>(() => new WindowsRecycleBin().RecycleAsync(path, reviewedHash));
        Assert.Equal("Changed before recycling", File.ReadAllText(path));
    }

    [Fact]
    [Trait("Category", "WindowsShell")]
    public async Task WindowsShellRecyclesADisposableFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string path = workspace.CreateFile($"Dowlume-recycle-test-{Guid.NewGuid():N}.txt");
        var mover = new SafeFileMover();
        await new WindowsRecycleBin().RecycleAsync(path, mover.Hash(path));
        Assert.False(File.Exists(path));
        // Only this disposable file is left in Recycle Bin; never empty the user's bin.
    }
}
