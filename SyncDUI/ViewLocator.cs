using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SyncDUI.ViewModels;
using SyncDUI.Views;

namespace SyncDUI;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        if (param is MainViewModel)
        {
            return new MainWindow();
        }

        return new TextBlock { Text = "Not Found: " + param.GetType().Name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
