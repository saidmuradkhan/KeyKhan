#Requires -Version 5.1
<#
.SYNOPSIS
    Runs the tests and produces a self-contained artifacts\publish\KeyKhan.exe.
.PARAMETER Version
    Version stamped into the executable, e.g. 1.2.0.
.PARAMETER SkipTests
    Package without running the test suite.
#>
param(
    [string]$Version,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Invoke-Step([string]$Name, [scriptblock]$Command) {
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE." }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK not found. Install it with: winget install Microsoft.DotNet.SDK.10'
}

$versionArgs = @(if ($Version) { "-p:Version=$Version" })

if (-not $SkipTests) {
    Invoke-Step 'Test' { dotnet test --project tests/KeyKhan.Vault.Tests -c Release }
}

Invoke-Step 'Publish' {
    dotnet publish src/KeyKhan.App -c Release --self-contained -p:DebugType=none @versionArgs -o artifacts/publish
}

Write-Host "`nDone: $(Resolve-Path artifacts/publish/KeyKhan.exe)" -ForegroundColor Green
