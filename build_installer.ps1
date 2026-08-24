# build_installer.ps1 — Publishes Nekoframe and compiles the installer.
# Usage: .\build_installer.ps1
# Requires Inno Setup 6: winget install JRSoftware.InnoSetup

param([string]$Version = "1.0.0")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$publishDir = "bin\Release\net8.0-windows\win-x64\publish"

Write-Host "`n[1/3] Publishing..." -ForegroundColor Cyan

dotnet publish `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version

if ($LASTEXITCODE -ne 0) { Write-Host "dotnet publish failed." -ForegroundColor Red; exit 1 }


Write-Host "[1/3] Done." -ForegroundColor Green

Write-Host "`n[2/3] Locating Inno Setup compiler..." -ForegroundColor Cyan

$iscc = Get-Command "iscc.exe" -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Source

if (-not $iscc) {
    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $iscc) {
    Write-Host "`nInno Setup not found. Install it: winget install JRSoftware.InnoSetup" -ForegroundColor Yellow
    exit 1
}

Write-Host "[2/3] Found: $iscc" -ForegroundColor Green

Write-Host "`n[3/3] Compiling installer..." -ForegroundColor Cyan

New-Item -ItemType Directory -Force -Path "dist" | Out-Null

& $iscc "installer\nekoframe.iss" /DAppVersion=$Version

if ($LASTEXITCODE -ne 0) { Write-Host "Inno Setup compilation failed." -ForegroundColor Red; exit 1 }

Write-Host "`n[3/3] Done.`n`nInstaller: dist\NekoframeSetup.exe`n" -ForegroundColor Green
