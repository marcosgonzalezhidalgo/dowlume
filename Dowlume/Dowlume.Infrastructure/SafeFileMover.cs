using System.Security.Cryptography;

namespace Dowlume.Infrastructure;

public sealed class SafeFileMover
{
    public string Hash(string path)
    {
        PathSafety.RejectLinks(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public void Move(string source, string destination, string expectedHash)
    {
        PathSafety.RejectLinks(source);
        PathSafety.RejectLinks(destination);
        if (OperatingSystem.IsWindows())
        {
            MoveOnWindows(source, destination, expectedHash);
            return;
        }
        using FileStream stream = OpenVerifiedRead(source, expectedHash);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        PathSafety.RejectLinks(destination);
        File.Move(source, destination, overwrite: false);
        if (File.Exists(source) || !string.Equals(Hash(destination), expectedHash, StringComparison.Ordinal))
        {
            throw new IOException("The move could not be verified. Both paths were preserved for inspection.");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void MoveOnWindows(string source, string destination, string expectedHash)
    {
        using var directories = DirectoryLease.Acquire(source, destination);
        using FileStream verifiedSource = WindowsFileHandle.OpenForMove(source);
        VerifyHash(verifiedSource, expectedHash);
        if (WindowsFileHandle.TryRename(verifiedSource.SafeFileHandle, destination))
        {
            return;
        }
        WindowsFileHandle.CopyWithoutReplacing(source, destination);
        // Keep the verified copy locked until the original handle is closed/deleted.
        using var copy = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        VerifyHash(copy, expectedHash);
        WindowsFileHandle.DeleteVerifiedSource(verifiedSource.SafeFileHandle);
        verifiedSource.Dispose();
    }

    private static void VerifyHash(Stream stream, string expectedHash)
    {
        if (Convert.ToHexString(SHA256.HashData(stream)) != expectedHash)
        {
            throw new IOException("The file changed after it was tracked. Review it in Explorer before retrying.");
        }
    }

    public FileStream OpenVerifiedRead(string path, string expectedHash)
    {
        PathSafety.RejectLinks(path);
        // Deny writes while processing; delete sharing permits Windows shell moves.
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.SequentialScan);
        try
        {
            VerifyHash(stream, expectedHash);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
