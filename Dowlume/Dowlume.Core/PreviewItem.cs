namespace Dowlume.Core;

public sealed record PreviewItem(string SourcePath, string DestinationPath, FileCategory Category, string ContentHash)
{
    public string Name => Path.GetFileName(SourcePath);
    public string Destination => Path.GetRelativePath(Path.GetDirectoryName(SourcePath)!, DestinationPath);
}
