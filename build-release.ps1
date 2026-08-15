[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$projectPath = Join-Path $PSScriptRoot "src\AdvancedStashSorting.csproj"
$modInfoPath = Join-Path $PSScriptRoot "src\ModInfo.cs"
$bepInExPath = Join-Path $PSScriptRoot "build\BepInEx"
$distributionPath = Join-Path $PSScriptRoot "distrib"

if (!(Test-Path -LiteralPath $projectPath -PathType Leaf))
{
    throw "Project file was not found: $projectPath"
}

if (!(Test-Path -LiteralPath $modInfoPath -PathType Leaf))
{
    throw "ModInfo file was not found: $modInfoPath"
}

$modInfo = Get-Content -LiteralPath $modInfoPath -Raw
$versionMatch = [regex]::Match(
    $modInfo,
    'public\s+const\s+string\s+Version\s*=\s*"(?<version>[^"]+)"\s*;')

if (!$versionMatch.Success)
{
    throw "Unable to read the mod version from $modInfoPath"
}

$version = $versionMatch.Groups["version"].Value.Trim()

if ([string]::IsNullOrWhiteSpace($version) -or
    $version.IndexOfAny([System.IO.Path]::GetInvalidFileNameChars()) -ge 0)
{
    throw "Invalid mod version: '$version'"
}

Get-Command dotnet -ErrorAction Stop | Out-Null
& dotnet build $projectPath --configuration Release

if ($LASTEXITCODE -ne 0)
{
    throw "Release build failed with exit code $LASTEXITCODE"
}

if (!(Test-Path -LiteralPath $bepInExPath -PathType Container))
{
    throw "Build output was not found: $bepInExPath"
}

New-Item -ItemType Directory -Path $distributionPath -Force | Out-Null
$archivePath = Join-Path $distributionPath "AdvancedStashSorting-$version.zip"
Compress-Archive -LiteralPath $bepInExPath -DestinationPath $archivePath -CompressionLevel Optimal -Force

if (!(Test-Path -LiteralPath $archivePath -PathType Leaf))
{
    throw "Archive was not created: $archivePath"
}

Write-Host "Created $archivePath"
