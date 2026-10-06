# Builds Radiata-<ver>-setup.exe + publish\latest.json. Runs locally (the release ritual) and in
# .github/workflows/release.yml (with -Public).
# Windows PowerShell 5.1 compatible (no &&, no ternary, no ??). Does NOT touch the ZIP packaging flow.
#
# One-shot (default): derive <ver> -> publish Radiata and its self-contained helper into a new staging
# folder -> strip PDBs -> refresh the helper manifest -> remove ZIP-only files -> (-Public) Help silo
# check -> compile packaging\radiata.iss -> hash -> latest.json.
#
# Split run, so the inner executables can be code-signed between the two halves (docs/INSTALLER.md,
# Signing):
#   -StageOnly           derive <ver>, publish, strip PDBs, record the version in the staging folder,
#                        print the folder and write it to <publish>\staging-path.txt, then stop.
#   -FromStaging <dir>   take that folder as the payload and run every step after the PDB strip, using
#                        the version recorded in it.
# The helper manifest (Radiata.helper-files.txt) hashes the ArcadeHost files and signing rewrites the
# helper exe, so the manifest is refreshed only in the second half.
# Running apps are not stopped by this script; a locked build output is reported as a build failure.
#
# See docs/INSTALLER.md for the contracts (AppId, payload layout, latest.json schema).

# -Public: defines PUBLIC_RELEASE in every project. Core\ReleaseGates.cs reads it: dev-only switches
# compile out and the gated features are not registered. Without it the full-feature flavour is built.
# The silo check proves the gated features left the public build by exporting its Help set. With -FromStaging, -Public
# must match the flavour the staging folder was published with.
param(
    [switch]$Public,
    [switch]$StageOnly,
    [string]$FromStaging,
    [string]$OutputDirectory
)
$publicProp = if ($Public) { '/p:RadiataPublic=true' } else { '/p:RadiataPublic=false' }
$ErrorActionPreference = 'Stop'

# Written into the staging folder by -StageOnly: line 1 = version, line 2 = public | tester. It is not
# payload, so the compile step moves it out of the folder while ISCC packs the folder.
$stagingRecordName = 'staging-version.txt'

function Fail([string]$msg) {
    Write-Host ""
    Write-Host "BUILD-INSTALLER FAILED: $msg" -ForegroundColor Red
    exit 1
}

function Step([string]$msg) {
    Write-Host ""
    Write-Host "==> $msg" -ForegroundColor Cyan
}

function FileSha256([string]$path) {
    $hasher = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $hasher.Dispose() }
}

# --- Version: <ReleaseMajor>.<ReleaseMinor>.<build.minor> ------------------------------------------
function Get-SourceVersion([string]$repo) {
    $csprojPath = Join-Path $repo 'ControllerWheel.csproj'
    if (-not (Test-Path $csprojPath)) { Fail "ControllerWheel.csproj not found at $csprojPath" }
    [xml]$csproj = Get-Content $csprojPath
    $releaseMajor = ($csproj.Project.PropertyGroup | ForEach-Object { $_.ReleaseMajor } | Where-Object { $_ }) | Select-Object -First 1
    if (-not $releaseMajor) { Fail "ReleaseMajor not found in ControllerWheel.csproj" }
    $releaseMinor = ($csproj.Project.PropertyGroup | ForEach-Object { $_.ReleaseMinor } | Where-Object { $_ }) | Select-Object -First 1
    if (-not $releaseMinor) { Fail "ReleaseMinor not found in ControllerWheel.csproj" }
    $buildMinorPath = Join-Path $repo 'build.minor'
    if (-not (Test-Path $buildMinorPath)) { Fail "build.minor file not found at $buildMinorPath" }
    $buildMinor = (Get-Content $buildMinorPath -Raw).Trim()
    if ($buildMinor -notmatch '^\d+$') { Fail "build.minor content '$buildMinor' is not a number" }
    return [pscustomobject]@{
        Ver        = "$releaseMajor.$releaseMinor.$buildMinor"
        BuildMinor = $buildMinor
    }
}

# --- Staging record: the version (and flavour) a staging folder was published with --------------------
function Write-StagingRecord([string]$staging, [string]$ver, [bool]$isPublic) {
    $flavour = if ($isPublic) { 'public' } else { 'tester' }
    [IO.File]::WriteAllLines((Join-Path $staging $stagingRecordName), [string[]]@($ver, $flavour), [Text.UTF8Encoding]::new($false))
}

