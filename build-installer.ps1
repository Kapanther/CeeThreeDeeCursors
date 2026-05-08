param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$appProject = Join-Path $root "CeeThreeDeeCursors\CeeThreeDeeCursors.csproj"
$installerProject = Join-Path $root "Installer\CeeThreeDeeCursors.Installer.wixproj"
$publishDir = Join-Path $root "artifacts\publish"
$generatedWxs = Join-Path $root "Installer\GeneratedFiles.wxs"
$installerDir = Join-Path $root "artifacts\installer"

[xml]$projXml = Get-Content -Path $appProject
$assemblyVersionRaw = $projXml.Project.PropertyGroup.AssemblyVersion | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($assemblyVersionRaw)) {
    $assemblyVersionRaw = "1.0.0.0"
}

$assemblyVersion = [version]$assemblyVersionRaw
$appVersion = "{0}.{1}.{2}" -f $assemblyVersion.Major, $assemblyVersion.Minor, $assemblyVersion.Build

Write-Host "Publishing app to: $publishDir"
dotnet publish $appProject -c $Configuration -o $publishDir

Write-Host "Generating WiX file list from publish output..."
$files = Get-ChildItem -Path $publishDir -File | Where-Object { $_.Name -ne "CeeThreeDeeCursors.exe" } | Sort-Object Name
if ($files.Count -eq 0) {
    throw "Publish directory is empty: $publishDir"
}

$componentLines = New-Object System.Collections.Generic.List[string]
$index = 1
foreach ($file in $files) {
    $componentId = "Cmp$index"
    $fileId = "File$index"
    $regName = $file.Name.Replace("'", "_")
    $guid = [guid]::NewGuid().ToString().ToUpperInvariant()
    $source = $file.FullName.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
    $componentLines.Add(('    <Component Id="{0}" Directory="INSTALLFOLDER" Guid="{1}">' -f $componentId, $guid))
    $componentLines.Add(('      <File Id="{0}" Source="{1}" />' -f $fileId, $source))
    $componentLines.Add(('      <RegistryValue Root="HKCU" Key="Software\CeeThreeDee\CeeThreeDeeCursors\Files" Name="{0}" Type="string" Value="1" KeyPath="yes" />' -f $regName))
    $componentLines.Add('    </Component>')
    $index++
}

$wxs = @(
    '<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">',
    '  <Fragment>',
    '    <ComponentGroup Id="AppFilesGroup">'
) + $componentLines + @(
    '    </ComponentGroup>',
    '  </Fragment>',
    '</Wix>'
)

Set-Content -Path $generatedWxs -Value $wxs -Encoding UTF8

Write-Host "Building MSI installer..."
dotnet build $installerProject -c $Configuration -p:AppPublishDir=$publishDir -p:AppVersion=$appVersion

# Rename MSI to include semantic version as V#.#.# (e.g., CeeThreeDeeCursorsV1.2.0.msi)
$versionTag = $appVersion

$defaultMsi = Join-Path $installerDir "CeeThreeDeeCursors-Installer.msi"
$versionedMsi = Join-Path $installerDir ("CeeThreeDeeCursorsV{0}.msi" -f $versionTag)

if (Test-Path $defaultMsi) {
    Move-Item -Path $defaultMsi -Destination $versionedMsi -Force
    Write-Host "Versioned MSI: $versionedMsi"
}
else {
    Write-Warning "Default MSI not found at expected path: $defaultMsi"
}

Write-Host "Done. MSI output is under artifacts\installer"
