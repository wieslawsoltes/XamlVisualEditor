using XamlVisualEditor.Extensions;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionSdkTypesTests
{
    [Fact]
    public void InputBoxOptions_StoresValues()
    {
        var options = new InputBoxOptions("Title", "Prompt", "Value");

        Assert.Equal("Title", options.Title);
        Assert.Equal("Prompt", options.Prompt);
        Assert.Equal("Value", options.Value);
    }

    [Fact]
    public void QuickPickItem_StoresValues()
    {
        var item = new QuickPickItem("Label", "Desc", "Detail");

        Assert.Equal("Label", item.Label);
        Assert.Equal("Desc", item.Description);
        Assert.Equal("Detail", item.Detail);
    }

    [Fact]
    public void ConfigurationChangedEventArgs_StoresSection()
    {
        var args = new ConfigurationChangedEventArgs("sample.section");

        Assert.Equal("sample.section", args.Section);
    }

    [Fact]
    public void TreeItem_StoresValues()
    {
        var item = new TreeItem("Label", "Desc", "Context");

        Assert.Equal("Label", item.Label);
        Assert.Equal("Desc", item.Description);
        Assert.Equal("Context", item.ContextValue);
    }

    [Fact]
    public void ExtensionViewContribution_StoresContainerAndPersistenceHints()
    {
        var contribution = new ExtensionViewContribution(
            "sample.view",
            "Sample",
            ExtensionViewType.Custom,
            ExtensionViewLocation.Right,
            10,
            ActivateByDefault: true,
            ContainerId: "CustomDock",
            PersistDockState: false);

        Assert.Equal("sample.view", contribution.ViewId);
        Assert.Equal("CustomDock", contribution.ContainerId);
        Assert.False(contribution.PersistDockState);
        Assert.True(contribution.ActivateByDefault);
    }

    [Fact]
    public void ExtensionViewVisibilityChangedEventArgs_StoresValues()
    {
        var args = new ExtensionViewVisibilityChangedEventArgs("sample.view", true);

        Assert.Equal("sample.view", args.ViewId);
        Assert.True(args.IsVisible);
    }

    [Fact]
    public void ExtensionViewFocusChangedEventArgs_StoresValues()
    {
        var args = new ExtensionViewFocusChangedEventArgs("sample.view", false);

        Assert.Equal("sample.view", args.ViewId);
        Assert.False(args.IsFocused);
    }

    [Fact]
    public void ExtensionCapabilityDeclaration_StoresValues()
    {
        var declaration = new ExtensionCapabilityDeclaration(
            "workspace.write",
            "Write files",
            "Writes files in the workspace.",
            IsHighRisk: true);

        Assert.Equal("workspace.write", declaration.CapabilityId);
        Assert.Equal("Write files", declaration.DisplayName);
        Assert.True(declaration.IsHighRisk);
    }

    [Fact]
    public void ExtensionPermissionDecision_StoresValues()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var decision = new ExtensionPermissionDecision(
            "workspace.write",
            IsAllowed: true,
            IsRemembered: true,
            ExtensionPermissionDecisionSource.Remembered,
            now);

        Assert.Equal("workspace.write", decision.CapabilityId);
        Assert.True(decision.IsAllowed);
        Assert.True(decision.IsRemembered);
        Assert.Equal(ExtensionPermissionDecisionSource.Remembered, decision.Source);
        Assert.Equal(now, decision.DecidedAt);
    }

    [Fact]
    public void ExtensionPermissionAuditEventArgs_StoresValues()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var args = new ExtensionPermissionAuditEventArgs(
            "workspace.write",
            isAllowed: false,
            isRemembered: false,
            ExtensionPermissionDecisionSource.Prompt,
            now);

        Assert.Equal("workspace.write", args.CapabilityId);
        Assert.False(args.IsAllowed);
        Assert.False(args.IsRemembered);
        Assert.Equal(ExtensionPermissionDecisionSource.Prompt, args.Source);
        Assert.Equal(now, args.Timestamp);
    }

    [Fact]
    public void TerminalInfo_StoresDimensions()
    {
        var info = new TerminalInfo(Guid.NewGuid(), "Term", 120, 40);

        Assert.Equal("Term", info.Title);
        Assert.Equal(120, info.Columns);
        Assert.Equal(40, info.Rows);
    }

    [Fact]
    public void TaskExecutionResult_StoresProblemMatches()
    {
        var result = new TaskExecutionResult(
            "build",
            1,
            new[] { "line1", "line2" },
            new[]
            {
                new TaskProblemMatch(TaskProblemSeverity.Error, "Program.cs", 12, 5, "error CS1002")
            });

        Assert.Equal("build", result.TaskId);
        Assert.Equal(1, result.ExitCode);
        Assert.Single(result.Problems);
        Assert.Equal(TaskProblemSeverity.Error, result.Problems[0].Severity);
    }
}
