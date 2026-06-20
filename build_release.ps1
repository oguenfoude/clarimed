$ErrorActionPreference = "Stop"

$workspaceDir = "D:\ClariMed"
$publishDir = Join-Path $workspaceDir "publish"
$payloadZip = Join-Path $workspaceDir "Payload.zip"
$installerProj = Join-Path $workspaceDir "src\FocusMed.Installer\FocusMed.Installer.csproj"

Write-Host "Cleaning old publish directory..."
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $payloadZip) { Remove-Item -Force $payloadZip }

Write-Host "Publishing FocusMed.Worker..."
dotnet publish (Join-Path $workspaceDir "src\FocusMed.Worker\FocusMed.Worker.csproj") -c Release -r win-x64 --self-contained true -o (Join-Path $publishDir "Worker")

Write-Host "Publishing FocusMed.Notifier..."
dotnet publish (Join-Path $workspaceDir "src\FocusMed.Notifier\FocusMed.Notifier.csproj") -c Release -r win-x64 --self-contained true -o (Join-Path $publishDir "Notifier")

Write-Host "Zipping Payload..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $payloadZip

Write-Host "Publishing FocusMed.Installer (Standalone EXE)..."
dotnet publish $installerProj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $workspaceDir "dist")

Write-Host "Build Complete! Installer is at D:\ClariMed\dist\FocusMed.Installer.exe"
