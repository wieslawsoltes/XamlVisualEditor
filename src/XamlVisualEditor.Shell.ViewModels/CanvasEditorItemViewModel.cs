using System;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// Legacy canvas item view model retained for drag/resize behaviors.
/// </summary>
public sealed class CanvasEditorItemViewModel : ReactiveObject, IDisposable
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
    public double X { get; set; }

    [Reactive]
    public double Y { get; set; }

    [Reactive]
    public double Width { get; set; } = 520;

    [Reactive]
    public double Height { get; set; } = 360;

    public void Dispose()
    {
        if (IsOwned)
        {
            Document.Dispose();
        }
    }
}
