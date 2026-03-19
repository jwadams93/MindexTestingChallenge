#!/usr/bin/env bash
set -euo pipefail
echo "==> Sr Test Engineer Challenge: tool-chain setup"
if ! command -v dotnet >/dev/null 2>&1; then
  echo "ERROR: dotnet SDK not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0" >&2
  exit 1
fi
dotnet --version
if command -v code >/dev/null 2>&1; then
  echo "Installing recommended VS Code extensions..."
  code --install-extension ms-dotnettools.csharp --force >/dev/null 2>&1 || true
  code --install-extension SpecFlowTeam.SpecFlow --force >/dev/null 2>&1 || true
  code --install-extension formulahendry.dotnet-test-explorer --force >/dev/null 2>&1 || true
  echo "Extensions installed (or already present)."
else
  echo "WARN: VS Code 'code' CLI not found. In VS Code: Command Palette → 'Shell Command: Install code in PATH'. Then re-run this script."
fi
echo "All set. Use scripts in the /scripts folder to run the app and tests."
