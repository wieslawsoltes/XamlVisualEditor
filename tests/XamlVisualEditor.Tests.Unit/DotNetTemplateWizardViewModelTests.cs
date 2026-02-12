using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Threading.Tasks;
using Xunit;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.DotNetTemplatesExtension.ViewModels;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DotNetTemplateWizardViewModelTests
{
    [Fact]
    public void ProjectPathPreview_UsesSolutionRootWhenEnabled()
    {
        FakeTemplateService service = new();
        DotNetTemplateWizardViewModel viewModel = new(service, DotNetTemplateWizardMode.Project)
        {
            Location = "/tmp",
            ProjectName = "DemoApp",
            SolutionName = "DemoSolution",
            CreateSolution = true,
            CreateSolutionDirectory = true,
            CreateProjectDirectory = true
        };

        string preview = viewModel.ProjectPathPreview;
        Assert.Contains("DemoSolution", preview);
        Assert.Contains("DemoApp", preview);
    }

    [Fact]
    public async Task CreateCommand_UsesAllProjectRowsInSolution()
    {
        FakeTemplateService service = new();
        DotNetTemplateWizardViewModel viewModel = new(service, DotNetTemplateWizardMode.Project)
        {
            Location = "/tmp",
            ProjectName = "AppOne",
            SolutionName = "DemoSolution",
            CreateSolution = true,
            CreateSolutionDirectory = true,
            CreateProjectDirectory = true,
            SelectedTemplate = new DotNetTemplateListItemViewModel(new DotNetTemplateInfo
            {
                Name = "Console App",
                ShortName = "console"
            }),
            Step = DotNetTemplateWizardStep.Configure
        };

        viewModel.ProjectRows.Add(new DotNetProjectRowViewModel("AppTwo", true));

        await viewModel.CreateCommand.Execute().ToTask();

        Assert.NotNull(service.LastSolutionRequest);
        Assert.Equal(2, service.LastSolutionRequest!.Projects.Count);
        Assert.Equal("AppOne", service.LastSolutionRequest.Projects[0].ProjectName);
        Assert.Equal("AppTwo", service.LastSolutionRequest.Projects[1].ProjectName);
    }

    private sealed class FakeTemplateService : IDotNetTemplateService
    {
        public DotNetNewSolutionRequest? LastSolutionRequest { get; private set; }

        public Task<IReadOnlyList<DotNetTemplateInfo>> ListTemplatesAsync(CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<DotNetTemplateInfo>>(new List<DotNetTemplateInfo>());
        }

        public Task<DotNetTemplateInstallResult> InstallTemplateAsync(string packageOrPath, CancellationToken ct = default)
        {
            DotNetTemplateInstallResult result = new()
            {
                Success = true,
                StandardOutput = string.Empty
            };
            return Task.FromResult(result);
        }

        public Task<DotNetNewResult> CreateProjectAsync(DotNetNewProjectRequest request, CancellationToken ct = default)
        {
            DotNetNewResult result = new()
            {
                Success = true,
                ProjectPath = "/tmp/DemoApp/DemoApp.csproj"
            };
            return Task.FromResult(result);
        }

        public Task<DotNetNewResult> CreateSolutionAsync(DotNetNewSolutionRequest request, CancellationToken ct = default)
        {
            LastSolutionRequest = request;
            DotNetNewResult result = new()
            {
                Success = true,
                ProjectPath = "/tmp/DemoApp/DemoApp.csproj",
                SolutionPath = "/tmp/DemoSolution/DemoSolution.sln"
            };
            return Task.FromResult(result);
        }
    }
}
