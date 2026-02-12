using System;
using System.IO;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class DebugTargetValidationTests
{
    [Fact]
    public void TryValidateDebugTarget_Fails_For_NetStandard()
    {
        using TempDir temp = new();
        string assemblyPath = Path.Combine(temp.Path, "netstandard2.0", "Test.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        File.WriteAllText(Path.ChangeExtension(assemblyPath, ".runtimeconfig.json"), "{}");

        bool result = MainWindowViewModel.TryValidateDebugTarget(assemblyPath, out string? failureReason);

        Assert.False(result);
        Assert.NotNull(failureReason);
        Assert.Contains("netstandard", failureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateDebugTarget_Fails_When_RuntimeConfig_Missing()
    {
        using TempDir temp = new();
        string assemblyPath = Path.Combine(temp.Path, "net8.0", "Test.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);

        bool result = MainWindowViewModel.TryValidateDebugTarget(assemblyPath, out string? failureReason);

        Assert.False(result);
        Assert.NotNull(failureReason);
        Assert.Contains("runtimeconfig", failureReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidateDebugTarget_Succeeds_With_RuntimeConfig()
    {
        using TempDir temp = new();
        string assemblyPath = Path.Combine(temp.Path, "net8.0", "Test.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(assemblyPath)!);
        File.WriteAllText(Path.ChangeExtension(assemblyPath, ".runtimeconfig.json"), "{}");

        bool result = MainWindowViewModel.TryValidateDebugTarget(assemblyPath, out string? failureReason);

        Assert.True(result);
        Assert.Null(failureReason);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xve-debug-validation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
            catch
            {
            }
        }
    }
}
