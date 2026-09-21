#requires -Version 5.1
<#
  tools/checks/lib/InvokeJev.ps1 -- shared TypeSafe/Jev System One HTTP call.

  Dot-sourced by BOTH:
    - tools/checks/rider-travel.ps1   (rider-arrival audit)
    - tools/checks/commit-walker.ps1  ([no-engine-change] tag audit)

  Extracted verbatim out of rider-travel.ps1 on 2026-09-22 per
  docs/commit-walker-check-spec.md section 6 ("Reuse its Invoke-Jev verbatim -- do
  not write a second HTTP call"). Same reasoning as tools/checks/lib/HeaderText.ps1's
  own extraction: a second copy drifts the first time the call shape changes, and
  this repo already rules on that defect class for constants and fixture literals.

  WHY THE MANUAL UTF-8 ENCODING MATTERS, AND IS NOT OPTIONAL: PowerShell 5.1's
  Invoke-RestMethod does NOT UTF-8-encode a plain [string] -Body -- it silently
  mis-encodes anything outside Latin-1, which corrupts the JSON bytes on the wire
  and the API returns 400 Bad Request. This was reproduced against a real rider-travel
  row (docs/rider-travel-check-spec.md): string body -> 400, identical JSON as explicit
  UTF-8 bytes -> 200. Both harnesses' state carries non-ASCII markup (em-dashes, section
  marks, star/prohibited glyphs in the ledger; em-dashes and section marks in commit
  subjects) so this is the median case for both callers, not an edge case.

  Any change here changes the observable HTTP behaviour of BOTH callers. Before it
  ships, re-run rider-travel's own acceptance items 3 and 6
  (docs/rider-travel-check-spec.md section 7) and confirm both still pass.
#>

function Invoke-Jev([string]$apiKey, [hashtable]$body) {
    $json = $body | ConvertTo-Json -Depth 12
    # See header comment above -- encoding the body ourselves is the fix, not a retry
    # or a fallback.
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    try {
        $resp = Invoke-RestMethod -Uri 'https://api.typesafe.ai/v1/systemone' -Method Post `
            -Headers @{ Authorization = "Bearer $apiKey" } -ContentType 'application/json; charset=utf-8' `
            -Body $bytes -TimeoutSec 30
        return @{ Ok = $true; Response = $resp; Error = $null }
    } catch {
        return @{ Ok = $false; Response = $null; Error = $_.Exception.Message }
    }
}
