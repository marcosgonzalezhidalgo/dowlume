namespace Dowlume.Core;

public interface IFileReadiness
{
    Task<bool> IsReadyAsync(string path, CancellationToken cancellationToken);
}
