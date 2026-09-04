#Requires -Version 5.0
<#
.SYNOPSIS
    Build InfluxDB Studio (Release) and pack a portable zip.

.EXAMPLE
    .\pack.ps1
    .\pack.ps1 -Configuration Release
    .\pack.ps1 -SkipRestore
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDir,

    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$IncludeSymbols
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Solution = Join-Path $Root 'CymaticLabs.InfluxDB.sln'
$Project = Join-Path $Root 'src\CymaticLabs.InfluxDB.Studio\CymaticLabs.InfluxDB.Studio.csproj'
$BuildOut = Join-Path $Root "src\CymaticLabs.InfluxDB.Studio\bin\$Configuration"

if (-not $OutputDir) {
    $OutputDir = Join-Path $Root 'dist'
}

function Write-Step([string]$Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }

    $candidates = @(
        'E:\DevTools\VisualStudio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\MSBuild\14.0\Bin\MSBuild.exe"
    )

    foreach ($path in $candidates) {
        if ($path -and (Test-Path $path)) { return $path }
    }

    throw 'MSBuild not found. Install Visual Studio (with .NET desktop development) or Build Tools.'
}

function Find-NuGet {
    $cmd = Get-Command nuget.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $local = Join-Path $Root 'tools\nuget.exe'
    if (Test-Path $local) { return $local }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $vsNuGet = & $vswhere -latest -find 'CommonExtensions\Microsoft\NuGet\NuGet.exe' | Select-Object -First 1
        if ($vsNuGet -and (Test-Path $vsNuGet)) { return $vsNuGet }
    }

    return $null
}

function Get-NuGet {
    $nuget = Find-NuGet
    if ($nuget) { return $nuget }

    $tools = Join-Path $Root 'tools'
    if (-not (Test-Path $tools)) {
        New-Item -ItemType Directory -Path $tools | Out-Null
    }

    $nuget = Join-Path $tools 'nuget.exe'
    Write-Host "Downloading nuget.exe ..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nuget
    return $nuget
}

function Get-FileVersion([string]$ExePath) {
    $info = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
    $version = $info.FileVersion
    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = $info.ProductVersion
    }
    if ([string]::IsNullOrWhiteSpace($version)) {
        return '0.0.0'
    }

    # 0.2.0.0 -> 0.2.0
    if ($version -match '^(\d+\.\d+\.\d+)\.0$') {
        return $Matches[1]
    }
    return $version
}

if (-not (Test-Path $Solution)) {
    throw "Solution not found: $Solution"
}

Write-Host "InfluxDB Studio pack" -ForegroundColor Green
Write-Host "Root: $Root"
Write-Host "Configuration: $Configuration"

if (-not $SkipRestore) {
    Write-Step 'Restore NuGet packages'
    $nuget = Get-NuGet
    & $nuget restore $Solution -NonInteractive
    if ($LASTEXITCODE -ne 0) { throw "nuget restore failed with exit code $LASTEXITCODE" }
}

if (-not $SkipBuild) {
    Write-Step "Build $Configuration"
    $msbuild = Find-MSBuild
    Write-Host "MSBuild: $msbuild"
    & $msbuild $Solution /t:Rebuild /p:Configuration=$Configuration /p:Platform='Any CPU' /m /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }
}

$exe = Join-Path $BuildOut 'InfluxDBStudio.exe'
if (-not (Test-Path $exe)) {
    throw "Build output not found: $exe"
}

$version = Get-FileVersion $exe
$packageName = "InfluxDBStudio-$version"
$stage = Join-Path $OutputDir $packageName
$zip = Join-Path $OutputDir "$packageName.zip"

Write-Step "Stage $packageName"
if (Test-Path $OutputDir) {
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    if (Test-Path $zip) { Remove-Item $zip -Force }
}
else {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

New-Item -ItemType Directory -Path $stage | Out-Null

$excludeExt = @('.pdb', '.xml', '.log', '.ilk', '.exp')
if ($IncludeSymbols) {
    $excludeExt = @('.xml', '.log', '.ilk', '.exp')
}

Get-ChildItem -Path $BuildOut -File | Where-Object {
    $excludeExt -notcontains $_.Extension.ToLowerInvariant() -and
    $_.Name -notmatch '\.vshost\.' -and
    $_.Name -ne 'InfluxDBStudio.log'
} | ForEach-Object {
    Copy-Item $_.FullName -Destination $stage
}

$readme = @"
InfluxDB Studio $version
Requires .NET Framework 4.6.1 or later.
Run InfluxDBStudio.exe
"@
Set-Content -Path (Join-Path $stage 'README.txt') -Value $readme -Encoding UTF8

Write-Step "Zip $zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "Folder: $stage"
Write-Host "Zip:    $zip"
Get-ChildItem $stage | Select-Object Name, Length | Format-Table -AutoSize
Write-Host ("Zip size: {0:N1} KB" -f ((Get-Item $zip).Length / 1KB))
