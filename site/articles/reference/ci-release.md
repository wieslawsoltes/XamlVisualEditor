---
title: CI and Release
description: Understand the XamlVisualEditor GitHub Actions workflows for build, docs, and release.
---

# CI and Release

The repository uses three primary GitHub Actions workflows.

## Build

`.github/workflows/build.yml` restores, builds, and tests the solution on Linux,
Windows, and macOS. It uploads TRX test result artifacts and publishes check-run
test reports when the token can create checks.

## Docs

`.github/workflows/docs.yml` builds the Lunet site from `site/`. Pushes to the
default branch deploy to GitHub Pages. Pull requests build the site without
deploying.

## Release

`.github/workflows/release.yml` runs on `v*` tags and manual dispatch. It builds
and tests the solution, publishes framework-dependent app artifacts for Linux,
Windows, and macOS, and creates a GitHub release with generated notes.

## Local docs build

```bash
./build-docs.sh
```

