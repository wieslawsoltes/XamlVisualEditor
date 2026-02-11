using System;
using XamlVisualEditor.Extensions;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionSdkInfoTests
{
    [Fact]
    public void ApiVersion_IsStableValue()
    {
        Assert.Equal(new Version(0, 1, 0), ExtensionSdkInfo.ApiVersion);
    }
}
