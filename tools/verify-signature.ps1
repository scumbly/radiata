# Verifies each file's Authenticode signature is Valid AND carries an RFC 3161 timestamp; exits non-zero
# if any file fails. Used by release.yml after each signing pass (the inner executables, then the setup
# exe), and by hand on a downloaded draft-release asset.
# Every file is checked and reported before the exit code is decided, so one run names all failures.
# An untimestamped signature from Artifact Signing stops validating when its certificate expires
# (days), which is why the timestamp is checked separately from Status.
#   verify-signature.ps1 a.exe
#   verify-signature.ps1 a.exe b.exe -ExpectSubject "Name"
#   verify-signature.ps1 -Path a.exe, b.exe   (in-process only; under powershell -File use separate arguments)
param(
    [Parameter(Mandatory, Position = 0, ValueFromRemainingArguments = $true)][string[]]$Path,
    # Optional: fail unless every signer's subject contains this text (e.g. the CN).
    [string]$ExpectSubject
)

$ErrorActionPreference = 'Stop'
$failedFiles = 0

foreach ($p in $Path) {
    $fail = @()
    try {
        $full = (Resolve-Path -LiteralPath $p).Path
        $sig  = Get-AuthenticodeSignature -LiteralPath $full

        Write-Host "File:      $full"
        Write-Host "Status:    $($sig.Status) $($sig.StatusMessage)"
        Write-Host "Signer:    $($sig.SignerCertificate.Subject)"
        Write-Host "Timestamp: $($sig.TimeStamperCertificate.Subject)"

        if ($sig.Status -ne 'Valid')           { $fail += "signature status is $($sig.Status)" }
        if (-not $sig.TimeStamperCertificate)  { $fail += 'no timestamp' }
        if ($ExpectSubject -and ($sig.SignerCertificate.Subject -notlike "*$ExpectSubject*")) {
            $fail += "signer subject does not contain '$ExpectSubject'"
        }
    } catch {
        Write-Host "File:      $p"
        $fail += "could not be checked: $($_.Exception.Message)"
    }

    if ($fail.Count) {
        Write-Host "FAIL: $($fail -join '; ')"
        $failedFiles++
    } else {
        Write-Host 'OK: signed and timestamped'
    }
    Write-Host ''
}

if ($Path.Count -gt 1) {
    if ($failedFiles) { Write-Host "FAIL: $failedFiles of $($Path.Count) files failed verification" }
    else              { Write-Host "OK: all $($Path.Count) files signed and timestamped" }
}
if ($failedFiles) { exit 1 }
