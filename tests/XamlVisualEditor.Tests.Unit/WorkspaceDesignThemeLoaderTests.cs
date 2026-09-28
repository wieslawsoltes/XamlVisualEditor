using System;
using System.IO;
using Xunit;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Tests.Unit;

public class WorkspaceDesignThemeLoaderTests
{
    [Fact]
    public void GetApplicationXamlOverridePath_ReturnsNull_WhenVariableIsUnset()
    {
        using EnvironmentVariableScope scope = new(WorkspaceDesignThemeLoader.ApplicationXamlOverrideVariable, null);

        Assert.Null(WorkspaceDesignThemeLoader.GetApplicationXamlOverridePath());
    }

    [Fact]
    public void GetApplicationXamlOverridePath_ReturnsNull_WhenFileDoesNotExist()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "App.axaml");
        using EnvironmentVariableScope scope = new(WorkspaceDesignThemeLoader.ApplicationXamlOverrideVariable, missingPath);

        Assert.Null(WorkspaceDesignThemeLoader.GetApplicationXamlOverridePath());
    }

    [Fact]
    public void GetApplicationXamlOverridePath_ReturnsFullPath_WhenFileExists()
    {
        string filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".axaml");
        File.WriteAllText(filePath, "<Application />");
        try
        {
            using EnvironmentVariableScope scope = new(WorkspaceDesignThemeLoader.ApplicationXamlOverrideVariable, filePath);

            Assert.Equal(Path.GetFullPath(filePath), WorkspaceDesignThemeLoader.GetApplicationXamlOverridePath());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GetApplicationXamlOverridePath_ReturnsNull_WhenValueIsWhitespace()
    {
        using EnvironmentVariableScope scope = new(WorkspaceDesignThemeLoader.ApplicationXamlOverrideVariable, "   ");

        Assert.Null(WorkspaceDesignThemeLoader.GetApplicationXamlOverridePath());
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previousValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previousValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previousValue);
        }
    }
}
