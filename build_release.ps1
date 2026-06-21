$workspaceDir = "D:\ClariMed"
$publishDir = Join-Path $workspaceDir "publish"
$payloadZip = Join-Path $workspaceDir "Payload2.zip"
$distDir = Join-Path $workspaceDir "dist"
$installerProj = Join-Path $workspaceDir "src\FocusMed.Installer\FocusMed.Installer.csproj"

# ── Step 1: Clean ──
Write-Host "`n=== Cleaning old artifacts ===" -ForegroundColor Cyan
Stop-Process -Name "FocusMed.Installer" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "FocusMed.Notifier" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue }
if (Test-Path $payloadZip) { Remove-Item -Force $payloadZip -ErrorAction SilentlyContinue }
if (Test-Path (Join-Path $distDir "FocusMed.Installer.exe")) { Remove-Item -Force (Join-Path $distDir "FocusMed.Installer.exe") -ErrorAction SilentlyContinue }

# ── Step 2: Publish Worker ──
Write-Host "`n=== Publishing FocusMed.Worker ===" -ForegroundColor Cyan
dotnet publish (Join-Path $workspaceDir "src\FocusMed.Worker\FocusMed.Worker.csproj") -c Release -r win-x64 --self-contained true /p:DebugType=None /p:DebugSymbols=false -o (Join-Path $publishDir "Worker")
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Worker publish failed!" -ForegroundColor Red; exit 1 }
Get-ChildItem (Join-Path $publishDir "Worker") -Filter "*.pdb" | Remove-Item -Force
Copy-Item -Path (Join-Path $workspaceDir "templates") -Destination (Join-Path $publishDir "Worker") -Recurse -Force


# ── Step 3: Publish Notifier ──
Write-Host "`n=== Publishing FocusMed.Notifier ===" -ForegroundColor Cyan
dotnet publish (Join-Path $workspaceDir "src\FocusMed.Notifier\FocusMed.Notifier.csproj") -c Release -r win-x64 --self-contained true /p:DebugType=None /p:DebugSymbols=false -o (Join-Path $publishDir "Notifier")
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Notifier publish failed!" -ForegroundColor Red; exit 1 }
Get-ChildItem (Join-Path $publishDir "Notifier") -Filter "*.pdb" | Remove-Item -Force

# ── Step 4: Zip Payload (Safe Method) ──
Write-Host "`n=== Zipping Payload ===" -ForegroundColor Cyan
dotnet build-server shutdown
Start-Sleep -Seconds 5

if (Test-Path $payloadZip) { Remove-Item -Force $payloadZip }

try {
    Compress-Archive -Path "$publishDir\*" -DestinationPath $payloadZip -Force -CompressionLevel Optimal
    $zipSuccess = $true
} catch {
    Write-Host "Zip failed: $_" -ForegroundColor Red
    $zipSuccess = $false
}

if (-not $zipSuccess -or -not (Test-Path $payloadZip)) { 
    Write-Host "ERROR: Payload.zip not created!" -ForegroundColor Red; exit 1 
}

$zipSize = [math]::Round((Get-Item $payloadZip).Length / 1MB, 1)
Write-Host "Payload.zip created ($zipSize MB)" -ForegroundColor Green

# ── Step 5: Publish Installer ──
Write-Host "`n=== Publishing FocusMed.Installer ===" -ForegroundColor Cyan
dotnet publish $installerProj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o $distDir
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: Installer publish failed!" -ForegroundColor Red; exit 1 }

Write-Host "`n=== Build Complete! ===" -ForegroundColor Green
$exeSize = [math]::Round((Get-Item (Join-Path $distDir "FocusMed.Installer.exe")).Length / 1MB, 1)
Write-Host "Installer: $distDir\FocusMed.Installer.exe ($exeSize MB)" -ForegroundColor Green
