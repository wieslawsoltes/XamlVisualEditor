using System;
using System.Reflection;
using System.Xml.Linq;
using XamlVisualEditor.Shell.ViewModels;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DesignTimeParsingTests
{
    [Fact]
    public void NormalizeDesignDataContext_CopiesDesignNamespaceValue()
    {
        string input = """
    <UserControl xmlns="https://github.com/avaloniaui"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             d:DataContext="Sample">
    </UserControl>
    """;

        string normalized = InvokeNormalizeDesignDataContext(input);
        XDocument doc = XDocument.Parse(normalized);
        XAttribute? attr = doc.Root?.Attribute("Design.DataContext");

        Assert.NotNull(attr);
        Assert.Equal("Sample", attr!.Value);
    }

    [Fact]
    public void NormalizeDesignDataContext_SkipsMarkupExtensionValues()
    {
        string input = """
    <UserControl xmlns="https://github.com/avaloniaui"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             d:DataContext="{x:Static local:Foo.Bar}">
    </UserControl>
    """;

        string normalized = InvokeNormalizeDesignDataContext(input);
        XDocument doc = XDocument.Parse(normalized);
        XAttribute? attr = doc.Root?.Attribute("Design.DataContext");

        Assert.Null(attr);
    }

    [Fact]
    public void TryGetDesignSize_UsesDesignWidthHeight()
    {
        string input = """
    <UserControl xmlns="https://github.com/avaloniaui"
             xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
             d:DesignWidth="420"
             d:DesignHeight="240">
    </UserControl>
    """;

        (double? width, double? height) = InvokeTryGetDesignSize(input);

        Assert.Equal(420, width);
        Assert.Equal(240, height);
    }

    [Fact]
    public void TryGetDesignSize_FallsBackToWidthHeight()
    {
        string input = """
    <UserControl xmlns="https://github.com/avaloniaui"
             Width="320"
             Height="180">
    </UserControl>
    """;

        (double? width, double? height) = InvokeTryGetDesignSize(input);

        Assert.Equal(320, width);
        Assert.Equal(180, height);
    }

    private static string InvokeNormalizeDesignDataContext(string xaml)
    {
        MethodInfo method = GetPreviewerMethod("NormalizeDesignDataContext");
        return (string)method.Invoke(null, new object?[] { xaml })!;
    }

    private static (double? Width, double? Height) InvokeTryGetDesignSize(string xaml)
    {
        MethodInfo method = GetPreviewerMethod("TryGetDesignSize");
        object? result = method.Invoke(null, new object?[] { xaml });
        return result is ValueTuple<double?, double?> tuple
            ? tuple
            : (null, null);
    }

    private static MethodInfo GetPreviewerMethod(string methodName)
    {
        Type? type = typeof(MainWindowViewModel).Assembly
            .GetType("XamlVisualEditor.Shell.ViewModels.PreviewerLaunchService");
        Assert.NotNull(type);

        MethodInfo? method = type!.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method!;
    }
}
