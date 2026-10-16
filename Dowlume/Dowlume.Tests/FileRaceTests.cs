using Dowlume.Infrastructure;

namespace Dowlume.Tests;

public sealed class FileRaceTests
{
    [Fact]
    public void VerifiedMoveHandleRejectsConcurrentWritesAndSourceReplacement()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string source = workspace.CreateFile("report.txt", "Original verified file");
        string replacement = workspace.CreateFile("replacement.txt", "Unreviewed replacement");
        string destination = Path.Combine(workspace.Downloads, "moved.txt");
        using (FileStream locked = WindowsFileHandle.OpenForMove(source))
        {
            Assert.Throws<IOException>(() => File.WriteAllText(source, "Concurrent edit"));
            Assert.Throws<IOException>(() => File.Move(source, source + ".renamed"));
            Exception? replacementError = Record.Exception(() => File.Move(replacement, source, overwrite: true));
            Assert.True(replacementError is IOException or UnauthorizedAccessException);
            Assert.True(WindowsFileHandle.TryRename(locked.SafeFileHandle, destination));
        }
        Assert.Equal("Original verified file", File.ReadAllText(destination));
        Assert.Equal("Unreviewed replacement", File.ReadAllText(replacement));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public void DestinationCreatedAfterSelectionIsNeverReplaced()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string source = workspace.CreateFile("original.txt", "Original");
        string destination = PathSafety.AvailableDestination(workspace.Downloads, "new.txt");
        using (FileStream locked = WindowsFileHandle.OpenForMove(source))
        {
            File.WriteAllText(destination, "Created by another application");
            Assert.Throws<IOException>(() =>
            {
                if (OperatingSystem.IsWindows())
                {
                    WindowsFileHandle.TryRename(locked.SafeFileHandle, destination);
                }
            });
        }
        Assert.Equal("Original", File.ReadAllText(source));
        Assert.Equal("Created by another application", File.ReadAllText(destination));
    }

    [Fact]
    public void DirectoryLeasePreventsAncestorReplacementDuringAnOperation()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string source = workspace.CreateFile("report.txt");
        string destination = Path.Combine(workspace.Downloads, "Documents", "report.txt");
        using (DirectoryLease lease = DirectoryLease.Acquire(source, destination))
        {
            Assert.Throws<IOException>(() => Directory.Move(workspace.Downloads, workspace.Downloads + "-renamed"));
            Assert.Throws<IOException>(() => Directory.Move(Path.GetDirectoryName(destination)!, Path.Combine(workspace.Downloads, "Replaced")));
        }
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(destination));
    }
}
