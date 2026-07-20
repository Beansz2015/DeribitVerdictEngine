#requires -Version 5.1
<#
  tools/build-manual-pdfs.ps1 — regenerate docs/TraderGuide.pdf + docs/UserManual.pdf
  from their .md sources.

  There was no build script before this (the toolchain had to be reverse-engineered when
  the What-If Replay section was added, 2026-07-17); this pins it so future regenerations
  are reproducible.

  Toolchain: pandoc + XeLaTeX (MiKTeX).
    - XeLaTeX, not the pdflatex default: the manuals are full of Unicode (arrows, box
      drawing, maths) and pdflatex hard-fails on it.
    - Cambria (body) + Cascadia Mono (code): between them they cover everything the
      manuals use except six glyphs, which tools/manual-pdf-header.tex routes to
      Segoe UI Symbol. Cascadia Mono matters — Consolas silently drops ⚠ ★ ✓ ✗ ⌈ ⌉ ∈
      inside code blocks.

  IMPORTANT: a missing glyph is only a WARNING from pandoc — the character is dropped
  from the PDF without failing the build. This script therefore treats any
  "Missing character" warning as a FAILURE and names the codepoints, so a symbol can
  never quietly vanish from a published manual.

  Usage:  powershell -ExecutionPolicy Bypass -File tools\build-manual-pdfs.ps1
  Exit:   0 = both PDFs built with full glyph coverage; 1 = build or coverage failure.
#>
[CmdletBinding()]
param(
    [string]$Pandoc = "$env:LOCALAPPDATA\Pandoc\pandoc.exe",
    [switch]$AllowMissingGlyphs
)

$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $repo

if (-not (Test-Path $Pandoc)) {
    $cmd = Get-Command pandoc -ErrorAction SilentlyContinue
    if ($cmd) { $Pandoc = $cmd.Source } else { Write-Host "FAIL  pandoc not found (tried '$Pandoc')" -ForegroundColor Red; exit 1 }
}

$header = Join-Path $repo 'tools\manual-pdf-header.tex'
$failed = $false

foreach ($doc in @('TraderGuide', 'UserManual')) {
    $md  = Join-Path $repo "docs\$doc.md"
    $pdf = Join-Path $repo "docs\$doc.pdf"
    Write-Host ""
    Write-Host "=== $doc ===" -ForegroundColor Cyan

    $out = & $Pandoc $md -o $pdf `
        --pdf-engine=xelatex `
        --include-in-header=$header `
        -V mainfont="Cambria" `
        -V monofont="Cascadia Mono" `
        -V geometry:margin=1in 2>&1

    # MiKTeX's "check for updates" nag is noise, not a build problem.
    $noise = $out | Where-Object { $_ -notmatch 'MiKTeX updates' }

    if (-not (Test-Path $pdf)) {
        Write-Host "FAIL  $doc.pdf was not produced" -ForegroundColor Red
        $noise | Select-Object -First 10 | ForEach-Object { Write-Host "      $_" }
        $failed = $true
        continue
    }

    $missing = @($noise | Select-String -Pattern 'Missing character' -AllMatches |
                 ForEach-Object { if ($_ -match '\(U\+([0-9A-F]{4})\)') { "U+$($Matches[1])" } } |
                 Sort-Object -Unique)

    $size = [math]::Round((Get-Item $pdf).Length / 1KB)
    if ($missing.Count -gt 0) {
        Write-Host "FAIL  $doc.pdf built (${size} KB) but DROPPED glyphs: $($missing -join ', ')" -ForegroundColor Red
        Write-Host "      Add a \newunicodechar line for each in tools\manual-pdf-header.tex." -ForegroundColor Yellow
        if (-not $AllowMissingGlyphs) { $failed = $true }
    } else {
        Write-Host "OK    $doc.pdf  (${size} KB, full glyph coverage)" -ForegroundColor Green
    }
}

Write-Host ""
if ($failed) { Write-Host "MANUAL PDF BUILD FAILED" -ForegroundColor Red; exit 1 }
Write-Host "MANUAL PDFs REBUILT" -ForegroundColor Green
exit 0
