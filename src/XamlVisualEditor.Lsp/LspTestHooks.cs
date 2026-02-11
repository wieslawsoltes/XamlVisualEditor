namespace XamlVisualEditor.Lsp;

internal static class LspTestHooks
{
    public static LspClientSession CreateSessionForTesting(LspServerConfiguration configuration, Stream input, Stream output)
    {
        return new LspClientSession(configuration, new StreamLspTransport(input, output));
    }
}
