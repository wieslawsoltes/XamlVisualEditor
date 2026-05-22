#!/usr/bin/env bash
set -euo pipefail

export DOTNET_NOLOGO=true
export DOTNET_CLI_TELEMETRY_OPTOUT=true
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

dotnet tool restore
rm -rf site/.lunet/build
cd site
dotnet tool run lunet --stacktrace build

