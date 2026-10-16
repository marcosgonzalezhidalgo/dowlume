namespace Dowlume.App.Services;

public interface IStartupRegistration
{
    bool IsSupported
    {
        get;
    }
    bool IsEnabled
    {
        get;
    }
    void SetEnabled(bool enabled);
}
