Write-Host "==> Sr Test Engineer Challenge: tool-chain setup" -ForegroundColor Cyan
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)
if (-not $dotnet) { Write-Error "dotnet SDK not found. Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0"; exit 1 }
dotnet --version
$code = (Get-Command code -ErrorAction SilentlyContinue)
if (-not $code) {
  Write-Warning "VS Code 'code' CLI not found. In VS Code, press Ctrl/Cmd+Shift+P → 'Shell Command: Install 'code' command in PATH'. Then re-run this script."
} else {
  Write-Host "Installing recommended VS Code extensions..."
  code --install-extension ms-dotnettools.csharp --force | Out-Null
  code --install-extension SpecFlowTeam.SpecFlow --force | Out-Null
  code --install-extension formulahendry.dotnet-test-explorer --force | Out-Null
  Write-Host "Extensions installed (or already present)."
}
Write-Host "All set. Use scripts in the /scripts folder to run the app and tests." -ForegroundColor Green
