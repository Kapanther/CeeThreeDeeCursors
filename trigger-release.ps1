param(
    [string]$Ref = "main",
    [string]$Tag = ""
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $root "CeeThreeDeeCursors\CeeThreeDeeCursors.csproj"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is required. Install it and run 'gh auth login'."
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    [xml]$projXml = Get-Content -Path $appProject
    $version = $projXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "1.0.0"
    }
    $Tag = "v$version"
}

Write-Host "Triggering workflow 'Build And Release MSI' on ref '$Ref' with tag '$Tag'..."

# This calls the existing GitHub Actions workflow_dispatch entrypoint.
gh workflow run "Build And Release MSI" --ref $Ref -f tag=$Tag

Write-Host "Workflow dispatched."
Write-Host "Track status: gh run list --workflow \"Build And Release MSI\""
