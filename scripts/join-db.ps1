<#
.SYNOPSIS
  Reassembles Collection.db from the split parts and verifies the SHA256 checksum.
.EXAMPLE
  pwsh scripts/join-db.ps1 -PartsDir db-parts -Output Collection.db
#>
param(
    [string]$PartsDir = "db-parts",
    [string]$Output = "Collection.db"
)

$ErrorActionPreference = "Stop"

$parts = Get-ChildItem -Path $PartsDir -File |
    Where-Object { $_.Name -match '^Collection\.db\.\d{3}$' } |
    Sort-Object Name
if ($parts.Count -eq 0) {
    throw "No parts found in $PartsDir (expected Collection.db.000, .001, ...)"
}

$out = [System.IO.File]::Create($Output)
try {
    foreach ($p in $parts) {
        $bytes = [System.IO.File]::OpenRead($p.FullName)
        try { $bytes.CopyTo($out) } finally { $bytes.Dispose() }
        Write-Host "Appended $($p.Name)"
    }
} finally {
    $out.Dispose()
}

$shaFile = Join-Path $PartsDir "Collection.db.sha256"
if (Test-Path $shaFile) {
    $expected = ((Get-Content $shaFile) -split '\s+')[0].ToLower()
    $actual = (Get-FileHash -Path $Output -Algorithm SHA256).Hash.ToLower()
    if ($expected -ne $actual) {
        throw "Checksum mismatch: expected $expected, got $actual"
    }
    Write-Host "Checksum OK ($actual)"
}

Write-Host "Joined -> $Output ($((Get-Item $Output).Length) bytes)"
