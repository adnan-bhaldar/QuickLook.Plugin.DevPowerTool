# ============================================================
# QuickLook.Plugin.DevPowerTool — Scripts/pack-zip.ps1
#
# Mirrors the HelloWorld pack-zip.ps1 approach:
#   1. Expects a successful Release build to exist in bin\Release\
#   2. Zips the required plugin files
#   3. Renames the zip to .qlplugin
#
# Run from the project root:
#   powershell -ExecutionPolicy Bypass -File Scripts\pack-zip.ps1
# ============================================================

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# ── Paths ──────────────────────────────────────────────────────────────────
$ProjectRoot  = Split-Path -Parent $PSScriptRoot
$BinDir       = Join-Path $ProjectRoot "bin\$Configuration"
$PluginName   = "QuickLook.Plugin.DevPowerTool"
$OutputZip    = Join-Path $ProjectRoot "$PluginName.qlplugin"

# ── Verify build output exists ─────────────────────────────────────────────
$MainDll = Join-Path $BinDir "$PluginName.dll"
if (-not (Test-Path $MainDll)) {
    Write-Error "Build output not found at: $MainDll`nPlease build the project in '$Configuration' mode first."
    exit 1
}

# ── Files to include in the .qlplugin package ─────────────────────────────
# QuickLook loads everything in the plugin folder; include the DLL,
# the metadata config, and any dependency DLLs that are NOT already
# provided by QuickLook itself (QuickLook.Common.dll is NOT included
# because QuickLook ships it).
$Include = @(
    "$PluginName.dll",
    "QuickLook.Plugin.Metadata.config"
)

# ── Create temp staging folder ────────────────────────────────────────────
$TempDir = Join-Path $env:TEMP "ql_pack_$([System.Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $TempDir | Out-Null

try {
    foreach ($file in $Include) {
        $src = Join-Path $BinDir $file
        if (Test-Path $src) {
            Copy-Item $src -Destination $TempDir
            Write-Host "  + $file"
        } else {
            Write-Warning "  ! $file not found — skipping"
        }
    }

    # ── Zip it ────────────────────────────────────────────────────────────
    if (Test-Path $OutputZip) { Remove-Item $OutputZip -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($TempDir, $OutputZip)

    Write-Host ""
    Write-Host "✅ Plugin packaged: $OutputZip" -ForegroundColor Green
    Write-Host "   Open QuickLook, press Space on the .qlplugin file, then click Install."
}
finally {
    Remove-Item $TempDir -Recurse -Force
}
