$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$tokens = $null
$errors = $null
$tree = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Read-WindowsRecycleMetadata.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'The metadata reader has syntax errors.' }
$function = $tree.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'ConvertFrom-RecycleMetadata'
}, $false)
if ($null -eq $function) { throw 'Metadata parser not found.' }
. ([scriptblock]::Create($function.Extent.Text))

function New-FictionalMetadata([string]$Path) {
    $pathBytes = [Text.Encoding]::Unicode.GetBytes($Path + [char]0)
    $bytes = New-Object byte[] (28 + $pathBytes.Length)
    [BitConverter]::GetBytes([uint64]2).CopyTo($bytes, 0)
    [BitConverter]::GetBytes([uint64]2097152).CopyTo($bytes, 8)
    [BitConverter]::GetBytes([DateTime]::Parse('2026-10-16T18:46:21Z').ToUniversalTime().ToFileTimeUtc()).CopyTo($bytes, 16)
    [BitConverter]::GetBytes([uint32]($pathBytes.Length / 2)).CopyTo($bytes, 24)
    $pathBytes.CopyTo($bytes, 28)
    return ,$bytes
}

function Assert-Rejected([byte[]]$Bytes) {
    $rejected = $false
    try { $null = ConvertFrom-RecycleMetadata $Bytes } catch { $rejected = $true }
    if (-not $rejected) { throw 'Malformed metadata was accepted.' }
}

$path = 'C:\FictionalFixtures\probe-oversized.bin'
$valid = New-FictionalMetadata $path
$decoded = ConvertFrom-RecycleMetadata $valid
if ($decoded.OriginalPath -ne $path -or $decoded.OriginalBytes -ne 2097152 -or
    $decoded.DeletedAtUtc -ne '2026-10-16T18:46:21.0000000Z') { throw 'Metadata fields were decoded incorrectly.' }

$unicodePath = 'C:\FictionalFixtures\' + [char]0x03A9 + '.bin'
if ((ConvertFrom-RecycleMetadata (New-FictionalMetadata $unicodePath)).OriginalPath -ne $unicodePath) {
    throw 'Unicode paths must be preserved.'
}
Assert-Rejected ([byte[]]@(0, 1, 2))
$unsupported = [byte[]]$valid.Clone()
$unsupported[0] = 3
Assert-Rejected $unsupported
$overflow = [byte[]]$valid.Clone()
[BitConverter]::GetBytes([uint32]::MaxValue).CopyTo($overflow, 24)
Assert-Rejected $overflow
$unterminated = [byte[]]$valid.Clone()
$unterminated[$unterminated.Length - 2] = 65
Assert-Rejected $unterminated
$invalidUnicode = [byte[]]$valid.Clone()
$invalidUnicode[28] = 0
$invalidUnicode[29] = 0xD8
Assert-Rejected $invalidUnicode
$invalidTime = [byte[]]$valid.Clone()
[BitConverter]::GetBytes([int64]::MaxValue).CopyTo($invalidTime, 16)
Assert-Rejected $invalidTime
Assert-Rejected (New-FictionalMetadata ('C:\Fake' + [char]0 + 'hidden.bin'))

Write-Host 'Nine in-memory metadata decoding cases passed. No guest, bin or fixture file was read or changed.'
