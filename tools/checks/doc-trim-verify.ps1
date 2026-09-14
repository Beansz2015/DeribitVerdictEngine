#requires -Version 5.1
<#
  tools/checks/doc-trim-verify.ps1

  Re-checks every row of docs/doc-trim-log.md (the ledger of blocks moved out of the
  always-read docs by a context trim). For each row it asserts THREE properties:

    1. TAG     - the block at <Lines at tag> in <Source>, read from the pre-trim git tag,
                 hashes to the ledger SHA-256.
    2. ARCHIVE - the archived copy between its begin/end markers in <Destination>
                 hashes to the same value.
    3. POINTER - the source doc in the working tree still names the ID.

  Hash = SHA-256 of the block's lines in LF form, joined by LF with one trailing LF,
  UTF-8. Git stores these docs with LF; the working tree may be CRLF, so both sides are
  normalised before hashing.

  Read-only. No network. Exit 0 = every row passes; exit 1 = any failure.
  Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/doc-trim-verify.ps1
#>
[CmdletBinding()]
param(
    [string]$Ledger = 'docs/doc-trim-log.md',
    [string]$Tag    = 'doc-trim-2026-09-14-pre'
)
$ErrorActionPreference = 'Stop'
$root = (& git rev-parse --show-toplevel).Trim()

function Get-GitBlobBytes([string]$spec) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    $psi.Arguments = "show `"$spec`""
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $root
    $p = [System.Diagnostics.Process]::Start($psi)
    $ms = New-Object System.IO.MemoryStream
    $p.StandardOutput.BaseStream.CopyTo($ms)
    $err = $p.StandardError.ReadToEnd()
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) { throw "git show $spec failed: $err" }
    return ,$ms.ToArray()
}

function Get-LfLines([byte[]]$bytes) {
    $t = [System.Text.Encoding]::UTF8.GetString($bytes).Replace("`r`n", "`n")
    return ,($t -split "`n")
}

function Get-BlockSha([string[]]$lines) {
    $text = ($lines -join "`n") + "`n"
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $h = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($text))
    return -join ($h | ForEach-Object { $_.ToString('x2') })
}

$ledgerPath = Join-Path $root $Ledger
$rows = @(Get-Content -LiteralPath $ledgerPath -Encoding UTF8 | Where-Object { $_ -match '^\| trim-' })
if ($rows.Count -eq 0) { Write-Host "FAIL  no ledger rows found in $Ledger"; exit 1 }

$tagCache = @{}; $fileCache = @{}; $fail = 0
foreach ($r in $rows) {
    $c = @($r.Split('|') | ForEach-Object { $_.Trim() })
    $id = $c[1]; $src = $c[2]; $range = $c[3]; $dest = $c[5]; $want = $c[7]
    $s = [int]($range.Split('-')[0]); $e = [int]($range.Split('-')[1])
    $bad = @()

    if (-not $tagCache.ContainsKey($src)) { $tagCache[$src] = Get-LfLines (Get-GitBlobBytes "${Tag}:$src") }
    $tagLines = $tagCache[$src]
    $tagSha = Get-BlockSha @($tagLines[($s - 1)..($e - 1)])
    if ($tagSha -ne $want) { $bad += 'TAG' }

    if (-not $fileCache.ContainsKey($dest)) {
        $p = Join-Path $root $dest
        $fileCache[$dest] = if (Test-Path -LiteralPath $p) { Get-LfLines ([System.IO.File]::ReadAllBytes($p)) } else { ,@() }
    }
    $dl = $fileCache[$dest]
    $i = [Array]::IndexOf($dl, "<!-- $id begin -->"); $j = [Array]::IndexOf($dl, "<!-- $id end -->")
    if ($i -lt 0 -or $j -le $i) { $bad += 'ARCHIVE(markers missing)' }
    elseif ($j -eq $i + 1) { $bad += 'ARCHIVE(empty)' }
    elseif ((Get-BlockSha @($dl[($i + 1)..($j - 1)])) -ne $want) { $bad += 'ARCHIVE' }

    if (-not $fileCache.ContainsKey("src:$src")) {
        $fileCache["src:$src"] = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes((Join-Path $root $src)))
    }
    if ($fileCache["src:$src"].IndexOf($id) -lt 0) { $bad += 'POINTER' }

    if ($bad.Count -eq 0) { Write-Host "PASS  $id  $src $range" }
    else { Write-Host ("FAIL  $id  $src $range  -> " + ($bad -join ', ')); $fail++ }
}
Write-Host ""
Write-Host ("{0} rows checked, {1} failed" -f $rows.Count, $fail)
if ($fail -gt 0) { exit 1 }
exit 0
