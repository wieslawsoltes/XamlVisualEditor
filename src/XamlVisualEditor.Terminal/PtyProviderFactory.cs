using System.Runtime.InteropServices;

namespace XamlVisualEditor.Terminal;

public static class PtyProviderFactory
{
    public static IPtyProvider CreateDefault()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsPtyProvider();
        }

        return new UnixPtyProvider();
    }
}
