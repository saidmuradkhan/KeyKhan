#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the tests and produces a self-contained artifacts\publish\KeyKhan.exe
    and, with -Installer, artifacts\installer\KeyKhan-Setup.exe.
.PARAMETER Version
    Version stamped into the executable and installer. Defaults to <Version> in Directory.Build.props.
.PARAMETER Installer
    Also build the installer (requires Inno Setup 6: winget install JRSoftware.InnoSetup).
.PARAMETER SkipTests
    Package without running the test suite.
#>
param(
    [string]$Version,
    [switch]$Installer,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Step([string]$Name, [scriptblock]$Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

function Find-InnoCompiler {
    $candidates = @(
        (Get-Command iscc -ErrorAction SilentlyContinue).Source
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK not found. Install it with: winget install Microsoft.DotNet.SDK.10'
}
if (-not $Version) {
    $Version = ([xml](Get-Content Directory.Build.props)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}

if (-not $SkipTests) {
    Invoke-Step 'Test' { dotnet test --project tests/KeyKhan.Vault.Tests -c Release }
}

Invoke-Step "Publish $Version" {
    dotnet publish src/KeyKhan.App -c Release --self-contained -p:DebugType=none "-p:Version=$Version" -o artifacts/publish
}

if ($Installer) {
    $iscc = Find-InnoCompiler
    if (-not $iscc) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }
    Invoke-Step 'Installer' { & $iscc /Qp "/DAppVersion=$Version" installer/KeyKhan.iss }
    Write-Host "Installer: $(Resolve-Path artifacts/installer/KeyKhan-Setup.exe)" -ForegroundColor Green
}

Write-Host "Portable:  $(Resolve-Path artifacts/publish/KeyKhan.exe)" -ForegroundColor Green
