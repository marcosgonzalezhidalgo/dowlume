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
        // Deny writes while checking content and moving. Delete sharing permits a Windows rename.
        using var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.SequentialScan);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(stream)), expectedHash, StringComparison.Ordinal))
        {
            throw new IOException("The file changed after it was tracked. Review it in Explorer before retrying.");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        PathSafety.RejectLinks(destination);
        File.Move(source, destination, overwrite: false);
        if (File.Exists(source) || !string.Equals(Hash(destination), expectedHash, StringComparison.Ordinal))
        {
            throw new IOException("The move could not be verified. Both paths were preserved for inspection.");
        }
    }
}
