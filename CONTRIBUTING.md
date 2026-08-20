# Contributing to XamlVisualEditor

Thank you for helping improve XamlVisualEditor.

## Before you start

- Search existing issues and pull requests before opening a duplicate.
- Use an issue for behavior changes that need design discussion.
- Read `AGENTS.md`; it is authoritative for architecture, MVVM, Avalonia,
  ReactiveUI, testing, performance, and code conventions.
- Keep changes focused and do not mix unrelated refactors into a fix.

## Development setup

```bash
git clone --recurse-submodules https://github.com/wieslawsoltes/XamlVisualEditor.git
cd XamlVisualEditor
dotnet restore XamlVisualEditor.slnx
dotnet build XamlVisualEditor.slnx -c Release --no-restore
```

The repository targets .NET 10. `global.json` selects an installed .NET 10 SDK
and rolls forward to the latest available feature band.

## Validate a change

Run the test projects affected by the change. Before requesting review, run the
complete Release validation when practical:

```bash
dotnet build XamlVisualEditor.slnx -c Release
dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj -c Release --no-build
dotnet test tests/XamlVisualEditor.Tests.Integration/XamlVisualEditor.Tests.Integration.csproj -c Release --no-build
dotnet test tests/XamlVisualEditor.Tests.UI/XamlVisualEditor.Tests.UI.csproj -c Release --no-build
dotnet test tests/XamlVisualEditor.Tests.Performance/XamlVisualEditor.Tests.Performance.csproj -c Release --no-build
dotnet pack XamlVisualEditor.slnx -c Release --no-build -o artifacts/packages
./eng/validate-release-packages.sh artifacts/packages 0.1.0
./build-docs.sh
```

Production code requires xUnit coverage. UI behavior requires Avalonia Headless
tests. Views remain passive, and UI events are routed through bindings, commands,
and behaviors rather than code-behind handlers.

## Pull requests

- Explain the problem and the chosen solution.
- Link related issues.
- Include tests and documentation for public behavior.
- Call out compatibility, package, or migration impact.
- Confirm the relevant build, test, package, and documentation checks.

By contributing, you agree that your contribution is licensed under the
repository's MIT License.
