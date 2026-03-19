#!/usr/bin/env bash
set -euo pipefail
dotnet restore
ASPNETCORE_URLS=http://localhost:5173 dotnet run --project src/WebApp
