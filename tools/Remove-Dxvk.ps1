param(
    [string]$BeatSaberDir = "C:\Users\webbs\BSManager\BSInstances\1.40.8 (3)"
)

$ErrorActionPreference = "Stop"

foreach ($name in @("d3d11.dll", "dxgi.dll", "dxvk.conf")) {
    $path = Join-Path $BeatSaberDir $name
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
        Write-Host "Removed $name"
    }
}

Write-Host "DXVK wrapper files removed from $BeatSaberDir"
