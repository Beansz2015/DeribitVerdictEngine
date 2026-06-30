# Installs the tracked pre-push hook into .git/hooks (not version-controlled).
# Run once per clone:  powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/install-hooks.ps1
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$src  = Join-Path $repo 'tools\checks\pre-push'
$dst  = Join-Path $repo '.git\hooks\pre-push'
Copy-Item -Path $src -Destination $dst -Force
Write-Host "Installed pre-push hook -> $dst"