function Read-StagingRecord([string]$staging) {
    $path = Join-Path $staging $stagingRecordName
    if (-not (Test-Path -LiteralPath $path)) { Fail "$stagingRecordName not found in $staging. Run build-installer.ps1 -StageOnly to make a staging folder." }
    $lines = @([IO.File]::ReadAllLines($path) | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($lines.Count -lt 2 -or $lines[0] -notmatch '^\d+\.\d+\.\d+$' -or @('public', 'tester') -notcontains $lines[1]) {
        Fail "$path is not a staging record (expected a version line, then 'public' or 'tester')."
    }
    return [pscustomobject]@{ Ver = $lines[0]; IsPublic = ($lines[1] -eq 'public') }
}

# --- Staging layout: what the publish must have produced ----------------------------------------------
function Test-StagingLayout([string]$staging) {
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'Radiata.exe'))) { Fail "Radiata.exe missing from staging" }
    if (-not (Test-Path -LiteralPath (Join-Path $staging 'drivers')))     { Fail "drivers folder missing from staging (CopyDriversToPublish did not run?)" }
    # The app's publish target owns the complete helper payload and its manifest.
    $arcadeHostDir = Join-Path $staging 'ArcadeHost'
    foreach ($required in @('Radiata.ArcadeHost.exe', 'hostfxr.dll', 'coreclr.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $arcadeHostDir $required))) { Fail "Helper runtime file missing: $required" }
    }
}

# --- Stage: publish into a new staging directory, then strip PDBs --------------------------------------
# Preserves running apps and prior payloads.
function Invoke-Stage([string]$repo, [string]$staging, [string]$buildMinor, [string]$publicProp) {
    Step "Publishing Radiata to $staging"
    New-Item -ItemType Directory -Path $staging | Out-Null

    # /p:PinnedBuildMinor: the csproj's SetBuildVersion target re-reads build.minor, which can change
    # between commits. Pinning the value read when the version was derived keeps the payload's version
    # equal to the setup filename, the ARP entry and latest.json when it changes mid-build.
    dotnet publish (Join-Path $repo 'ControllerWheel.csproj') -c Release -r win-x64 --self-contained true -o $staging /p:PinnedBuildMinor=$buildMinor $publicProp
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish ControllerWheel.csproj exited $LASTEXITCODE" }
    Test-StagingLayout $staging

    Step "Stripping *.pdb"
    Get-ChildItem -LiteralPath $staging -Recurse -Filter '*.pdb' | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
}

# --- Helper manifest: hashes of the ArcadeHost files as they will ship ---------------------------------
# Runs after every step that rewrites those files (PDB strip, code signing).
function Update-HelperManifest([string]$staging) {
    $arcadeHostDir = Join-Path $staging 'ArcadeHost'
    $helperManifest = @(Get-ChildItem -LiteralPath $arcadeHostDir -File -Recurse | Sort-Object FullName | ForEach-Object {
        (FileSha256 $_.FullName) + '|' + $_.FullName.Substring($staging.Length + 1)
    })
    [IO.File]::WriteAllLines((Join-Path $staging 'Radiata.helper-files.txt'), [string[]]$helperManifest, [Text.UTF8Encoding]::new($false))
}

# --- ZIP-only files never ship in the installer payload ------------------------------------------------
# "Uninstall Radiata.cmd" is the portable uninstaller; the installer's generated uninstaller owns
# file removal here, and shipping both would offer two contradictory uninstall paths.
function Remove-ZipOnlyFiles([string]$staging) {
    $zipOnly = Join-Path $staging 'Uninstall Radiata.cmd'
    if (Test-Path $zipOnly) {
        Remove-Item -Force $zipOnly
        Write-Host "Removed: Uninstall Radiata.cmd"
    } else {
        Write-Host "Uninstall Radiata.cmd not present in staging (nothing to remove)"
    }
}

