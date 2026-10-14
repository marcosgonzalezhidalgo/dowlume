using System.Diagnostics;
using Dowlume.Infrastructure;

namespace Dowlume.Tests;

public sealed class JunctionTests
{
    [Fact]
    public void RejectsJunctionDestinationsWithoutTouchingTheirTargets()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
        using var workspace = new TemporaryWorkspace();
        string outside = Directory.CreateDirectory(Path.Combine(workspace.Root, "Outside")).FullName;
        string junction = Path.Combine(workspace.Downloads, "Documents");
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.Arguments = $"/c mklink /J \"{junction}\" \"{outside}\"";
        using Process process = Process.Start(start)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
        try
        {
            string source = workspace.CreateFile("report.pdf");
            var mover = new SafeFileMover();
            Assert.Throws<IOException>(() => mover.Move(source, Path.Combine(junction, "report.pdf"), mover.Hash(source)));
            Assert.True(File.Exists(source));
            Assert.Empty(Directory.GetFiles(outside));
        }
        finally
        {
            Directory.Delete(junction, recursive: false);
        }
    }
}
