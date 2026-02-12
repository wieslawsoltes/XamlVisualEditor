using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Services;

public sealed class AppWorkspaceHost : IWorkspaceHost
{
    private readonly MainWindowViewModel _mainViewModel;

    public AppWorkspaceHost(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public Task OpenWorkspaceAsync(string workspacePath, WorkspaceOpenMode mode, CancellationToken cancellationToken)
    {
        if (Directory.Exists(workspacePath))
        {
            return mode switch
            {
                WorkspaceOpenMode.NewWindow => OpenInNewWindowAsync(workspacePath, cancellationToken),
                _ => OpenFolderInCurrentWindowAsync(workspacePath)
            };
        }

        return mode switch
        {
            WorkspaceOpenMode.NewWindow => OpenInNewWindowAsync(workspacePath, cancellationToken),
            _ => OpenInCurrentWindowAsync(workspacePath)
        };
    }

    private Task OpenInCurrentWindowAsync(string workspacePath)
    {
        return _mainViewModel.OpenFileAsync(workspacePath);
    }

    private Task OpenFolderInCurrentWindowAsync(string workspacePath)
    {
        return _mainViewModel.OpenFolderAsync(workspacePath);
    }

    private static Task OpenInNewWindowAsync(string workspacePath, CancellationToken cancellationToken)
    {
        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return Task.CompletedTask;
        }

        ProcessStartInfo startInfo;
        if (processPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(processPath);
        }
        else
        {
            startInfo = new ProcessStartInfo(processPath)
            {
                UseShellExecute = false
            };
        }

        startInfo.ArgumentList.Add("--open");
        startInfo.ArgumentList.Add(workspacePath);

        if (!cancellationToken.IsCancellationRequested)
        {
            Process.Start(startInfo);
        }

        return Task.CompletedTask;
    }
}
