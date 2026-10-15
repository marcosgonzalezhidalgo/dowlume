namespace Dowlume.Infrastructure;

public static class PathSafety
{
    public static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool IsWithin(string root, string path) =>
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, Comparison);

    public static void RequireWithin(string root, string path)
    {
        if (!IsWithin(root, path))
        {
            throw new IOException("The file is outside the configured Downloads folder.");
        }
        RejectLinks(path);
    }

    public static void ValidateRoot(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !Directory.Exists(path) ||
            string.Equals(Path.GetPathRoot(path), Path.TrimEndingDirectorySeparator(path), Comparison) ||
            string.Equals(Path.GetPathRoot(path), path, Comparison) || path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Choose an existing local folder, not a drive root or network share.");
        }
        RejectLinks(path);
        string[] protectedFolders =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        ];
        // Disposable integration folders may live under the OS temporary directory.
        bool isTemporary = IsWithin(Path.GetTempPath(), path);
        if (!isTemporary && protectedFolders.Where(folder => folder.Length > 0).Any(folder =>
                string.Equals(Path.TrimEndingDirectorySeparator(folder), Path.TrimEndingDirectorySeparator(path), Comparison) || IsWithin(folder, path)))
        {
            throw new ArgumentException("System and application-data folders cannot be monitored.");
        }
    }

    public static void RejectLinks(string path)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Symbolic links, junctions, and cloud placeholders are not supported.");
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            current = Path.GetDirectoryName(current);
        }
    }

    public static string AvailableDestination(string folder, string name)
    {
        RejectLinks(folder);
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);
        for (int suffix = 0; suffix < 10000; suffix++)
        {
            string candidate = Path.Combine(folder, suffix == 0 ? name : $"{stem} ({suffix}){extension}");
            if (!Path.Exists(candidate))
            {
                return candidate;
            }
        }
        throw new IOException("Too many files share this name. Rename the source and try again.");
    }
}
