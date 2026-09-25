# Builds the agent and the app into one folder (dist\) ready to copy or package.
# Usage: pwsh scripts/publish.ps1 [-Configuration Release] [-Runtime win-x64] [-SelfContained] [-Version 1.2.3]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist"
$sc = if ($SelfContained) { "true" } else { "false" }

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

foreach ($project in @("FocusLens.Agent", "FocusLens.App")) {
    dotnet publish (Join-Path $root "src\$project\$project.csproj") `
        -c $Configuration -r $Runtime --self-contained $sc -p:Version=$Version -o $out
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $project" }
}

Write-Host "Published to $out"
Write-Host "Run $out\FocusLens.exe (it starts FocusLensAgent.exe next to it)."
