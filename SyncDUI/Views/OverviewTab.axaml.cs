using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SyncDUI.Views;

public partial class OverviewTab : UserControl
{
    public OverviewTab()
    {
        InitializeComponent();
    }

    private void OpenConfigFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.ConfigPath))
        {
            OpenFolderInFileManager(viewModel.ConfigPath);
        }
    }

    private void OpenSelectedFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel viewModel && viewModel.SelectedFolder is not null)
        {
            OpenFolderInFileManager(viewModel.SelectedFolder.Path);
        }
    }

    private static void OpenFolderInFileManager(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var target = path.Trim();
        if (!Directory.Exists(target) && !File.Exists(target))
        {
            var directory = Path.GetDirectoryName(target);
            if (string.IsNullOrWhiteSpace(directory) || (!Directory.Exists(directory) && !File.Exists(directory)))
            {
                return;
            }

            target = directory;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = target,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = target,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = target,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
        }
    }
}
