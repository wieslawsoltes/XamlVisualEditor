---
title: CI and Release
description: Understand the XamlVisualEditor GitHub Actions workflows for build, docs, and release.
---

# CI and Release

The repository uses three primary GitHub Actions workflows.

## Build

`.github/workflows/build.yml` restores, builds, and tests the solution on Linux,
Windows, and macOS. It uploads TRX test result artifacts and publishes check-run
test reports when the token can create checks. A Linux NuGet pack job validates
package metadata and uploads `.nupkg` and `.snupkg` artifacts for PR inspection.

## Docs

`.github/workflows/docs.yml` builds the Lunet site from `site/`. Pushes to the
default branch deploy to GitHub Pages. Pull requests build the site without
deploying.

## Release

`.github/workflows/release.yml` runs on `v*` tags and manual dispatch. It builds
and tests the solution, publishes framework-dependent app artifacts for Linux,
Windows, and macOS, packs reusable libraries and extensions into NuGet `.nupkg`
and `.snupkg` artifacts, publishes them to NuGet when `NUGET_API_KEY` is
configured, and creates a GitHub release with generated notes.

NuGet package metadata is centralized in `Directory.Build.props`: package
identity defaults, author and company, repository and project URLs, MIT license,
readme, icon, tags, Source Link, deterministic build settings, and symbol
package output. Executable hosts and tests remain non-packable.

## Local docs build

```bash
./build-docs.sh
```
