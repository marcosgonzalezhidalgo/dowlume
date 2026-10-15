namespace Dowlume.Core;

public interface IRecycleBin
{
    Task RecycleAsync(string path);
}
