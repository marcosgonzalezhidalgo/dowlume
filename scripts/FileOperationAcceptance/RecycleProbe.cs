using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Dowlume.Core;
using Dowlume.Infrastructure;
using Microsoft.Win32;

namespace FileOperationAcceptance;

[SupportedOSPlatform("windows")]
internal static class RecycleProbe
{
    public static async Task RunAsync(string sourcePath, string expectedHash, string reportPath)
    {
        using RegistryKey? hardware = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
        if (hardware?.GetValue("SystemManufacturer") as string != "Microsoft Corporation" ||
            hardware.GetValue("SystemProductName") as string != "Virtual Machine")
        {
            throw new InvalidOperationException("Recycle policy probes require the disposable Hyper-V guest.");
        }
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        if (!string.Equals(Environment.UserName, "DowlumeTester", StringComparison.OrdinalIgnoreCase) ||
            identity.Groups?.Contains(administrators) == true)
        {
            throw new InvalidOperationException("Use the standard DowlumeTester guest account.");
        }

        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dowlume.RecycleAcceptance") + Path.DirectorySeparatorChar;
        sourcePath = Path.GetFullPath(sourcePath);
        reportPath = Path.GetFullPath(reportPath);
        if (!sourcePath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(sourcePath), Path.GetDirectoryName(reportPath), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(sourcePath).StartsWith("probe-", StringComparison.Ordinal) ||
            Path.GetExtension(sourcePath) != ".bin" || Path.GetExtension(reportPath) != ".json")
        {
            throw new ArgumentException("Use a probe file and new report inside the dedicated disposable fixture directory.");
        }
        PathSafety.RejectLinks(sourcePath);
        PathSafety.RejectLinks(reportPath);
        if (File.Exists(reportPath))
        {
            throw new IOException("The report already exists; preserve the earlier evidence.");
        }
        var mover = new SafeFileMover();
        if (mover.Hash(sourcePath) != expectedHash)
        {
            throw new IOException("The probe source does not match its expected content.");
        }

        Exception? failure = null;
        try
        {
            await new WindowsRecycleBin().RecycleAsync(sourcePath, expectedHash).WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (Exception error)
        {
            failure = error;
        }
        bool sourceExists = File.Exists(sourcePath);
        var report = new
        {
            RecordedAtUtc = DateTime.UtcNow,
            AdapterReturnedSuccess = failure is null,
            ErrorType = failure?.GetType().FullName,
            Error = failure?.Message,
            ErrorHResult = failure?.HResult,
            SourceExists = sourceExists,
            SourceHash = sourceExists ? mover.Hash(sourcePath) : null,
            ExpectedHash = expectedHash,
            InfrastructureSha256 = HashAssembly(typeof(WindowsRecycleBin).Assembly.Location),
            CoreSha256 = HashAssembly(typeof(IRecycleBin).Assembly.Location)
        };
        await using var output = new FileStream(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string HashAssembly(string path)
    {
        using FileStream input = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(input));
    }
}
