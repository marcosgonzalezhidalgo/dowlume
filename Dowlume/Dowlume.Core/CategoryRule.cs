namespace Dowlume.Core;

public sealed record CategoryRule(FileCategory Category, string FolderName, string[] Extensions, int ReviewAfterDays);
