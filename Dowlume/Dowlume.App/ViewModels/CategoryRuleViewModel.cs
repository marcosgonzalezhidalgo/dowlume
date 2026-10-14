using CommunityToolkit.Mvvm.ComponentModel;
using Dowlume.Core;

namespace Dowlume.App.ViewModels;

public partial class CategoryRuleViewModel : ObservableObject
{
    public FileCategory Category { get; }
    [ObservableProperty] private string _folderName;
    [ObservableProperty] private string _extensions;
    [ObservableProperty] private int _reviewAfterDays;

    public CategoryRuleViewModel(CategoryRule rule)
    {
        Category = rule.Category;
        _folderName = rule.FolderName;
        _extensions = string.Join(", ", rule.Extensions);
        _reviewAfterDays = rule.ReviewAfterDays;
    }

    public CategoryRule ToRule() => new(Category, FolderName.Trim(), ParseExtensions(Extensions), ReviewAfterDays);

    public static string[] ParseExtensions(string value) => value
        .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(extension => extension.ToLowerInvariant()).ToArray();
}
