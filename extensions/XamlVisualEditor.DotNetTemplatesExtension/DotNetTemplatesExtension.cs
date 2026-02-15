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
    private const string NewProjectIconPath =
        "M4.5 3A2.5 2.5 0 0 0 2 5.5v9A2.5 2.5 0 0 0 4.5 17h5.1c-.16-.32-.3-.65-.4-1H4.5A1.5 1.5 0 0 1 3 14.5V8h4.09c.4 0 .78-.16 1.06-.44L9.7 6h5.79c.83 0 1.5.67 1.5 1.5v2.1c.36.18.7.4 1 .66V7.5A2.5 2.5 0 0 0 15.5 5H9.7L8.23 3.51A1.75 1.75 0 0 0 6.98 3H4.5ZM3 5.5C3 4.67 3.67 4 4.5 4h2.48c.2 0 .4.08.53.22L8.8 5.5 7.44 6.85a.5.5 0 0 1-.35.15H3V5.5Zm16 9a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0Zm-4-2a.5.5 0 0 0-1 0V14h-1.5a.5.5 0 0 0 0 1H14v1.5a.5.5 0 0 0 1 0V15h1.5a.5.5 0 0 0 0-1H15v-1.5Z";

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
            new(
                NewProjectCommandId,
                "New Project",
                "Create a new project",
                ExtensionToolbarLocations.Main,
                "dotnet",
                10,
                NewProjectIconPath)
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
