param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[a-p]{32}$')][string]$ExtensionId,
    [Parameter(Mandatory = $true)][string]$IslandExe,
    [ValidateSet('Chrome', 'Edge')][string]$Browser = 'Chrome'
)
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $IslandExe).Path
if ([IO.Path]::GetFileName($exe) -ne 'WindowsDynamicIsland.exe') { throw 'Expected WindowsDynamicIsland.exe' }
$dir = Join-Path $env:LOCALAPPDATA 'WindowsDynamicIsland\BrowserHost'
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$hostExe = Join-Path $dir 'WindowsDynamicIsland.exe'
if ($exe -ne $hostExe) { Copy-Item -LiteralPath $exe -Destination $hostExe -Force }
$name = 'com.paraxyoz.dynamic_island'
$manifest = Join-Path $dir ($name + '-' + $Browser.ToLowerInvariant() + '.json')
$data = @{
    name = $name
    description = 'Windows Dynamic Island download progress'
    path = $hostExe
    type = 'stdio'
    allowed_origins = @("chrome-extension://$ExtensionId/")
}
[IO.File]::WriteAllText($manifest, ($data | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$base = if ($Browser -eq 'Edge') { 'HKCU:\Software\Microsoft\Edge' } else { 'HKCU:\Software\Google\Chrome' }
$key = Join-Path $base ('NativeMessagingHosts\' + $name)
New-Item -Path $key -Force | Out-Null
Set-Item -Path $key -Value $manifest
Write-Output "Registered $Browser host for $ExtensionId."
