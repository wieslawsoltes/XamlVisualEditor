using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.DotNetTemplatesExtension.ViewModels;

public enum DotNetTemplateWizardMode
{
    Project,
    Solution,
    File
}

public enum DotNetTemplateWizardStep
{
    Template,
    Configure
}

public sealed class DotNetTemplateWizardResult
{
    public DotNetTemplateWizardResult(string? solutionPath, string projectPath)
    {
        SolutionPath = solutionPath;
        ProjectPath = projectPath;
    }

    public string? SolutionPath { get; }

    public string ProjectPath { get; }
}

public sealed class WorkspaceOpenRequest
{
    public WorkspaceOpenRequest(string workspacePath)
    {
        WorkspacePath = workspacePath;
        WorkspaceName = Path.GetFileNameWithoutExtension(workspacePath);
    }

    public string WorkspacePath { get; }

    public string WorkspaceName { get; }
}

public enum WorkspaceOpenChoice
{
    OpenCurrent,
    OpenNewWindow,
    Cancel
}

public sealed class DotNetTemplateListItemViewModel : ReactiveObject
{
    public DotNetTemplateListItemViewModel(DotNetTemplateInfo template)
    {
        Template = template;
    }

    public DotNetTemplateInfo Template { get; }

    public string Name => Template.Name;

    public string ShortName => Template.ShortName;

    public string Language
    {
        get
        {
            IReadOnlyList<string> languages = DotNetTemplateWizardViewModel.GetTemplateLanguages(Template);
            return languages.Count == 0 ? "Any" : string.Join(", ", languages);
        }
    }

    public string Type => string.IsNullOrWhiteSpace(Template.Type) ? "Template" : Template.Type;

    public string Author => Template.Author ?? string.Empty;

    public string Description => Template.Description ?? string.Empty;

    public string TagSummary
    {
        get
        {
            if (Template.Tags.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", Template.Tags.Select(pair => $"{pair.Key}: {pair.Value}"));
        }
    }
}

public sealed class DotNetProjectRowViewModel : ReactiveObject
{
    public DotNetProjectRowViewModel(string projectName, bool createDirectory)
    {
        ProjectName = projectName;
        CreateProjectDirectory = createDirectory;
    }

    [Reactive]
    public string ProjectName { get; set; }

    [Reactive]
    public bool CreateProjectDirectory { get; set; }
}

public sealed class DotNetTemplateWizardViewModel : ReactiveObject
{
    private const string LastLocationKey = "dotnetTemplates.lastLocation";
    private readonly IDotNetTemplateService _templateService;
    private readonly ISettings? _settings;
    private readonly ObservableCollection<DotNetTemplateListItemViewModel> _allTemplates = new();
    private bool _isLoadingTemplates;
    private bool _suppressPrimaryProjectSync;
    private IDisposable? _primaryRowSubscription;
    private bool _suppressAutoFileName;
    private bool _useAutoFileName = true;
    private string? _autoFileName;

    public DotNetTemplateWizardViewModel(
        IDotNetTemplateService templateService,
        DotNetTemplateWizardMode mode,
        ISettings? settings = null)
    {
        _templateService = templateService;
        _settings = settings;
        Mode = mode;

        UpdateTitle();

        SearchText = string.Empty;
        ProjectName = mode == DotNetTemplateWizardMode.File ? "MyFile" : "MyProject";
        SolutionName = ProjectName;
        Location = GetInitialLocation();
        CreateProjectDirectory = mode != DotNetTemplateWizardMode.File;
        CreateSolutionDirectory = true;
        CreateSolution = mode == DotNetTemplateWizardMode.Solution;
        AddProjectToSolution = mode == DotNetTemplateWizardMode.Solution;

        ProjectRows = new ObservableCollection<DotNetProjectRowViewModel>
        {
            new DotNetProjectRowViewModel(ProjectName, CreateProjectDirectory)
        };
        SelectedProjectRow = ProjectRows[0];

        IObservable<bool> hasTemplate = this.WhenAnyValue(x => x.SelectedTemplate)
            .Select(template => template is not null);
        IObservable<bool> notBusy = this.WhenAnyValue(x => x.IsBusy)
            .Select(busy => !busy);

        NextCommand = ReactiveCommand.Create(
            () =>
            {
                Step = DotNetTemplateWizardStep.Configure;
            },
            hasTemplate.CombineLatest(notBusy, (has, ready) => has && ready));

        BackCommand = ReactiveCommand.Create(
            () =>
            {
                Step = DotNetTemplateWizardStep.Template;
            },
            this.WhenAnyValue(x => x.Step)
                .Select(step => step == DotNetTemplateWizardStep.Configure));

        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await CloseInteraction.Handle(null);
        });

