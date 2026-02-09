using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;

namespace XamlVisualEditor.Shell.ViewModels;

internal sealed class WorkspaceAssemblyResolver : IDisposable
{
    private readonly List<string> _searchDirectories = new();
    private readonly HashSet<string> _searchDirectorySet = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AssemblyDependencyResolver> _dependencyResolvers = new();
    private readonly HashSet<string> _missingManaged = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _missingNative = new(StringComparer.OrdinalIgnoreCase);
    private readonly Action<string, string>? _log;
    private bool _isDisposed;

    public WorkspaceAssemblyResolver(
        IEnumerable<string> assemblyPaths,
        IEnumerable<string>? preferredAssemblyPaths = null,
        Action<string, string>? log = null)
    {
        _log = log;
        AddSearchDirectories(preferredAssemblyPaths);
        AddSearchDirectories(assemblyPaths);
        AddDependencyResolvers(preferredAssemblyPaths ?? assemblyPaths);

        AssemblyLoadContext.Default.Resolving += OnResolving;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll += OnResolvingUnmanagedDll;
    }

    private void AddSearchDirectories(IEnumerable<string>? assemblyPaths)
    {
        if (assemblyPaths is null)
        {
            return;
        }

        foreach (string path in assemblyPaths)
        {
            if (!ShouldIncludeAssemblyPath(path))
            {
                continue;
            }

            string? dir = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            if (_searchDirectorySet.Add(dir))
            {
                _searchDirectories.Add(dir);
            }
        }
    }

    private void AddDependencyResolvers(IEnumerable<string> assemblyPaths)
    {
        foreach (string path in assemblyPaths)
        {
            if (!ShouldIncludeAssemblyPath(path))
            {
                continue;
            }

            try
            {
                _dependencyResolvers.Add(new AssemblyDependencyResolver(path));
            }
            catch
            {
                // Ignore invalid component paths.
            }
        }
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        if (string.IsNullOrEmpty(name.Name))
        {
            return null;
        }

        foreach (AssemblyDependencyResolver resolver in _dependencyResolvers)
        {
            string? resolvedPath = resolver.ResolveAssemblyToPath(name);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            {
                return context.LoadFromAssemblyPath(resolvedPath);
            }
        }

        string fileName = name.Name + ".dll";
        foreach (string dir in _searchDirectories)
        {
            string candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate))
            {
                return context.LoadFromAssemblyPath(candidate);
            }
        }

        LogMissingManaged(name);

        return null;
    }

    private IntPtr OnResolvingUnmanagedDll(Assembly assembly, string libraryName)
    {
        foreach (AssemblyDependencyResolver resolver in _dependencyResolvers)
        {
            string? resolvedPath = resolver.ResolveUnmanagedDllToPath(libraryName);
            if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            {
                if (NativeLibrary.TryLoad(resolvedPath, out IntPtr handle))
                {
                    return handle;
                }
            }
        }

        LogMissingNative(assembly, libraryName);

        return IntPtr.Zero;
    }

    private void LogMissingManaged(AssemblyName name)
    {
        if (string.IsNullOrWhiteSpace(name.Name))
        {
            return;
        }

        if (_missingManaged.Add(name.Name))
        {
            _log?.Invoke("Warning", $"Unresolved assembly: {name.Name}");
        }
    }

    private void LogMissingNative(Assembly assembly, string libraryName)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return;
        }

        string key = $"{assembly.FullName}:{libraryName}";
        if (_missingNative.Add(key))
        {
            _log?.Invoke("Warning", $"Unresolved native library '{libraryName}' for {assembly.GetName().Name}");
        }
    }

    private static bool ShouldIncludeAssemblyPath(string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assemblyPath);
        if (!extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsReferenceAssemblyPath(assemblyPath);
    }

    private static bool IsReferenceAssemblyPath(string assemblyPath)
    {
        string normalized = assemblyPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        string marker = Path.DirectorySeparatorChar.ToString();
        return normalized.Contains(marker + "ref" + marker, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(marker + "refint" + marker, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        AssemblyLoadContext.Default.Resolving -= OnResolving;
        AssemblyLoadContext.Default.ResolvingUnmanagedDll -= OnResolvingUnmanagedDll;
    }
}
