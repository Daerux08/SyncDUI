using System;
using System.IO;
using Avalonia.Controls;
using SyncDUI.Models;
using SyncDUI.ViewModels;

namespace SyncDUI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            _ = viewModel.RefreshCommand.ExecuteAsync(null);
        }
    }

    private void OpenConfigFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !string.IsNullOrWhiteSpace(viewModel.ConfigPath))
        {
            OpenFolderInFileManager(viewModel.ConfigPath);
        }
    }

    private void OpenSelectedFolder_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && viewModel.SelectedFolder is not null)
        {
            OpenFolderInFileManager(viewModel.SelectedFolder.Path);
        }
    }

    private void OpenFolderListItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SyncthingFolderEntry folder)
        {
            OpenFolderInFileManager(folder.Path);
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