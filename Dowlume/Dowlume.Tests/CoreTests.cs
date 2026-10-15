using Dowlume.Core;

namespace Dowlume.Tests;

public sealed class CoreTests
{
    [Theory]
    [InlineData("report.PDF", FileCategory.Documents)]
    [InlineData("photo.jpg", FileCategory.Images)]
    [InlineData("unknown.xyz", FileCategory.Other)]
    [InlineData("setup.msi", FileCategory.Installers)]
    public void ClassifiesExtensionsIgnoringCase(string path, FileCategory expected) =>
        Assert.Equal(expected, FileClassifier.Classify(path, new AppSettings()).Category);

    [Theory]
    [InlineData("test.crdownload")]
    [InlineData("test.PART")]
    [InlineData("~temporary.docx")]
    public void ExcludesIncompleteDownloads(string path) =>
        Assert.True(FileClassifier.IsExcluded(path, new AppSettings()));

    [Fact]
    public void KeptFilesNeverExpire()
    {
        var file = new TrackedFile(Guid.NewGuid(), "a", "a", FileCategory.Other, DateTimeOffset.UnixEpoch, LifecycleStatus.Kept, "hash");
        Assert.False(LifecyclePolicy.IsDue(file, new AppSettings(), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("CON")]
    [InlineData("Expiring Files")]
    public void RejectsUnsafeCategoryNames(string name)
    {
        var settings = new AppSettings { DownloadsPath = Path.GetTempPath() };
        settings.Categories[0] = settings.Categories[0] with
        {
            FolderName = name
        };
        Assert.Throws<ArgumentException>(settings.Validate);
    }
}
