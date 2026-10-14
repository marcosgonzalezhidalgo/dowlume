namespace Dowlume.App.Services;

public interface IDesktopDialogs
{
    Task<string?> ChooseFolderAsync(string title);
    Task<bool> ConfirmAsync(string title, string message, string action);
    void RevealFile(string path);
}
