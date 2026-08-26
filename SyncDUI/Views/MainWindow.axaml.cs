using Avalonia.Controls;

namespace SyncDUI.Views;

using SyncDUI.ViewModels;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel viewModel)
        {
            _ = viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }
}