namespace Dowlume.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private string _greeting = "Welcome to Avalonia!";

    public string Greeting
    {
        get => _greeting;
        set => SetProperty(ref _greeting, value);
    }
}
