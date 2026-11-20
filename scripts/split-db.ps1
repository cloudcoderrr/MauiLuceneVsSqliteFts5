<#
.SYNOPSIS
  Splits the large Test/Collection.db into fixed-size parts so they can be uploaded as
  GitHub Release assets (each well under the 100 MB file limit) and reassembled at test time.

.EXAMPLE
  pwsh scripts/split-db.ps1 -Source Test/Collection.db -OutDir db-parts -PartSizeMB 90
#>
param(
    [string]$Source = "Test/Collection.db",
    [string]$OutDir = "db-parts",
    [int]$PartSizeMB = 90
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Source)) {
    throw "Source not found: $Source"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Get-ChildItem -Path $OutDir -Filter "Collection.db.*" -ErrorAction SilentlyContinue | Remove-Item -Force

$partSize = $PartSizeMB * 1MB
$buffer = New-Object byte[] (4MB)
$in = [System.IO.File]::OpenRead($Source)
try {
    $index = 0
    while ($true) {
        $partPath = Join-Path $OutDir ("Collection.db.{0:D3}" -f $index)
        $out = [System.IO.File]::Create($partPath)
        $written = 0L
        try {
            while ($written -lt $partSize) {
                $toRead = [Math]::Min($buffer.Length, $partSize - $written)
                $read = $in.Read($buffer, 0, [int]$toRead)
                if ($read -le 0) { break }
                $out.Write($buffer, 0, $read)
                $written += $read
            }
        } finally {
            $out.Dispose()
        }

        if ($written -eq 0) {
            Remove-Item $partPath -Force
            break
        }

        Write-Host ("Wrote {0} ({1:N0} bytes)" -f $partPath, $written)
        $index++
        if ($in.Position -ge $in.Length) { break }
    }
} finally {
    $in.Dispose()
}

$hash = (Get-FileHash -Path $Source -Algorithm SHA256).Hash.ToLower()
$shaFile = Join-Path $OutDir "Collection.db.sha256"
"$hash  Collection.db" | Set-Content -Path $shaFile -NoNewline
Write-Host "SHA256: $hash -> $shaFile"
Write-Host "Done. Parts in $OutDir"
