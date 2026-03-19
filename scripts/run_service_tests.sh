#!/usr/bin/env bash
set -euo pipefail
dotnet test tests/Service.Tests/Service.Tests.csproj -v normal
