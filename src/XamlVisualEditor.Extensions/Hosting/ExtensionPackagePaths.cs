using System;
using System.IO;

namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Provides default paths for extension storage.</summary>
public static class ExtensionPackagePaths
{
    /// <summary>Gets the root extensions directory.</summary>
    public static string GetExtensionsRoot()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "XamlVisualEditor", "Extensions");
    }

    /// <summary>Gets the installed packages directory.</summary>
    public static string GetInstalledRoot()
    {
        return Path.Combine(GetExtensionsRoot(), "Installed");
    }

    /// <summary>Gets the local gallery directory.</summary>
    public static string GetCatalogRoot()
    {
        return Path.Combine(GetExtensionsRoot(), "Catalog");
    }

    /// <summary>Gets the extension state file path.</summary>
    public static string GetStateFilePath()
    {
        return Path.Combine(GetExtensionsRoot(), "extension-state.json");
    }
}
