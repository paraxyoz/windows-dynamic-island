$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
if (!(Test-Path $msbuild)) {
    $msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
}

Get-Process -Name "WindowsDynamicIsland" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 200

Write-Host "=== 1. Building WindowsDynamicIsland (Release x64) ===" -ForegroundColor Cyan
& $msbuild "$root\src\WindowsDynamicIsland\WindowsDynamicIsland.csproj" /p:Configuration=Release /v:minimal
Copy-Item "$root\assets\app.ico" "$root\bin\Release\app.ico" -Force
if (Test-Path "$root\assets\Sounds") {
    New-Item -ItemType Directory -Force -Path "$root\bin\Release\Assets\Sounds" | Out-Null
    Copy-Item "$root\assets\Sounds\*" "$root\bin\Release\Assets\Sounds\" -Recurse -Force
}

Write-Host "=== 2. Building and running automated unit & integration tests ===" -ForegroundColor Cyan
& $msbuild "$root\tests\WindowsDynamicIsland.Tests\WindowsDynamicIsland.Tests.csproj" /p:Configuration=Release /v:minimal
& "$root\bin\Tests\WindowsDynamicIsland.Tests.exe"
if ($LASTEXITCODE -ne 0) {
    throw "Automated tests failed."
}

Write-Host "=== 3. Building standalone installer WindowsDynamicIslandSetup.exe ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path "$root\dist" | Out-Null
Copy-Item "$root\bin\Release\WindowsDynamicIsland.exe" "$root\dist\WindowsDynamicIsland.exe" -Force
Copy-Item "$root\assets\app.ico" "$root\dist\app.ico" -Force
if (Test-Path "$root\assets\Sounds") {
    New-Item -ItemType Directory -Force -Path "$root\dist\Assets\Sounds" | Out-Null
    Copy-Item "$root\assets\Sounds\*" "$root\dist\Assets\Sounds\" -Recurse -Force
}
& $msbuild "$root\installer\WindowsDynamicIslandSetup.csproj" /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed."
}

Write-Host "=== Build completed successfully! Output files in dist\ ===" -ForegroundColor Green
Get-ChildItem "$root\dist\*.exe" | Select-Object Name, Length, FullName
