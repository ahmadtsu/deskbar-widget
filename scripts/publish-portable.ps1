# Publishes the portable single-file win-x64 exe to ./publish/.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
dotnet publish "$root/src/DeskbarWidget.App/DeskbarWidget.App.csproj" `
    -c Release -r win-x64 --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    -o "$root/publish"
Write-Output "Published to $root/publish/DeskbarWidget.App.exe"