        RefreshTemplatesCommand = ReactiveCommand.CreateFromTask(ct => LoadTemplatesAsync(ct), notBusy);

        InstallTemplateCommand = ReactiveCommand.CreateFromTask(
            ct => InstallTemplateAsync(ct),
            this.WhenAnyValue(x => x.InstallTemplateInput, x => x.IsBusy, (input, busy) =>
                !busy && !string.IsNullOrWhiteSpace(input)));

        BrowseLocationCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            string? path = await SelectFolderInteraction.Handle(Unit.Default);
            if (!string.IsNullOrWhiteSpace(path))
            {
                Location = path;
            }
        });

        AddProjectRowCommand = ReactiveCommand.Create(AddProjectRow,
            this.WhenAnyValue(x => x.IsSolutionSetup));

        IObservable<int> projectCount = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => ProjectRows.CollectionChanged += h,
                h => ProjectRows.CollectionChanged -= h)
            .Select(_ => ProjectRows.Count)
            .StartWith(ProjectRows.Count);

        RemoveProjectRowCommand = ReactiveCommand.Create(RemoveSelectedProjectRow,
            this.WhenAnyValue(x => x.SelectedProjectRow, x => x.IsSolutionSetup)
                .CombineLatest(projectCount, (tuple, count) =>
                    tuple.Item2 && tuple.Item1 is not null && count > 1));

        IObservable<bool> canCreate = Observable.CombineLatest(
            this.WhenAnyValue(x => x.SelectedTemplate),
            this.WhenAnyValue(x => x.ProjectName),
            this.WhenAnyValue(x => x.Location),
            this.WhenAnyValue(x => x.IsBusy),
            this.WhenAnyValue(x => x.Step),
            this.WhenAnyValue(x => x.IsSolutionSetup),
            (template, name, location, busy, step, isSolution) =>
                template is not null
                && step == DotNetTemplateWizardStep.Configure
                && !busy
                && !string.IsNullOrWhiteSpace(location)
                && (isSolution ? AreProjectRowsValid() : IsValidName(name)));

        CreateCommand = ReactiveCommand.CreateFromTask(ct => CreateAsync(ct), canCreate);

        this.WhenAnyValue(x => x.SearchText, x => x.SelectedLanguage)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());

        this.WhenAnyValue(x => x.Step)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsTemplateStep));
                this.RaisePropertyChanged(nameof(IsConfigureStep));
                UpdateTitle();
            });

        this.WhenAnyValue(x => x.CreateSolution)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(IsSolutionSetup)));

        ProjectRows.CollectionChanged += (_, _) =>
        {
            this.RaisePropertyChanged(nameof(ProjectCountDisplay));
            if (ProjectRows.Count > 0)
            {
                SyncPrimaryProjectFromRows(ProjectRows[0].ProjectName);
            }
            UpdatePrimaryRowSubscription();
        };

        UpdatePrimaryRowSubscription();

        this.WhenAnyValue(x => x.ProjectName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Subscribe(name =>
            {
                SyncPrimaryProjectToRows(name);
                if (Mode == DotNetTemplateWizardMode.Solution && string.IsNullOrWhiteSpace(SolutionName))
                {
                    SolutionName = name;
                }

                if (IsFileWizard && !_suppressAutoFileName)
                {
                    _useAutoFileName = string.Equals(name, _autoFileName, StringComparison.Ordinal);
                }
            });

        this.WhenAnyValue(x => x.SelectedTemplate)
            .Where(_ => IsFileWizard)
            .Subscribe(template => ApplySuggestedFileName(template));

        this.WhenAnyValue(x => x.CreateProjectDirectory)
            .Subscribe(value =>
            {
                if (ProjectRows.Count > 0)
                {
                    ProjectRows[0].CreateProjectDirectory = value;
                }
            });

        this.WhenAnyValue(x => x.Location)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.TaskpoolScheduler)
            .Subscribe(path =>
            {
                if (_settings is null || string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                _ = _settings.UpdateAsync(LastLocationKey, path, SettingsTarget.User, CancellationToken.None);
            });

        this.WhenAnyValue(
                x => x.ProjectName,
                x => x.Location,
                x => x.CreateProjectDirectory,
                x => x.CreateSolution,
                x => x.SolutionName,
                x => x.CreateSolutionDirectory)
            .Select(_ => BuildProjectPathPreview())
            .ToProperty(this, x => x.ProjectPathPreview, out _projectPathPreview);

        this.WhenAnyValue(
                x => x.Location,
                x => x.SolutionName,
                x => x.CreateSolutionDirectory,
                x => x.CreateSolution)
            .Select(_ => BuildSolutionPathPreview())
            .ToProperty(this, x => x.SolutionPathPreview, out _solutionPathPreview);

        _ = LoadTemplatesAsync(CancellationToken.None);
    }

    [Reactive]
    public string Title { get; private set; } = string.Empty;

    public DotNetTemplateWizardMode Mode { get; }

    [Reactive]
    public DotNetTemplateWizardStep Step { get; set; } = DotNetTemplateWizardStep.Template;

    [Reactive]
    public string SearchText { get; set; }

    [Reactive]
    public string? SelectedLanguage { get; set; }

    [Reactive]
    public DotNetTemplateListItemViewModel? SelectedTemplate { get; set; }

    [Reactive]
    public string ProjectName { get; set; }

    [Reactive]
    public string SolutionName { get; set; }

    [Reactive]
    public string Location { get; set; }

    [Reactive]
    public bool CreateProjectDirectory { get; set; }

    [Reactive]
    public bool CreateSolutionDirectory { get; set; }

    [Reactive]
    public bool CreateSolution { get; set; }

    [Reactive]
    public bool AddProjectToSolution { get; set; }

    [Reactive]
    public string? InstallTemplateInput { get; set; }

    [Reactive]
    public bool IsBusy { get; private set; }

    [Reactive]
    public string? StatusMessage { get; private set; }

    [Reactive]
    public string? ErrorMessage { get; private set; }

    public ObservableCollection<string> Languages { get; } = new();

    public ObservableCollection<DotNetTemplateListItemViewModel> Templates { get; } = new();

    public ObservableCollection<DotNetProjectRowViewModel> ProjectRows { get; }

    [Reactive]
    public DotNetProjectRowViewModel? SelectedProjectRow { get; set; }

    public bool IsTemplateStep => Step == DotNetTemplateWizardStep.Template;

    public bool IsConfigureStep => Step == DotNetTemplateWizardStep.Configure;

    public bool CanToggleSolution => Mode == DotNetTemplateWizardMode.Project;

    public bool IsSolutionSetup => CreateSolution || Mode == DotNetTemplateWizardMode.Solution;

    public bool IsFileWizard => Mode == DotNetTemplateWizardMode.File;

    public bool ShowProjectDirectoryOption => !IsFileWizard;

    public string PrimaryNameLabel => IsFileWizard ? "File name" : "Project name";

    public string PrimaryPathPreviewLabel => IsFileWizard ? "File path preview" : "Project path preview";

    public string ProjectCountDisplay => $"Projects: {ProjectRows.Count}";

    public string ProjectPathPreview => _projectPathPreview.Value;

    public string SolutionPathPreview => _solutionPathPreview.Value;

    public Interaction<DotNetTemplateWizardResult?, Unit> CloseInteraction { get; } = new();

    public Interaction<Unit, string?> SelectFolderInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> NextCommand { get; }

    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshTemplatesCommand { get; }

    public ReactiveCommand<Unit, Unit> InstallTemplateCommand { get; }

    public ReactiveCommand<Unit, Unit> BrowseLocationCommand { get; }

    public ReactiveCommand<Unit, Unit> AddProjectRowCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveProjectRowCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    private readonly ObservableAsPropertyHelper<string> _projectPathPreview;
    private readonly ObservableAsPropertyHelper<string> _solutionPathPreview;

    public async System.Threading.Tasks.Task LoadTemplatesAsync(CancellationToken ct)
    {
        if (_isLoadingTemplates)
        {
            return;
        }

        _isLoadingTemplates = true;
        IsBusy = true;
        StatusMessage = "Loading templates...";
        ErrorMessage = null;

        try
        {
            IReadOnlyList<DotNetTemplateInfo> templates = await _templateService.ListTemplatesAsync(ct);
            List<DotNetTemplateListItemViewModel> items = templates
                .Select(template => new DotNetTemplateListItemViewModel(template))
                .ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allTemplates.Clear();
                foreach (DotNetTemplateListItemViewModel item in items)
                {
                    _allTemplates.Add(item);
                }

                ApplyFilter();

                Languages.Clear();
                Languages.Add("All");
                foreach (string language in GetTemplatesForMode(_allTemplates)
                             .SelectMany(item => GetTemplateLanguages(item.Template))
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .OrderBy(lang => lang, StringComparer.OrdinalIgnoreCase))
                {
                    Languages.Add(language);
                }

                SelectedLanguage ??= "All";
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
            _isLoadingTemplates = false;
        }
    }

    private void ApplyFilter()
    {
        Templates.Clear();
        string? language = SelectedLanguage;
        string search = SearchText?.Trim() ?? string.Empty;
        IEnumerable<DotNetTemplateListItemViewModel> filtered = GetTemplatesForMode(_allTemplates);

        if (!string.IsNullOrWhiteSpace(language) && !string.Equals(language, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(item =>
                GetTemplateLanguages(item.Template).Any(lang =>
                    string.Equals(lang, language, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(item =>
                item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.ShortName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                || item.TagSummary.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        foreach (DotNetTemplateListItemViewModel item in filtered)
        {
            Templates.Add(item);
        }

        if (SelectedTemplate is null || !Templates.Contains(SelectedTemplate))
        {
            SelectedTemplate = Templates.FirstOrDefault();
        }
    }

    private async System.Threading.Tasks.Task InstallTemplateAsync(CancellationToken ct)
    {
        string? input = InstallTemplateInput?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Installing template...";

        try
        {
            DotNetTemplateInstallResult result = await _templateService.InstallTemplateAsync(input, ct);
            if (!result.Success)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Template installation failed."
                    : result.ErrorMessage;
                return;
            }

            InstallTemplateInput = string.Empty;
            await LoadTemplatesAsync(ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    private async System.Threading.Tasks.Task CreateAsync(CancellationToken ct)
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        if (!IsSolutionSetup && !IsValidName(ProjectName))
        {
            ErrorMessage = "Project name is invalid.";
            return;
        }

        if (IsSolutionSetup && !AreProjectRowsValid())
        {
            ErrorMessage = "Please provide valid project names.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = Mode == DotNetTemplateWizardMode.File
            ? "Creating file..."
            : "Creating project...";

        try
        {
            DotNetNewResult result;
            if (IsSolutionSetup)
            {
                DotNetNewSolutionRequest request = BuildSolutionRequest();
                result = await _templateService.CreateSolutionAsync(request, ct);
            }
            else
            {
                DotNetNewProjectRequest request = BuildProjectRequest();
                result = await _templateService.CreateProjectAsync(request, ct);
            }

            if (!result.Success)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Template creation failed."
                    : result.ErrorMessage;
                return;
            }

            string? primaryPath = result.ProjectPath ?? result.SolutionPath;
            if (string.IsNullOrWhiteSpace(primaryPath))
            {
                ErrorMessage = "Template creation succeeded but no output path was returned.";
                return;
            }

            DotNetTemplateWizardResult wizardResult = new(result.SolutionPath, primaryPath);
            await CloseInteraction.Handle(wizardResult);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    private DotNetNewProjectRequest BuildProjectRequest()
    {
        string projectName = ProjectName.Trim();

        return new DotNetNewProjectRequest
        {
            TemplateShortName = SelectedTemplate?.Template.ShortName ?? string.Empty,
            ProjectName = projectName,
            Location = Location,
            CreateProjectDirectory = Mode != DotNetTemplateWizardMode.File && CreateProjectDirectory,
            Parameters = BuildTemplateParameters()
        };
    }

    private DotNetNewSolutionRequest BuildSolutionRequest()
    {
        string solutionRoot = BuildSolutionRoot();
        string solutionName = SolutionName.Trim();

        List<DotNetNewProjectRequest> projects = new();
        foreach (DotNetProjectRowViewModel row in ProjectRows)
        {
            string projectName = row.ProjectName.Trim();
            projects.Add(new DotNetNewProjectRequest
            {
                TemplateShortName = SelectedTemplate?.Template.ShortName ?? string.Empty,
                ProjectName = projectName,
                Location = solutionRoot,
                CreateProjectDirectory = row.CreateProjectDirectory,
                Parameters = BuildTemplateParameters()
            });
        }

        return new DotNetNewSolutionRequest
        {
            SolutionName = solutionName,
            Location = Location,
            CreateSolutionDirectory = CreateSolutionDirectory,
            AddProjectsToSolution = AddProjectToSolution,
            Projects = projects
        };
    }

    private string BuildSolutionRoot()
    {
        string solutionName = SolutionName.Trim();
        if (CreateSolutionDirectory)
        {
            return Path.Combine(Location, solutionName);
        }

        return Location;
    }

    private IReadOnlyDictionary<string, string> BuildTemplateParameters()
    {
        Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase);

        if (IsFileWizard)
        {
            parameters["force"] = string.Empty;
        }

        string? selectedLanguage = SelectedLanguage;
        if (!string.IsNullOrWhiteSpace(selectedLanguage)
            && !string.Equals(selectedLanguage, "All", StringComparison.OrdinalIgnoreCase))
        {
            parameters["language"] = selectedLanguage;
            return parameters;
        }

        if (SelectedTemplate is null)
        {
            return parameters;
        }

        IReadOnlyList<string> templateLanguages = GetTemplateLanguages(SelectedTemplate.Template);
        if (templateLanguages.Count == 1)
        {
            parameters["language"] = templateLanguages[0];
        }

        return parameters;
    }

    internal static IReadOnlyList<string> GetTemplateLanguages(DotNetTemplateInfo template)
    {
        string? language = template.Language;
        if (string.IsNullOrWhiteSpace(language)
            && template.Tags.TryGetValue("language", out string? tagLanguage))
        {
            language = tagLanguage;
        }

        if (string.IsNullOrWhiteSpace(language))
        {
            return Array.Empty<string>();
        }

        string cleaned = language.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);

        string[] parts = cleaned.Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);
        List<string> languages = new();
        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                languages.Add(trimmed);
            }
        }

        return languages.Count == 0
            ? Array.Empty<string>()
            : languages;
    }

    private string BuildProjectPathPreview()
    {
        if (Mode == DotNetTemplateWizardMode.File)
        {
            return Path.Combine(Location, ProjectName);
        }

        if (!IsSolutionSetup)
        {
            return CreateProjectDirectory
                ? Path.Combine(Location, ProjectName)
                : Location;
        }

        string solutionRoot = BuildSolutionRoot();
        return Path.Combine(solutionRoot, ProjectName);
    }

    private string BuildSolutionPathPreview()
    {
        if (!IsSolutionSetup)
        {
            return string.Empty;
        }

        string solutionRoot = BuildSolutionRoot();
        return Path.Combine(solutionRoot, SolutionName + ".sln");
    }

    private bool AreProjectRowsValid()
    {
        if (ProjectRows.Count == 0)
        {
            return false;
        }

        foreach (DotNetProjectRowViewModel row in ProjectRows)
        {
            if (!IsValidName(row.ProjectName))
            {
                return false;
            }
        }

        return true;
    }

    private void AddProjectRow()
    {
        string name = ProjectName;
        if (!IsValidName(name))
        {
            name = "Project" + (ProjectRows.Count + 1);
        }

        DotNetProjectRowViewModel row = new(name, CreateProjectDirectory);
        ProjectRows.Add(row);
        SelectedProjectRow = row;
    }

    private void RemoveSelectedProjectRow()
    {
        if (SelectedProjectRow is null)
        {
            return;
        }

        DotNetProjectRowViewModel row = SelectedProjectRow;
        ProjectRows.Remove(row);
        SelectedProjectRow = ProjectRows.Count > 0 ? ProjectRows[^1] : null;
    }

    private void SyncPrimaryProjectToRows(string name)
    {
        if (_suppressPrimaryProjectSync)
        {
            return;
        }

        _suppressPrimaryProjectSync = true;
        if (ProjectRows.Count > 0)
        {
            ProjectRows[0].ProjectName = name;
        }
        _suppressPrimaryProjectSync = false;
    }

    private void SyncPrimaryProjectFromRows(string name)
    {
        if (_suppressPrimaryProjectSync)
        {
            return;
        }

        _suppressPrimaryProjectSync = true;
        ProjectName = name;
        _suppressPrimaryProjectSync = false;
    }

    private void UpdatePrimaryRowSubscription()
    {
        _primaryRowSubscription?.Dispose();
        if (ProjectRows.Count == 0)
        {
            return;
        }

        DotNetProjectRowViewModel primary = ProjectRows[0];
        _primaryRowSubscription = primary.WhenAnyValue(x => x.ProjectName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Subscribe(SyncPrimaryProjectFromRows);
    }

    private static bool IsValidName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        return name.IndexOfAny(invalid) < 0;
    }

    private string GetInitialLocation()
    {
        if (_settings is not null)
        {
            string? stored = _settings.Get<string>(LastLocationKey);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }
        }

        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(documents))
        {
            return documents;
        }

        return Environment.CurrentDirectory;
    }

    private IEnumerable<DotNetTemplateListItemViewModel> GetTemplatesForMode(
        IEnumerable<DotNetTemplateListItemViewModel> templates)
    {
        return templates;
    }

    private static bool IsItemTemplate(DotNetTemplateInfo template)
    {
        string? type = template.Type;
        if (string.IsNullOrWhiteSpace(type) && template.Tags.TryGetValue("type", out string? tagType))
        {
            type = tagType;
        }

        if (string.IsNullOrWhiteSpace(type) && template.Tags.TryGetValue("tags", out string? tagList))
        {
            if (tagList.Contains("item", StringComparison.OrdinalIgnoreCase))
            {
                type = "item";
            }
        }

        return string.Equals(type, "item", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplySuggestedFileName(DotNetTemplateListItemViewModel? template)
    {
        if (template is null)
        {
            return;
        }

        string suggestion = GetSuggestedFileBaseName(template.Template);
        if (!_useAutoFileName && !string.IsNullOrWhiteSpace(ProjectName))
        {
            _autoFileName = suggestion;
            return;
        }

        _suppressAutoFileName = true;
        ProjectName = suggestion;
        _autoFileName = suggestion;
        _useAutoFileName = true;
        _suppressAutoFileName = false;
    }

    private static string GetSuggestedFileBaseName(DotNetTemplateInfo template)
    {
        string source = string.Join(" ", template.ShortName, template.Name).ToLowerInvariant();

        if (source.Contains("interface", StringComparison.Ordinal))
        {
            return "IInterface1";
        }

        if (source.Contains("record", StringComparison.Ordinal))
        {
            return "Record1";
        }

        if (source.Contains("struct", StringComparison.Ordinal))
        {
            return "Struct1";
        }

        if (source.Contains("enum", StringComparison.Ordinal))
        {
            return "Enum1";
        }

        if (source.Contains("delegate", StringComparison.Ordinal))
        {
            return "Delegate1";
        }

        if (source.Contains("component", StringComparison.Ordinal))
        {
            return "Component1";
        }

        if (source.Contains("class", StringComparison.Ordinal))
        {
            return "Class1";
        }

        return "MyFile";
    }

    private void UpdateTitle()
    {
        string baseTitle = Mode switch
        {
            DotNetTemplateWizardMode.Solution => "New Solution",
            DotNetTemplateWizardMode.File => "New File",
            _ => "New Project"
        };

        string stepTitle = Step == DotNetTemplateWizardStep.Template
            ? "Select Template"
            : "Configure";

        Title = baseTitle + " - " + stepTitle;
    }
}

public sealed class WorkspaceOpenPromptDialogViewModel : ReactiveObject
{
    public WorkspaceOpenPromptDialogViewModel(WorkspaceOpenRequest request)
    {
        Title = "Open Workspace";
        Message = $"Open '{request.WorkspaceName}' now?";

        OpenCurrentCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await CloseInteraction.Handle(WorkspaceOpenChoice.OpenCurrent);
        });

        OpenNewWindowCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await CloseInteraction.Handle(WorkspaceOpenChoice.OpenNewWindow);
        });

        CancelCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            await CloseInteraction.Handle(WorkspaceOpenChoice.Cancel);
        });
    }

    public string Title { get; }

    public string Message { get; }

    public Interaction<WorkspaceOpenChoice, Unit> CloseInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> OpenCurrentCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenNewWindowCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
}
