using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.DotNetTemplatesExtension.ViewModels;
using XamlVisualEditor.DotNetTemplatesExtension.Views;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.DotNetTemplatesExtension;

public sealed class DotNetTemplatesExtension : IXveExtension
{
    private const string WizardDialogId = "dotnet.templates.wizard";
    private const string WorkspacePromptDialogId = "dotnet.templates.workspace.open";
    private const string NewProjectCommandId = "dotnet.templates.newProject";
    private const string NewSolutionCommandId = "dotnet.templates.newSolution";
    private const string NewFileCommandId = "dotnet.templates.newFile";

    private readonly IDotNetTemplateService _templateService;

    public DotNetTemplatesExtension(IDotNetTemplateService templateService)
    {
        _templateService = templateService;
    }

    public Task ActivateAsync(ExtensionContext context, CancellationToken cancellationToken)
    {
        context.Subscriptions.Add(context.DialogHost.RegisterDialog(
            WizardDialogId,
            viewModel => new NewDotNetWizardDialog { DataContext = viewModel }));

        context.Subscriptions.Add(context.DialogHost.RegisterDialog(
            WorkspacePromptDialogId,
            viewModel => new WorkspaceOpenPromptDialog { DataContext = viewModel }));

        context.Subscriptions.Add(context.Commands.Register(NewProjectCommandId, _ =>
            OpenWizardAsync(context, DotNetTemplateWizardMode.Project)));

        context.Subscriptions.Add(context.Commands.Register(NewSolutionCommandId, _ =>
            OpenWizardAsync(context, DotNetTemplateWizardMode.Solution)));

        context.Subscriptions.Add(context.Commands.Register(NewFileCommandId, _ =>
            OpenWizardAsync(context, DotNetTemplateWizardMode.File)));

        ExtensionMenuContribution[] menuItems =
        {
            new(NewProjectCommandId, "New Project...", ExtensionMenuLocations.FileNew, "dotnet", 10),
            new(NewSolutionCommandId, "New Solution...", ExtensionMenuLocations.FileNew, "dotnet", 20),
            new(NewFileCommandId, "New File from Template...", ExtensionMenuLocations.FileNew, "dotnet", 30)
        };
        context.Subscriptions.Add(context.Contributions.RegisterMenuItems(context.ExtensionId, menuItems));

        ExtensionToolbarContribution[] toolbarItems =
        {
            new(NewProjectCommandId, "New Project", "Create a new project", ExtensionToolbarLocations.Main, "dotnet", 10)
        };
        context.Subscriptions.Add(context.Contributions.RegisterToolbarItems(context.ExtensionId, toolbarItems));

        ExtensionCommandPaletteContribution[] paletteItems =
        {
            new(NewProjectCommandId, "New Project...", "File"),
            new(NewSolutionCommandId, "New Solution...", "File"),
            new(NewFileCommandId, "New File from Template...", "File")
        };
        context.Subscriptions.Add(context.Contributions.RegisterCommandPaletteItems(context.ExtensionId, paletteItems));

        return Task.CompletedTask;
    }

    private async Task OpenWizardAsync(ExtensionContext context, DotNetTemplateWizardMode mode)
    {
        DotNetTemplateWizardViewModel viewModel = new(_templateService, mode, context.Settings);
        DotNetTemplateWizardResult? result = await context.DialogHost.ShowDialogAsync<DotNetTemplateWizardResult?>(
            WizardDialogId,
            viewModel,
            CancellationToken.None);

        if (result is null)
        {
            return;
        }

        if (mode == DotNetTemplateWizardMode.File)
        {
            string filePath = ResolveFilePath(result.ProjectPath, viewModel.Location, viewModel.ProjectName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                await context.Window.ShowErrorMessageAsync(
                    "Template creation did not return a file path.",
                    CancellationToken.None);
                return;
            }

            await context.Editor.OpenDocumentAsync(filePath, CancellationToken.None);
            return;
        }

        string workspacePath = result.SolutionPath ?? result.ProjectPath;
        WorkspaceOpenRequest request = new(workspacePath);
        WorkspaceOpenPromptDialogViewModel promptViewModel = new(request);

        WorkspaceOpenChoice? choice = await context.DialogHost.ShowDialogAsync<WorkspaceOpenChoice?>(
            WorkspacePromptDialogId,
            promptViewModel,
            CancellationToken.None);

        if (choice is null || choice == WorkspaceOpenChoice.Cancel)
        {
            return;
        }

        WorkspaceOpenMode openMode = choice == WorkspaceOpenChoice.OpenNewWindow
            ? WorkspaceOpenMode.NewWindow
            : WorkspaceOpenMode.CurrentWindow;

        await context.WorkspaceHost.OpenWorkspaceAsync(workspacePath, openMode, CancellationToken.None);
    }

    private static string ResolveFilePath(string? reportedPath, string location, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(reportedPath) && File.Exists(reportedPath))
        {
            return reportedPath;
        }

        if (!string.IsNullOrWhiteSpace(reportedPath) && Directory.Exists(reportedPath))
        {
            return FindNewestFile(reportedPath, fileName);
        }

        if (!string.IsNullOrWhiteSpace(location) && Directory.Exists(location))
        {
            return FindNewestFile(location, fileName);
        }

        return string.Empty;
    }

    private static string FindNewestFile(string root, string fileName)
    {
        try
        {
            IEnumerable<string> files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories);
            string? nameMatch = files.FirstOrDefault(file =>
                string.Equals(Path.GetFileNameWithoutExtension(file), fileName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(nameMatch))
            {
                return nameMatch;
            }

            return files
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
