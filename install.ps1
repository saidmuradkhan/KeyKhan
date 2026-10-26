#Requires -Version 5.1
<#
.SYNOPSIS
    Installs KeyKhan for the current user (no administrator rights needed) and adds a Start menu shortcut.
.PARAMETER Source
    Path to KeyKhan.exe. Defaults to the file next to this script, then to artifacts\publish\KeyKhan.exe.
.PARAMETER Uninstall
    Removes the program and the shortcut. Your vault in %APPDATA%\KeyKhan is never touched.
#>
param(
    [string]$Source,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\KeyKhan'
$target = Join-Path $installDir 'KeyKhan.exe'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'KeyKhan.lnk'

Get-Process KeyKhan -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target } | Stop-Process -Force

if ($Uninstall) {
    Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $shortcut -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'KeyKhan' -ErrorAction SilentlyContinue
    Write-Host 'KeyKhan was uninstalled. Your vault is still in' (Join-Path $env:APPDATA 'KeyKhan')
    return
}

if (-not $Source) {
    $Source = @((Join-Path $PSScriptRoot 'KeyKhan.exe'), (Join-Path $PSScriptRoot 'artifacts\publish\KeyKhan.exe')) |
        Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $Source -or -not (Test-Path $Source)) {
    throw 'KeyKhan.exe not found. Download it from the Releases page or run .\build.ps1 first.'
}

New-Item -ItemType Directory -Force $installDir | Out-Null
Copy-Item $Source $target -Force

$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($shortcut)
$link.TargetPath = $target
$link.WorkingDirectory = $installDir
$link.Description = 'KeyKhan password manager'
$link.Save()

Write-Host "KeyKhan installed to $installDir and added to the Start menu."
Start-Process $target
