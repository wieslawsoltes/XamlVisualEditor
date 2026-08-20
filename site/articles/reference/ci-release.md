---
title: CI and Release
description: Understand the XamlVisualEditor GitHub Actions workflows for build, docs, and release.
---

# CI and Release

The repository uses three primary GitHub Actions workflows.

## Build

`.github/workflows/build.yml` restores, builds, and tests the complete product
solution on Linux, Windows, and macOS. It uploads TRX test result artifacts and
publishes check-run test reports when the token can create checks. A Linux NuGet
pack job validates all 48 product packages and symbol packages, required NuGet
metadata, generated built-in extension manifests, README badge coverage, and the
two sample extension packages before uploading `.nupkg` and `.snupkg` artifacts
for inspection.

## Docs

`.github/workflows/docs.yml` builds the Lunet site from `site/`. Pushes to the
default branch deploy to GitHub Pages. Pull requests build the site without
deploying.

## Release

`.github/workflows/release.yml` runs on valid Semantic Version `v*` tags and
manual dispatch. It builds and tests the solution, publishes framework-dependent
x64 and Arm64 app archives for Linux, Windows, and macOS, validates and packs
reusable libraries and installable extensions, publishes `.nupkg` and `.snupkg`
symbols to NuGet, and creates a GitHub release with generated notes and a
`SHA256SUMS` integrity file. Releases require the `NUGET_API_KEY` repository
secret so GitHub and NuGet cannot silently diverge.

Each application archive is checked for the app host, a matching runtime-specific
previewer host, the VS Code compatibility host, README, changelog, and license
before it is uploaded.

NuGet package metadata is centralized in `Directory.Build.props`: package
identity defaults, author and company, repository and project URLs, MIT license,
readme, icon, tags, Source Link, deterministic build settings, and symbol
package output. `Directory.Build.targets` generates version-synchronized
`xve.extension.json` manifests for built-in extension packages. Executable hosts
and tests remain non-packable.

## Local docs build

```bash
./build-docs.sh
```
