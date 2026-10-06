$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "artifacts/publish/win-x64"

dotnet test (Join-Path $root "WorkDelta.slnx") -c Release
dotnet publish (Join-Path $root "src/WorkDelta.App/WorkDelta.App.csproj") `
  -c Release -r win-x64 --self-contained true -o $output

Write-Host "Published to $output"
