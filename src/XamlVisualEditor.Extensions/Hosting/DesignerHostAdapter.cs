namespace XamlVisualEditor.Extensions.Hosting;

/// <summary>Default designer host adapter for extension context.</summary>
public sealed class DesignerHostAdapter : IDesignerHost, IDisposable
{
    private readonly IEditorServices _editorServices;

    public DesignerHostAdapter(IEditorServices editorServices)
    {
        _editorServices = editorServices;
        _editorServices.ActiveDocumentChanged += OnActiveDocumentChanged;
    }

    public string? ActiveDocumentPath => _editorServices.ActiveDocument?.FilePath;

    public event EventHandler<DesignerDocumentChangedEventArgs>? ActiveDocumentChanged;

#pragma warning disable CS0067
    public event EventHandler<DesignerSelectionChangedEventArgs>? SelectionChanged;
#pragma warning restore CS0067

    public Task<IReadOnlyList<DesignerNodeSummary>> GetSelectedNodesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DesignerNodeSummary>>(Array.Empty<DesignerNodeSummary>());
    }

    public Task<IReadOnlyList<DesignerNodeSummary>> GetVisualTreeAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DesignerNodeSummary>>(Array.Empty<DesignerNodeSummary>());
    }

    public Task<IReadOnlyList<DesignerNodeSummary>> GetLogicalTreeAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DesignerNodeSummary>>(Array.Empty<DesignerNodeSummary>());
    }

    public Task<IReadOnlyList<DesignerPropertyInfo>> GetPropertiesAsync(string nodeId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DesignerPropertyInfo>>(Array.Empty<DesignerPropertyInfo>());
    }

    public Task<IReadOnlyList<DesignerEventInfo>> GetEventsAsync(string nodeId, CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<DesignerEventInfo>>(Array.Empty<DesignerEventInfo>());
    }

    public Task<bool> SetPropertyAsync(string nodeId, string propertyName, string? value, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<string?> InsertElementAsync(
        string typeName,
        string xmlNamespace,
        string? parentNodeId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> DeleteNodeAsync(string nodeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<string?> WrapNodeAsync(
        string nodeId,
        string wrapperTypeName,
        string wrapperXmlNamespace,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<bool> SelectNodeAsync(string nodeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task<bool> RevealNodeAsync(string nodeId, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public IDesignerTransaction BeginTransaction(string name)
    {
        return new NoOpDesignerTransaction();
    }

    public void Dispose()
    {
        _editorServices.ActiveDocumentChanged -= OnActiveDocumentChanged;
    }

    private void OnActiveDocumentChanged(object? sender, EditorActiveDocumentChangedEventArgs e)
    {
        ActiveDocumentChanged?.Invoke(this, new DesignerDocumentChangedEventArgs(e.Document?.FilePath));
    }

    private sealed class NoOpDesignerTransaction : IDesignerTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
