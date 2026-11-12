param(
    [Parameter(Mandatory = $true)][string]$MakeAppxPath,
    [string]$PresentMonPath,
    [string]$PresentMonLicense
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$release = Join-Path $root 'bin\Release'
if (!(Test-Path (Join-Path $release 'WindowsDynamicIsland.exe'))) {
    throw 'Run build.ps1 to produce the Release build first.'
}
$staging = Join-Path $root ('obj\Msix-' + [Guid]::NewGuid().ToString('N'))
$assetDir = Join-Path $staging 'Assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
try {
    Copy-Item (Join-Path $PSScriptRoot 'Package.appxmanifest') (Join-Path $staging 'AppxManifest.xml')
    Copy-Item (Join-Path $release 'WindowsDynamicIsland.exe'), (Join-Path $root 'assets\app.ico') $staging
    Copy-Item (Join-Path $root 'assets\Sounds') $assetDir -Recurse
    if ($PresentMonPath) {
        if (!$PresentMonLicense -or !(Test-Path -LiteralPath $PresentMonLicense)) {
            throw 'Pass the upstream PresentMon LICENSE.txt as -PresentMonLicense.'
        }
        if ((Get-FileHash -LiteralPath $PresentMonPath).Hash -ne 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF') {
            throw 'Expected the official PresentMon 2.6.0 x64 executable.'
        }
        $toolsDir = New-Item -ItemType Directory -Path (Join-Path $staging 'Tools')
        Copy-Item -LiteralPath $PresentMonPath -Destination (Join-Path $toolsDir.FullName 'PresentMon.exe')
        Copy-Item -LiteralPath $PresentMonLicense -Destination (Join-Path $toolsDir.FullName 'PresentMon-LICENSE.txt')
    }
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Image]::FromFile((Join-Path $root 'assets\logo.png'))
    try {
        foreach ($asset in @{ 'StoreLogo.png' = 50; 'Icon44.png' = 44; 'Icon150.png' = 150 }.GetEnumerator()) {
            $bitmap = [System.Drawing.Bitmap]::new($image, $asset.Value, $asset.Value)
            try { $bitmap.Save((Join-Path $assetDir $asset.Key), [System.Drawing.Imaging.ImageFormat]::Png) }
            finally { $bitmap.Dispose() }
        }
    }
    finally { $image.Dispose() }

    $out = Join-Path $root 'dist\WindowsDynamicIsland.msix'
    & $MakeAppxPath pack /d $staging /p $out /o
    if ($LASTEXITCODE) { throw 'MakeAppx failed.' }
    Get-Item -LiteralPath $out
}
finally {
    $resolvedStage = (Resolve-Path -LiteralPath $staging).Path
    $objRoot = (Resolve-Path -LiteralPath (Join-Path $root 'obj')).Path + [IO.Path]::DirectorySeparatorChar
    if ($resolvedStage.StartsWith($objRoot, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
