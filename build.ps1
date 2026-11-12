param(
    [string]$MSBuildPath,
    [string]$WindowsMetadataPath
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$programFilesX86 = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')

if (!$MSBuildPath) {
    $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $MSBuildPath = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\MSBuild.exe' |
            Select-Object -First 1
    }
    if (!$MSBuildPath) {
        $MSBuildPath = (Get-Command MSBuild.exe, dotnet.exe -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    }
    if (!$MSBuildPath) {
        throw 'Install Visual Studio Build Tools or the .NET SDK, or pass -MSBuildPath.'
    }
}

if (!$WindowsMetadataPath) {
    $sdk = Join-Path $programFilesX86 'Windows Kits\10\UnionMetadata'
    $WindowsMetadataPath = Get-ChildItem -LiteralPath $sdk -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -as [version] } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName 'Windows.winmd' } |
        Where-Object { Test-Path -LiteralPath $_ } |
        Select-Object -First 1
}
if (!$WindowsMetadataPath -or !(Test-Path -LiteralPath $WindowsMetadataPath)) {
    throw 'Install the Windows SDK or pass -WindowsMetadataPath pointing to its Windows.winmd.'
}
$WindowsMetadataPath = (Resolve-Path -LiteralPath $WindowsMetadataPath).Path

function Build-Project([string]$project) {
    $buildArgs = @("$root\$project", "/p:Configuration=Release", "/p:WindowsMetadataPath=$WindowsMetadataPath", "/v:minimal")
    if ([IO.Path]::GetFileNameWithoutExtension($MSBuildPath) -eq 'dotnet') {
        $buildArgs = @('msbuild') + $buildArgs
    }
    & $MSBuildPath @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $project."
    }
}

Write-Host 'Building Windows Dynamic Island (Release x64)' -ForegroundColor Cyan
Build-Project 'src\WindowsDynamicIsland\WindowsDynamicIsland.csproj'
Copy-Item "$root\assets\app.ico" "$root\bin\Release\app.ico" -Force
New-Item -ItemType Directory -Force -Path "$root\bin\Release\Assets\Sounds" | Out-Null
Copy-Item "$root\assets\Sounds\*" "$root\bin\Release\Assets\Sounds\" -Force

Write-Host 'Building and running automated tests' -ForegroundColor Cyan
Build-Project 'tests\WindowsDynamicIsland.Tests\WindowsDynamicIsland.Tests.csproj'
& "$root\bin\Tests\WindowsDynamicIsland.Tests.exe"
if ($LASTEXITCODE -ne 0) {
    throw 'Automated tests failed.'
}

Write-Host 'Building standalone installer' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "$root\dist" | Out-Null
Copy-Item "$root\bin\Release\WindowsDynamicIsland.exe" "$root\dist\WindowsDynamicIsland.exe" -Force
Build-Project 'installer\WindowsDynamicIslandSetup.csproj'

Get-ChildItem "$root\dist\*.exe" | Select-Object Name, Length, FullName
