using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Dowlume.App.ViewModels;

namespace Dowlume.App;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null)
        {
            return null;
        }

        string viewName = data.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal);
        var viewType = Type.GetType(viewName);

        if (viewType is null)
        {
            return new TextBlock { Text = $"Not Found: {viewName}" };
        }

        return (Control?)Activator.CreateInstance(viewType);
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
