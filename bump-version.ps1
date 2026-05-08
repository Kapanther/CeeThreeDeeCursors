param(
    [ValidateSet("major", "minor", "patch")]
    [string]$Part = "patch"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $root "CeeThreeDeeCursors\CeeThreeDeeCursors.csproj"

if (-not (Test-Path $projectFile)) {
    throw "Project file not found: $projectFile"
}

[xml]$projXml = Get-Content -Path $projectFile

$propertyGroup = $projXml.Project.PropertyGroup | Select-Object -First 1
if ($null -eq $propertyGroup) {
    throw "No PropertyGroup found in $projectFile"
}

function Ensure-Node {
    param(
        [System.Xml.XmlElement]$Parent,
        [string]$Name,
        [string]$DefaultValue
    )

    $node = $Parent.SelectSingleNode($Name)
    if ($null -eq $node) {
        $node = $projXml.CreateElement($Name)
        $node.InnerText = $DefaultValue
        [void]$Parent.AppendChild($node)
    }
    return $node
}

$assemblyVersionNode = Ensure-Node -Parent $propertyGroup -Name "AssemblyVersion" -DefaultValue "1.0.0.0"
$fileVersionNode = Ensure-Node -Parent $propertyGroup -Name "FileVersion" -DefaultValue "1.0.0.0"
$versionNode = Ensure-Node -Parent $propertyGroup -Name "Version" -DefaultValue "1.0.0"
$informationalNode = Ensure-Node -Parent $propertyGroup -Name "InformationalVersion" -DefaultValue "1.0.0"

$currentRaw = $assemblyVersionNode.InnerText
if ([string]::IsNullOrWhiteSpace($currentRaw)) {
    $currentRaw = "1.0.0.0"
}

$current = [version]$currentRaw
$major = $current.Major
$minor = $current.Minor
$patch = $current.Build
if ($patch -lt 0) { $patch = 0 }

switch ($Part) {
    "major" {
        $major++
        $minor = 0
        $patch = 0
    }
    "minor" {
        $minor++
        $patch = 0
    }
    "patch" {
        $patch++
    }
}

$newThree = "{0}.{1}.{2}" -f $major, $minor, $patch
$newFour = "{0}.{1}.{2}.0" -f $major, $minor, $patch

$versionNode.InnerText = $newThree
$informationalNode.InnerText = $newThree
$assemblyVersionNode.InnerText = $newFour
$fileVersionNode.InnerText = $newFour

$projXml.Save($projectFile)

Write-Host "Version bumped: $currentRaw -> $newFour"
Write-Host "Version tag: $newThree"
