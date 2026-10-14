using System.Text.Json;

namespace Dowlume.Infrastructure;

public sealed class DiagnosticLog(string path)
{
    private readonly object _gate = new();

    public void Write(string eventName, Exception? error = null)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > 1_000_000)
                {
                    File.Move(path, path + ".previous", overwrite: true);
                }
                // Exception messages and stack traces can contain personal paths.
                string record = JsonSerializer.Serialize(new
                {
                    timeUtc = DateTimeOffset.UtcNow,
                    eventName,
                    errorType = error?.GetType().Name,
                    errorCode = error?.HResult
                });
                File.AppendAllText(path, record + Environment.NewLine);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
