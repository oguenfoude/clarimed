$ErrorActionPreference = "Stop"
$baseDir = "D:\FocusMed"

Write-Host "Killing dotnet processes to unlock files..."
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# 1. Replace text in files
Write-Host "Replacing text in files..."
$files = Get-ChildItem -Path $baseDir -Recurse -File -Exclude "*.db", "*.png", "*.jpg", "*.webp", "*.dll", "*.exe", "*.binlog", "*.dcm", "*.pdf" | Where-Object { $_.FullName -notmatch "\\\.git\\" -and $_.FullName -notmatch "\\bin\\" -and $_.FullName -notmatch "\\obj\\" }

foreach ($f in $files) {
    try {
        $content = Get-Content -Path $f.FullName -Raw
        if ($content -match "(?i)focusmed") {
            $content = $content -creplace "FocusMed", "FocusMed"
            $content = $content -creplace "focusmed", "focusmed"
            $content = $content -creplace "FOCUSMED", "FOCUSMED"
            Set-Content -Path $f.FullName -Value $content -NoNewline
            Write-Host "Updated content: $($f.FullName)"
        }
    } catch {
        Write-Host "Failed to read/write $($f.FullName): $_"
    }
}

# 2. Rename files
Write-Host "Renaming files..."
$filesToRename = Get-ChildItem -Path $baseDir -Recurse -File | Where-Object { $_.Name -match "FocusMed" -and $_.FullName -notmatch "\\\.git\\" } | Sort-Object Length -Descending

foreach ($f in $filesToRename) {
    $newName = $f.Name -replace "FocusMed", "FocusMed"
    Rename-Item -Path $f.FullName -NewName $newName
    Write-Host "Renamed file: $($f.FullName) -> $newName"
}

# 3. Rename directories (deepest first)
Write-Host "Renaming directories..."
$dirsToRename = Get-ChildItem -Path $baseDir -Recurse -Directory | Where-Object { $_.Name -match "FocusMed" -and $_.FullName -notmatch "\\\.git\\" } | Sort-Object -Property @{Expression={$_.FullName.Length}; Descending=$true}

foreach ($d in $dirsToRename) {
    $newName = $d.Name -replace "FocusMed", "FocusMed"
    Rename-Item -Path $d.FullName -NewName $newName
    Write-Host "Renamed dir: $($d.FullName) -> $newName"
}

# 4. Rename the root directory
# Wait, if we rename the root directory, our current working directory breaks.
# The prompt says: "The user has 1 active workspaces... d:\FocusMed" -> "oguenfoude/focusmed"
# If we rename D:\FocusMed, it might break the agent's IDE workspace mapping. I'll ask the user, or just leave the root folder name intact but rename the solution and everything inside. Let's just rename everything inside.

Write-Host "Done!"
