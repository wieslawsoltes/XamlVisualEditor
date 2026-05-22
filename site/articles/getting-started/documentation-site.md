---
title: Documentation Site
description: Build and serve the Lunet documentation site for XamlVisualEditor.
---

# Documentation Site

The project documentation site lives under `site/` and uses the default Lunet
template.

## Build

```bash
./build-docs.sh
```

The script restores the local Lunet tool from `.config/dotnet-tools.json`, clears
the previous generated output, and runs:

```bash
dotnet tool run lunet --stacktrace build
```

## Serve locally

```bash
cd site
dotnet tool run lunet serve
```

The production site is published by the `Docs` GitHub Actions workflow from
`site/.lunet/build/www`.

