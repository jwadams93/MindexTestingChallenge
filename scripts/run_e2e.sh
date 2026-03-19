#!/usr/bin/env bash
set -euo pipefail
dotnet test tests/E2E.Specs/E2E.Specs.csproj -v normal
