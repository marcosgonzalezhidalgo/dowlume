namespace Dowlume.Tests;

public sealed class TemporaryWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "Dowlume.Tests", Guid.NewGuid().ToString("N"));
    public string Downloads => Path.Combine(Root, "Downloads");
    public string Database => Path.Combine(Root, "state.db");

    public TemporaryWorkspace() => Directory.CreateDirectory(Downloads);

    public string CreateFile(string name, string content = "Disposable test data")
    {
        string path = Path.Combine(Downloads, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        // The randomly created root is the only directory this fixture owns.
        Directory.Delete(Root, recursive: true);
    }
}
