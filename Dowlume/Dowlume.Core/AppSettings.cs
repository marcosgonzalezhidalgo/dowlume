namespace Dowlume.Core;

public sealed record AppSettings
{
    public const string ReviewFolderName = "Expiring Files";

    public string DownloadsPath { get; init; } = string.Empty;
    public List<CategoryRule> Categories { get; init; } =
    [
        new(FileCategory.Documents, "Documents", [".pdf", ".txt", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv", ".odt", ".epub"], 30),
        new(FileCategory.Images, "Images", [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".heic", ".bmp", ".tiff"], 60),
        new(FileCategory.Videos, "Videos", [".mp4", ".mkv", ".mov", ".avi", ".webm"], 30),
        new(FileCategory.Archives, "Archives", [".zip", ".7z", ".rar", ".tar", ".gz"], 14),
        new(FileCategory.Installers, "Installers", [".exe", ".msi", ".msix", ".appx", ".iso"], 7),
        new(FileCategory.Other, "Other", [], 30)
    ];
    public string[] ExcludedExtensions { get; init; } = [];

    public void Validate()
    {
        if (!Path.IsPathFullyQualified(DownloadsPath))
        {
            throw new ArgumentException("Choose an absolute Downloads folder path.");
        }

        if (Categories.Count != Enum.GetValues<FileCategory>().Length ||
            Categories.Select(rule => rule.Category).Distinct().Count() != Categories.Count ||
            Categories.Any(rule => !Enum.IsDefined(rule.Category)))
        {
            throw new ArgumentException("Define exactly one rule for each category.");
        }

        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ReviewFolderName };
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (CategoryRule rule in Categories)
        {
            ValidateFolderName(rule.FolderName);
            if (!folders.Add(rule.FolderName) || rule.ReviewAfterDays is < 1 or > 3650)
            {
                throw new ArgumentException("Category folders must be unique; review periods must be 1–3650 days.");
            }
            foreach (string extension in rule.Extensions)
            {
                ValidateExtension(extension);
                if (!extensions.Add(extension))
                {
                    throw new ArgumentException("An extension can belong to only one category.");
                }
            }
        }
        foreach (string extension in ExcludedExtensions)
        {
            ValidateExtension(extension);
        }
    }

    private static void ValidateFolderName(string name)
    {
        string stem = name.Split('.')[0];
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name is "." or ".." ||
            name.IndexOfAny(['<', '>', ':', '"', '/', '\\', '|', '?', '*']) >= 0 ||
            name.Any(char.IsControl) || name.EndsWith('.') || name.EndsWith(' ') ||
            reserved.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Category names must be safe Windows folder names.");
        }
    }

    private static void ValidateExtension(string extension)
    {
        if (extension.Length < 2 || extension[0] != '.' || !extension.Skip(1).All(char.IsAsciiLetterOrDigit))
        {
            throw new ArgumentException("Extensions must start with a dot and contain only letters and numbers.");
        }
    }
}