# --- Public-release silo check -------------------------------------------------------------------------
# The staged exe's own Help export (--export-controls) follows the build's ReleaseGates, so a withheld
# feature that still reaches Help (a new topic, a cross-link, a keyword) shows up here as its name.
# This is the ONLY check that runs against the public flavour (TestHarness runs against Debug). The
# same staged exe is what regenerates the public site pages (--export-help-html): docs/LOCALIZATION.md.
function Test-PublicSilo([string]$staging, [string]$publishRoot) {
    Step "Public-release silo check (Help export from the staged exe)"
    $siloOut = Join-Path $publishRoot ('public-controls-check-' + [Guid]::NewGuid().ToString('N') + '.md')
    $siloProc = Start-Process -FilePath (Join-Path $staging 'Radiata.exe') -ArgumentList @('--export-controls', "`"$siloOut`"") -WindowStyle Hidden -Wait -PassThru
    if ($siloProc.ExitCode -ne 0) { Fail "Staged Radiata.exe --export-controls exited $($siloProc.ExitCode)" }
    if (-not (Test-Path $siloOut)) { Fail "Staged Radiata.exe --export-controls wrote nothing to $siloOut" }
    $siloText = [System.IO.File]::ReadAllText($siloOut)
    $siloHits = @([regex]::Matches($siloText, '(?i)internode|custom-arcade-games|custom-materials|Show in Explorer|Packages\\(Materials|Arcade Games)') | ForEach-Object { $_.Value } | Sort-Object -Unique)
    if ($siloHits.Count -gt 0) { Fail ("The public build's Help still names a withheld feature: {0}. See Core\ReleaseGates.cs." -f ($siloHits -join ', ')) }
    Write-Host "Clean: the public Help export names none of the withheld features"
    Remove-Item -Force $siloOut
}

# --- Inno Setup compiler -------------------------------------------------------------------------------
function Find-Iscc {
    Step "Locating ISCC.exe"
    $iscc = $null
    $candidates = @()
    if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe') }
    if ($env:LOCALAPPDATA)        { $candidates += (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe') }
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) { $iscc = $c; break }
    }
    if (-not $iscc) {
        foreach ($hive in @('HKLM:', 'HKCU:')) {
            try {
                $ap = Get-ItemProperty "$hive\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\ISCC.exe" -ErrorAction Stop
                if ($ap.'(default)' -and (Test-Path $ap.'(default)')) { $iscc = $ap.'(default)'; break }
            } catch { }
        }
    }
    if (-not $iscc) { Fail "ISCC.exe not found (checked Program Files (x86), %LOCALAPPDATA%\Programs, and App Paths). Install Inno Setup 6." }
    Write-Host "ISCC: $iscc"
    return $iscc
}

# --- Everything after staging: manifest, silo check, compile, hash, latest.json ------------------------
function Complete-Installer([string]$repo, [string]$staging, [string]$ver, [string]$publishRoot, [bool]$isPublic) {
    $verNum = "$ver.0"

    # The manifest describes the bytes actually shipped, so it follows the PDB strip and any signing.
    Step "Refreshing the helper manifest"
    Update-HelperManifest $staging

    Step "Removing ZIP-only files"
    Remove-ZipOnlyFiles $staging

    if ($isPublic) { Test-PublicSilo $staging $publishRoot }

    $iscc = Find-Iscc

    Step "Compiling packaging\radiata.iss"
    $issPath = Join-Path $repo 'packaging\radiata.iss'
    if (-not (Test-Path $issPath)) { Fail "radiata.iss not found at $issPath" }

    # radiata.iss packs every file under the staging folder; the staging record is bookkeeping, not payload.
    $record = Join-Path $staging $stagingRecordName
    $aside = $null
    if (Test-Path -LiteralPath $record) {
        $aside = Join-Path (Split-Path -Parent $staging) ('staging-record-' + [Guid]::NewGuid().ToString('N') + '.tmp')
        Move-Item -LiteralPath $record -Destination $aside
    }
    try {
        & $iscc "/DAppVer=$ver" "/DAppVerNum=$verNum" "/DPayloadDir=$staging" "/O$publishRoot" $issPath
        $isccExit = $LASTEXITCODE
    } finally {
        if ($aside -and (Test-Path -LiteralPath $aside)) { Move-Item -LiteralPath $aside -Destination $record }
    }
    if ($isccExit -ne 0) { Fail "ISCC exited $isccExit" }

    $setupExe = Join-Path $publishRoot "Radiata-$ver-setup.exe"
    if (-not (Test-Path $setupExe)) { Fail "Expected installer not found: $setupExe" }

    Step "Hashing installer"
    $sha256 = FileSha256 $setupExe

    # latest.json: the exact update-feed schema, uploaded to getradiata.app/update after release.
    Step "Writing latest.json"
    $releases = 'https://github.com/scumbly/radiata/releases'
    $latestJson = '{"version":"' + $ver + '","url":"' + $releases + '/download/v' + $ver + '/Radiata-' + $ver + '-setup.exe","sha256":"' + $sha256 + '","notesUrl":"' + $releases + '/tag/v' + $ver + '"}'
    $latestPath = Join-Path $publishRoot 'latest.json'
    [System.IO.File]::WriteAllText($latestPath, $latestJson, (New-Object System.Text.UTF8Encoding($false)))

    $sizeMB = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
    Write-Host ""
    Write-Host "================= INSTALLER BUILD COMPLETE =================" -ForegroundColor Green
    Write-Host ("  Version     : {0}" -f $ver)
    Write-Host ("  Setup exe   : {0}" -f $setupExe)
    Write-Host ("  Size        : {0} MB" -f $sizeMB)
    Write-Host ("  SHA-256     : {0}" -f $sha256)
    Write-Host ("  latest.json : {0}" -f $latestPath)
    Write-Host "============================================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Release ritual: attach the setup exe to the GitHub release (tag v$ver), then upload"
    Write-Host "latest.json to the site's update/ directory (getradiata.app/update). See docs/INSTALLER.md."
}

# --- Main ----------------------------------------------------------------------------------------------
$fromStagingMode = $PSBoundParameters.ContainsKey('FromStaging')
if ($StageOnly -and $fromStagingMode) { Fail "-StageOnly and -FromStaging cannot be combined." }

# A relative -FromStaging is resolved against the caller's location, before this script changes it.
$stagingIn = $null
if ($fromStagingMode) {
    if ([string]::IsNullOrWhiteSpace($FromStaging)) { Fail "-FromStaging needs a staging folder path." }
    if (-not (Test-Path -LiteralPath $FromStaging -PathType Container)) { Fail "Staging folder not found: $FromStaging" }
    $stagingIn = (Resolve-Path -LiteralPath $FromStaging).ProviderPath.TrimEnd('\')
}

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
$publishRoot = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repo 'publish' }

if ($fromStagingMode) {
    $staging = $stagingIn
    Step "Using staging folder $staging"
    $record = Read-StagingRecord $staging
    if ($record.IsPublic -ne [bool]$Public) {
        $made = if ($record.IsPublic) { 'with -Public' } else { 'without -Public' }
        Fail "This staging folder was published $made; run -FromStaging the same way."
    }
    $ver = $record.Ver
    Write-Host "Version: $ver (from $stagingRecordName)"
    Test-StagingLayout $staging
} else {
    Step "Deriving version"
    $source = Get-SourceVersion $repo
    $ver = $source.Ver
    Write-Host "Version: $ver (VersionInfo $ver.0)"

    $staging = Join-Path $publishRoot ('installer-staging-' + [Guid]::NewGuid().ToString('N'))
    Invoke-Stage $repo $staging $source.BuildMinor $publicProp

    if ($StageOnly) {
        Write-StagingRecord $staging $ver ([bool]$Public)
        $pathFile = Join-Path $publishRoot 'staging-path.txt'
        [IO.File]::WriteAllLines($pathFile, [string[]]@($staging), [Text.UTF8Encoding]::new($false))
        Write-Host ""
        Write-Host "==================== STAGING COMPLETE ====================" -ForegroundColor Green
        Write-Host ("  Version      : {0}" -f $ver)
        Write-Host ("  Staging path : {0}" -f $staging)
        Write-Host ("  Path file    : {0}" -f $pathFile)
        Write-Host "==========================================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Sign the executables in the staging folder if the release is signed, then run:"
        Write-Host ("  tools\build-installer.ps1 {0}-FromStaging ""{1}""" -f $(if ($Public) { '-Public ' } else { '' }), $staging)
        exit 0
    }
}

Complete-Installer $repo $staging $ver $publishRoot ([bool]$Public)
