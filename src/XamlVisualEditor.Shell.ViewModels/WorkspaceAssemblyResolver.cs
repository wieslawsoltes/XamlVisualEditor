using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace XamlVisualEditor.Shell.ViewModels;

internal sealed class WorkspaceAssemblyResolver : IDisposable
{
    private readonly HashSet<string> _directories;
    private bool _isDisposed;

    public WorkspaceAssemblyResolver(IEnumerable<string> assemblyPaths)
    {
        _directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in assemblyPaths)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                _directories.Add(dir);
            }
        }

        AssemblyLoadContext.Default.Resolving += OnResolving;
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
        {
            return null;
        }

        string fileName = name.Name + ".dll";
        foreach (string dir in _directories)
        {
            string candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return context.LoadFromAssemblyPath(candidate);
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        AssemblyLoadContext.Default.Resolving -= OnResolving;
    }
}
