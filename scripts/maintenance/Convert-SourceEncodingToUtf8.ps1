<#
.SYNOPSIS
    Rewrites UTF-16 encoded repository text files as UTF-8 without a BOM.

.DESCRIPTION
    Editors and tooling occasionally save C#, XAML or script files as UTF-16,
    which the C# compiler rejects with a wall of "unexpected character" errors.
    This script finds those files and converts them in place.

.PARAMETER Check
    Reports offending files and exits with code 1 instead of rewriting them.
    Intended for use as a CI gate.
#>
[CmdletBinding()]
param(
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$textExtensions = @(
    ".cs", ".xaml", ".csproj", ".props", ".targets", ".sln", ".json", ".xml",
    ".resw", ".resx", ".appxmanifest", ".manifest", ".config", ".md", ".ps1",
    ".psm1", ".py", ".yml", ".yaml", ".editorconfig", ".txt")

function Test-Utf16Bytes {
    param([byte[]]$Bytes)

    if ($Bytes.Length -lt 4) {
        return $false
    }

    if (($Bytes[0] -eq 0xFF -and $Bytes[1] -eq 0xFE) -or
        ($Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF)) {
        return $true
    }

    # UTF-16 without a BOM: ASCII content leaves a zero in every second byte.
    $sample = [Math]::Min($Bytes.Length, 512)
    $zeroes = 0
    for ($index = 1; $index -lt $sample; $index += 2) {
        if ($Bytes[$index] -eq 0) { $zeroes++ }
    }

    return $zeroes -gt ($sample / 4)
}

function Get-Utf16Encoding {
    param([byte[]]$Bytes)

    if ($Bytes[0] -eq 0xFE -and $Bytes[1] -eq 0xFF) {
        return [System.Text.Encoding]::BigEndianUnicode
    }

    return [System.Text.Encoding]::Unicode
}

$utf8 = New-Object System.Text.UTF8Encoding($false)
$converted = @()

Get-ChildItem -LiteralPath $repositoryRoot -Recurse -File |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj|\.git|\.vs|AppPackages|artifacts|node_modules)\\' -and
        $textExtensions -contains $_.Extension.ToLowerInvariant()
    } |
    ForEach-Object {
        $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
        if (-not (Test-Utf16Bytes -Bytes $bytes)) {
            return
        }

        $relative = $_.FullName.Substring($repositoryRoot.Length + 1)
        $converted += $relative
        if ($Check) {
            return
        }

        $text = (Get-Utf16Encoding -Bytes $bytes).GetString($bytes).TrimStart([char]0xFEFF)
        [System.IO.File]::WriteAllText($_.FullName, $text, $utf8)
    }

if ($converted.Count -eq 0) {
    Write-Host "All repository text files are UTF-8."
    exit 0
}

$verb = if ($Check) { "UTF-16 files found" } else { "Converted to UTF-8" }
Write-Host "$verb ($($converted.Count)):"
$converted | ForEach-Object { Write-Host "  $_" }

if ($Check) {
    exit 1
}
