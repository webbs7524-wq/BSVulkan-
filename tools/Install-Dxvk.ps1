param(
    [string]$BeatSaberDir = "C:\Users\webbs\BSManager\BSInstances\1.40.8 (3)",
    [switch]$EnableHud
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath (Join-Path $BeatSaberDir "Beat Saber.exe"))) {
    throw "Beat Saber.exe was not found in '$BeatSaberDir'. Pass -BeatSaberDir with the correct game folder."
}

$release = Invoke-RestMethod -Uri "https://api.github.com/repos/doitsujin/dxvk/releases/latest"
$asset = $release.assets |
    Where-Object { $_.name -like "dxvk-*.tar.gz" -and $_.name -notlike "*native*" } |
    Select-Object -First 1

if ($null -eq $asset) {
    throw "Could not find a DXVK release archive in the latest GitHub release."
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "BSVulkan-DXVK"
$resolvedTempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)

if (-not $resolvedTempRoot.StartsWith($resolvedTempParent, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean temp directory outside the system temp folder: '$resolvedTempRoot'"
}

if (Test-Path -LiteralPath $tempRoot) {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $tempRoot | Out-Null

$archivePath = Join-Path $tempRoot $asset.name
Write-Host "Downloading $($release.tag_name) from $($asset.browser_download_url)"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath

tar -xzf $archivePath -C $tempRoot

$dxvkDir = Get-ChildItem -LiteralPath $tempRoot -Directory |
    Where-Object { $_.Name -like "dxvk-*" } |
    Select-Object -First 1

if ($null -eq $dxvkDir) {
    throw "The DXVK archive did not contain the expected dxvk-* directory."
}

$x64Dir = Join-Path $dxvkDir.FullName "x64"
foreach ($dllName in @("d3d11.dll", "dxgi.dll")) {
    $source = Join-Path $x64Dir $dllName
    $destination = Join-Path $BeatSaberDir $dllName

    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing '$source' in the DXVK archive."
    }

    if (Test-Path -LiteralPath $destination) {
        $backup = "$destination.bak.$(Get-Date -Format 'yyyyMMddHHmmss')"
        Move-Item -LiteralPath $destination -Destination $backup
        Write-Host "Backed up existing $dllName to $backup"
    }

    Copy-Item -LiteralPath $source -Destination $destination
    Write-Host "Installed $dllName"
}

if ($EnableHud) {
    $configPath = Join-Path $BeatSaberDir "dxvk.conf"
    "dxvk.hud = version,api,devinfo,fps" | Set-Content -LiteralPath $configPath -Encoding ASCII
    Write-Host "Enabled DXVK HUD in $configPath"
}

Write-Host "DXVK $($release.tag_name) installed into $BeatSaberDir"
