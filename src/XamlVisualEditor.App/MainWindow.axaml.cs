using System.Diagnostics;
using System.Reactive;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using ReactiveUI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.App.Views;

namespace XamlVisualEditor.App;

/// <summary>
/// Main application window.
/// </summary>
public sealed partial class MainWindow : Window
{
    private IDisposable? _openFileHandler;
    private IDisposable? _saveFileHandler;
    private IDisposable? _renameSymbolHandler;
    private IDisposable? _definitionPickerHandler;
    private IDisposable? _codeActionPickerHandler;
    private IDisposable? _workspaceSymbolQueryHandler;
    private IDisposable? _commandPaletteHandler;
    private IDisposable? _extensionPackageOpenHandler;
    private IDisposable? _previewerTrustHandler;
    private IDisposable? _debugToolConsentHandler;

    public MainWindow()
        : this(null)
    {
    }

    public MainWindow(MainWindowViewModel? viewModel)
    {
        InitializeComponent();

        // Register interaction handlers when DataContext is set
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                RegisterInteractions(vm);
            }
        };

        // Dispose handlers when window is closing
        Closing += (_, _) =>
        {
            _openFileHandler?.Dispose();
            _saveFileHandler?.Dispose();
            _renameSymbolHandler?.Dispose();
            _definitionPickerHandler?.Dispose();
            _codeActionPickerHandler?.Dispose();
            _workspaceSymbolQueryHandler?.Dispose();
            _commandPaletteHandler?.Dispose();
            _extensionPackageOpenHandler?.Dispose();
            _previewerTrustHandler?.Dispose();
            _debugToolConsentHandler?.Dispose();
        };

        if (viewModel is not null)
        {
            DataContext = viewModel;
        }
        else if (DataContext is MainWindowViewModel vm)
        {
            RegisterInteractions(vm);
        }
    }

    private void RegisterInteractions(MainWindowViewModel vm)
    {
        // Dispose previous handlers
        _openFileHandler?.Dispose();
        _saveFileHandler?.Dispose();
        _renameSymbolHandler?.Dispose();
        _definitionPickerHandler?.Dispose();
        _codeActionPickerHandler?.Dispose();
        _workspaceSymbolQueryHandler?.Dispose();
        _commandPaletteHandler?.Dispose();
        _extensionPackageOpenHandler?.Dispose();
        _extensionPackageOpenHandler?.Dispose();
        _previewerTrustHandler?.Dispose();
        _debugToolConsentHandler?.Dispose();

        // Open file dialog interaction
        _openFileHandler = vm.OpenFileInteraction.RegisterHandler(async interaction =>
        {
            FilePickerOpenOptions options = new()
            {
                Title = "Open XAML, Project, or Solution",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("All Supported")
                    {
                        Patterns = new[] { "*.axaml", "*.xaml", "*.csproj", "*.sln", "*.slnx" }
                    },
                    new FilePickerFileType("XAML Files") { Patterns = new[] { "*.axaml", "*.xaml" } },
                    new FilePickerFileType("Projects") { Patterns = new[] { "*.csproj" } },
                    new FilePickerFileType("Solutions") { Patterns = new[] { "*.sln", "*.slnx" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count > 0)
            {
                interaction.SetOutput(files[0].Path.LocalPath);
            }
            else
            {
                interaction.SetOutput(null);
            }
        });

        // Save file dialog interaction
        _saveFileHandler = vm.SaveFileInteraction.RegisterHandler(async interaction =>
        {
            FilePickerSaveOptions options = new()
            {
                Title = "Save XAML File",
                DefaultExtension = ".axaml",
                SuggestedFileName = interaction.Input,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("XAML Files") { Patterns = new[] { "*.axaml", "*.xaml" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            };

            var file = await StorageProvider.SaveFilePickerAsync(options);
            interaction.SetOutput(file?.Path.LocalPath);
        });

        _renameSymbolHandler = vm.RenameSymbolInteraction.RegisterHandler(async interaction =>
        {
            LanguageRenameInfo info = interaction.Input;
            RenameSymbolDialogViewModel dialogVm = new(
                "Rename Symbol",
                "New name:",
                info.Name);
            RenameSymbolDialog dialog = new()
            {
                DataContext = dialogVm
            };

            string? result = await dialog.ShowDialog<string?>(this);
            interaction.SetOutput(result);
        });

        _definitionPickerHandler = vm.SelectDefinitionInteraction.RegisterHandler(async interaction =>
        {
            DefinitionPickerDialogViewModel dialogVm = new(interaction.Input);
            DefinitionPickerDialog dialog = new()
            {
                DataContext = dialogVm
            };

            ReferenceLocationViewModel? result = await dialog.ShowDialog<ReferenceLocationViewModel?>(this);
            interaction.SetOutput(result);
        });

        _codeActionPickerHandler = vm.SelectCodeActionInteraction.RegisterHandler(async interaction =>
        {
            CodeActionPickerDialogViewModel dialogVm = new(interaction.Input);
            CodeActionPickerDialog dialog = new()
            {
                DataContext = dialogVm
            };

            LanguageCodeAction? result = await dialog.ShowDialog<LanguageCodeAction?>(this);
            interaction.SetOutput(result);
        });

        _workspaceSymbolQueryHandler = vm.WorkspaceSymbolQueryInteraction.RegisterHandler(async interaction =>
        {
            RenameSymbolDialogViewModel dialogVm = new(
                "Workspace Symbols",
                "Search:",
                interaction.Input ?? string.Empty);
            RenameSymbolDialog dialog = new()
            {
                DataContext = dialogVm
            };

            string? result = await dialog.ShowDialog<string?>(this);
            interaction.SetOutput(result);
        });

        _commandPaletteHandler = vm.CommandPaletteInteraction.RegisterHandler(async interaction =>
        {
            CommandPaletteDialogViewModel dialogVm = new(interaction.Input);
            CommandPaletteDialog dialog = new()
            {
                DataContext = dialogVm
            };

            ExtensionCommandPaletteItemViewModel? result =
                await dialog.ShowDialog<ExtensionCommandPaletteItemViewModel?>(this);
            interaction.SetOutput(result);
        });

        _extensionPackageOpenHandler = vm.ExtensionPackageOpenInteraction.RegisterHandler(async interaction =>
        {
            FilePickerOpenOptions options = new()
            {
                Title = "Install Extension Package",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("NuGet Package") { Patterns = new[] { "*.nupkg" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                }
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
            if (files.Count > 0)
            {
                interaction.SetOutput(files[0].Path.LocalPath);
            }
            else
            {
                interaction.SetOutput(null);
            }
        });

        _previewerTrustHandler = vm.PreviewerTrustInteraction.RegisterHandler(async interaction =>
        {
            PreviewerTrustDialogViewModel dialogVm = new(interaction.Input);
            PreviewerTrustDialog dialog = new()
            {
                DataContext = dialogVm
            };

            PreviewerTrustDecision result = await dialog.ShowDialog<PreviewerTrustDecision>(this);
            interaction.SetOutput(result);
        });

        _debugToolConsentHandler = vm.DebugToolConsentInteraction.RegisterHandler(async interaction =>
        {
            DebugToolConsentDialogViewModel dialogVm = new(interaction.Input);
            DebugToolConsentDialog dialog = new()
            {
                DataContext = dialogVm
            };

            bool result = await dialog.ShowDialog<bool>(this);
            interaction.SetOutput(result);
        });

    }
}
