# Publishes the portable single-file win-x64 exe to ./publish/.
# Framework-dependent (EverythingToolbar model): needs no runtime packs at
# publish time, so it works offline. Target machines need
# ".NET Desktop Runtime 9.0" or later (same prerequisite pattern as
# EverythingToolbar's ".NET Desktop Runtime 8.0").
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet publish "$root/src/DeskbarWidget.App/DeskbarWidget.App.csproj" `
    -c Release -r win-x64 --self-contained false `
    /p:PublishSingleFile=true `
    -o "$root/publish"
Write-Output "Published to $root/publish/DeskbarWidget.App.exe"
