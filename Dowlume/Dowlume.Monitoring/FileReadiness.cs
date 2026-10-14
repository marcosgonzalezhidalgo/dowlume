using Dowlume.Core;

namespace Dowlume.Monitoring;

public sealed class FileReadiness(TimeSpan? observationInterval = null, TimeSpan? minimumAge = null) : IFileReadiness
{
    private readonly TimeSpan _observationInterval = observationInterval ?? TimeSpan.FromMilliseconds(300);
    private readonly TimeSpan _minimumAge = minimumAge ?? TimeSpan.FromSeconds(3);

    public async Task<bool> IsReadyAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var before = new FileInfo(path);
            if (!before.Exists || (before.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
                DateTime.UtcNow - before.LastWriteTimeUtc < _minimumAge)
            {
                return false;
            }
            long length = before.Length;
            DateTime modified = before.LastWriteTimeUtc;
            await Task.Delay(_observationInterval, cancellationToken);
            var after = new FileInfo(path);
            if (!after.Exists || after.Length != length || after.LastWriteTimeUtc != modified)
            {
                return false;
            }
            using var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return probe.Length == length;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
