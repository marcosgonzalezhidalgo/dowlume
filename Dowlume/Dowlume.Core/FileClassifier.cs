namespace Dowlume.Core;

public static class FileClassifier
{
    private static readonly HashSet<string> TemporaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".crdownload", ".part", ".partial", ".tmp", ".temp", ".download", ".opdownload"
    };

    public static bool IsExcluded(string path, AppSettings settings)
    {
        string extension = Path.GetExtension(path);
        return TemporaryExtensions.Contains(extension) ||
               settings.ExcludedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
               Path.GetFileName(path).StartsWith('~');
    }

    public static CategoryRule Classify(string path, AppSettings settings) =>
        settings.Categories.FirstOrDefault(rule => rule.Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        ?? settings.Categories.Single(rule => rule.Category == FileCategory.Other);
}
