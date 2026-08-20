using System;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// Legacy canvas item view model retained for drag/resize behaviors.
/// </summary>
public sealed partial class CanvasEditorItemViewModel : ReactiveObject, IDisposable
{
    public CanvasEditorItemViewModel(IEditorDocumentViewModel document, bool isOwned)
    {
        Document = document;
        IsOwned = isOwned;
    }

    public IEditorDocumentViewModel Document { get; }

    public bool IsOwned { get; }

    public string FilePath => Document.FilePath;

    public string FileName => Document.FileName;

    [Reactive]
    public partial double X { get; set; }

    [Reactive]
    public partial double Y { get; set; }

    [Reactive]
    public partial double Width { get; set; } = 520;

    [Reactive]
    public partial double Height { get; set; } = 360;

    public void Dispose()
    {
        if (IsOwned)
        {
            Document.Dispose();
        }
    }
}
