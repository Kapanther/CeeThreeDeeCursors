param(
    [string]$Configuration = "Release",
    [string]$Tag = "",
    [switch]$PreRelease,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $root "CeeThreeDeeCursors\CeeThreeDeeCursors.csproj"
$buildInstallerScript = Join-Path $root "build-installer.ps1"
$installerDir = Join-Path $root "artifacts\installer"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) is required. Install it and run: gh auth login"
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    [xml]$projXml = Get-Content -Path $appProject
    $version = $projXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "1.0.0"
    }
    $Tag = "v$version"
}

if ($DryRun) {
    Write-Host "[DryRun] Would build installer with configuration: $Configuration"
}
else {
    & $buildInstallerScript -Configuration $Configuration
}

$tagVersion = $Tag.TrimStart('v', 'V')
$preferredMsi = Join-Path $installerDir ("CeeThreeDeeCursorsV{0}.msi" -f $tagVersion)

$msi = $null
if (Test-Path $preferredMsi) {
    $msi = Get-Item $preferredMsi
}
else {
    $msi = Get-ChildItem -Path $installerDir -Filter "CeeThreeDeeCursorsV*.msi" |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($msi) {
        Write-Warning "Preferred MSI for tag $Tag not found at $preferredMsi. Falling back to latest MSI: $($msi.Name)"
    }
}

if (-not $msi) {
    throw "No versioned MSI found in $installerDir"
}

Write-Host "Using MSI: $($msi.FullName)"
Write-Host "Target release tag: $Tag"

$releaseExists = $false
if (-not $DryRun) {
    $previousPreference = $ErrorActionPreference
    try {
        # gh writes 'release not found' to stderr for missing tags; treat that as a normal non-existent release check.
        $ErrorActionPreference = "Continue"
        & gh release view $Tag 1> $null 2> $null
        $releaseExists = ($LASTEXITCODE -eq 0)
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }
}

if ($releaseExists) {
    $cmd = ('gh release upload {0} "{1}" --clobber' -f $Tag, $msi.FullName)
    if ($DryRun) {
        Write-Host "[DryRun] Would run: $cmd"
    }
    else {
        Write-Host "Release exists. Uploading/overwriting MSI asset..."
        gh release upload $Tag "$($msi.FullName)" --clobber
    }
}
else {
    $title = "CeeThreeDeeCursors $Tag"
    $args = @("release", "create", $Tag, "$($msi.FullName)", "--title", $title, "--generate-notes")
    if ($PreRelease) {
        $args += "--prerelease"
    }

    if ($DryRun) {
        Write-Host "[DryRun] Would run: gh $($args -join ' ')"
    }
    else {
        Write-Host "Creating release and uploading MSI..."
        gh @args
    }
}

if ($DryRun) {
    Write-Host "[DryRun] Complete. No release was created or modified."
}
else {
    Write-Host "Release updated successfully."
}
