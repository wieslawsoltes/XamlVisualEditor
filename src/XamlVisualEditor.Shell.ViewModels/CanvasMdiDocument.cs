using System.Runtime.Serialization;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.Shell;

/// <summary>
/// Dock document for the MDI canvas.
/// </summary>
public sealed class CanvasMdiDocument : Document
{
    [IgnoreDataMember]
    public IEditorDocumentViewModel DocumentViewModel { get; }

    [IgnoreDataMember]
    public bool IsOwned { get; }

    [Reactive]
    [IgnoreDataMember]
    public bool IsTransient { get; set; }

    public string FilePath => DocumentViewModel.FilePath;

    public string FileName => DocumentViewModel.FileName;

    public CanvasMdiDocument(IEditorDocumentViewModel documentViewModel, bool isOwned)
    {
        DocumentViewModel = documentViewModel;
        IsOwned = isOwned;
        Id = documentViewModel.FilePath;
        Title = documentViewModel.FileName;
        CanClose = true;
    }
}
