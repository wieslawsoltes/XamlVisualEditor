---
title: Extension SDK
description: Build native .NET extensions for XamlVisualEditor with manifests, commands, views, services, and NuGet packaging.
---

# Extension SDK

XamlVisualEditor extensions are native .NET packages. A package contains
`xve.extension.json` at the package root and an assembly implementing
`IXveExtension`.

## Minimal entry point

```csharp
using XamlVisualEditor.Extensions;

public sealed class MyExtension : IXveExtension
{
    public Task ActivateAsync(ExtensionContext context, CancellationToken ct)
    {
        context.Subscriptions.Add(context.Commands.Register(
            "sample.sayHello",
            _ => context.Window.ShowInformationMessageAsync("Hello", ct)));

        return Task.CompletedTask;
    }
}
```

## Common contribution surfaces

- Commands and command metadata.
- Menus, toolbars, command palette, and status bar items.
- Tree, custom, and webview-style views.
- Language servers and intellisense providers.
- Debugger registrations.
- Property editors.
- Settings, storage, logging, dialogs, and permissions.

## Samples

Sample packages live under `tools/ExtensionSamples`:

```bash
dotnet pack tools/ExtensionSamples/HelloExtension/HelloExtension.csproj -c Release
dotnet pack tools/ExtensionSamples/LspExtension/LspExtension.csproj -c Release
```

For deeper details, see the internal extension docs:

- `docs/EXTENSIONS.md`
- `docs/EXTENSION-API.md`
- `docs/VSCODE-MIGRATION.md`

