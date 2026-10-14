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
    public async Task WindowsShellRecyclesADisposableFile()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string path = workspace.CreateFile($"Dowlume-recycle-test-{Guid.NewGuid():N}.txt");
        var mover = new SafeFileMover();
        using FileStream verifiedSource = mover.OpenVerifiedRead(path, mover.Hash(path));
        await new WindowsRecycleBin().RecycleAsync(path);
        Assert.False(File.Exists(path));
        // Only this disposable file is left in Recycle Bin; never empty the user's bin.
    }
}
