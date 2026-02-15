# Extension Migration Guide (Internal)

This guide is for contributors moving shell-owned features to extension-owned features.

## 1. Migration Rules

- Keep feature logic in extension projects under `extensions/`.
- Keep extension contracts in `src/XamlVisualEditor.Extensions`.
- Do not reference `XamlVisualEditor.Shell.ViewModels` directly from new extensions.
- Keep views passive (MVVM) and route commands through `ICommands`.

## 2. Recommended Migration Steps

1. Define/extend the SDK contract in `src/XamlVisualEditor.Extensions`.
2. Add a shell adapter implementing that contract in `src/XamlVisualEditor.Shell.ViewModels`.
3. Register the adapter in `src/XamlVisualEditor.App/App.axaml.cs`.
4. Build extension commands, contributions, and views in `extensions/<Feature>Extension`.
5. Move menu/palette wiring from shell to extension contributions.
6. Remove or gate shell fallback UI paths once extension parity is verified.

## 3. Checklist for a Feature Move

- Add command metadata (`CommandMetadata`) for all extension commands.
- Register menu/toolbar/palette contributions via `IExtensionContributionRegistry`.
- Register view contributions via `RegisterViews`.
- Keep command enablement declarative with `when` expressions.
- Add or update tests for adapters and extension view models.
- Update `docs/EXTENSION-API.md` and `docs/EXTENSIONS.md`.

## 4. Common Pitfalls

- Leaving duplicate command handlers in `MainWindowViewModel`.
- Keeping shell dock defaults for panels now contributed by extensions.
- Exposing raw host ViewModels instead of typed extension-facing APIs.
- Adding reflection-based plumbing where source-generated or typed APIs are available.

## 5. Current Priority Areas

- Terminal/task host API expansion.
- Settings schema discovery/validation/change notifications.
- Final shell command map cleanup to host lifecycle responsibilities only.
