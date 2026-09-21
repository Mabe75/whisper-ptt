<#
.SYNOPSIS
    Baut Voice Push-to-Talk und erzeugt dist\VoicePTT-Setup.exe.

.DESCRIPTION
    1. dotnet publish (self-contained, win-x64) nach build\publish
       -> die .NET-Laufzeit liegt im Programmordner, der Zielrechner braucht nichts.
    2. Inno Setup packt alles in einen einzelnen, offline arbeitenden Installer.

.PARAMETER SkipPublish
    Nur den Installer neu packen, ohne vorher zu kompilieren.
#>
[CmdletBinding()]
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $root

$project   = Join-Path $root 'src\VoicePTT\VoicePTT.csproj'
$publishIn = Join-Path $root 'build\publish'
$issFile   = Join-Path $root 'installer\VoicePTT.iss'
$distDir   = Join-Path $root 'dist'

function Find-Iscc {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "ISCC.exe nicht gefunden. Inno Setup 6 installieren: winget install -e --id JRSoftware.InnoSetup"
}

if (-not $SkipPublish) {
    Write-Host '== dotnet publish (self-contained win-x64) ==' -ForegroundColor Cyan
    if (Test-Path $publishIn) { Remove-Item $publishIn -Recurse -Force }
    dotnet publish $project `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:DebugType=none `
        -o $publishIn
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish fehlgeschlagen (Exit $LASTEXITCODE)." }
}

if (-not (Test-Path (Join-Path $publishIn 'VoicePTT.exe'))) {
    throw "build\publish\VoicePTT.exe fehlt - erst ohne -SkipPublish bauen."
}

$size = [math]::Round(((Get-ChildItem $publishIn -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "   Programmordner: $size MB" -ForegroundColor DarkGray

Write-Host '== Inno Setup ==' -ForegroundColor Cyan
$iscc = Find-Iscc
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
& $iscc $issFile
if ($LASTEXITCODE -ne 0) { throw "Inno Setup fehlgeschlagen (Exit $LASTEXITCODE)." }

$setup = Join-Path $distDir 'VoicePTT-Setup.exe'
$mb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ''
Write-Host "Fertig: $setup ($mb MB)" -ForegroundColor Green
