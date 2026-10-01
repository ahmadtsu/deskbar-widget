<#
.SYNOPSIS
    Builds the DeskbarWidget application and compiles the Inno Setup installer.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Write-Host "==> Publishing portable single-file DeskbarWidget..." -ForegroundColor Cyan
& "$PSScriptRoot\publish-portable.ps1"

Write-Host "==> Locating Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Cyan
$cmd = Get-Command iscc.exe -ErrorAction SilentlyContinue
$iscc = if ($cmd) { $cmd.Source } else { $null }

if (-not $iscc) {
    $searchPaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
        "${env:LOCALAPPDATA}\Programs\Antigravity IDE\resources\app\node_modules\innosetup\bin\ISCC.exe"
    )
    foreach ($path in $searchPaths) {
        if (Test-Path $path) {
            $iscc = $path
            break
        }
    }
}

if (-not $iscc) {
    throw "ISCC.exe not found! Please install Inno Setup 6 or add it to PATH."
}

Write-Host "==> Using Inno Setup compiler: $iscc" -ForegroundColor Green
$issFile = Join-Path $root "installer\Installer-x64.iss"

Write-Host "==> Compiling installer: $issFile" -ForegroundColor Cyan
& $iscc $issFile

if ($LASTEXITCODE -ne 0) {
    throw "ISCC compilation failed with exit code $LASTEXITCODE"
}

$outputExe = Join-Path $root "installer\output\DeskbarWidget-Setup-x64.exe"
Write-Host "==> Installer successfully built: $outputExe" -ForegroundColor Green
