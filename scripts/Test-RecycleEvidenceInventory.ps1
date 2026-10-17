$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Load only the read-only inventory function. Never execute the guest collector or native probes.
$collector = Join-Path $PSScriptRoot 'Collect-WindowsRecycleEvidence.ps1'
$tokens = $null
$parseErrors = $null
$tree = [Management.Automation.Language.Parser]::ParseFile($collector, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'The collector has syntax errors.' }
$function = $tree.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-RawBinInventory'
}, $false)
if ($null -eq $function) { throw 'The inventory function was not found.' }
. ([scriptblock]::Create($function.Extent.Text))

$fixtureRoot = Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/acceptance/inventory-' + [Guid]::NewGuid().ToString('N'))
$rawDirectory = Join-Path $fixtureRoot 'fictional-bin'
New-Item -ItemType Directory -Path $rawDirectory | Out-Null

$empty = Get-RawBinInventory $rawDirectory
if ($empty.Status -ne 'Complete' -or $empty.Entries.Count -ne 0) {
    throw 'An accessible empty directory must be distinguishable from an incomplete inventory.'
}

$payload = Join-Path $rawDirectory '$Rfictional.bin'
[IO.File]::WriteAllText($payload, 'Fictional orphan payload for a read-only evidence regression.')
[IO.File]::SetAttributes($payload, [IO.FileAttributes]::Hidden)
$expectedHash = (Get-FileHash -LiteralPath $payload).Hash
$orphan = Get-RawBinInventory $rawDirectory
if ($orphan.Status -ne 'Complete' -or $orphan.Entries.Count -ne 1 -or
    $orphan.Entries[0].Name -ne '$Rfictional.bin' -or $orphan.Entries[0].Sha256 -ne $expectedHash) {
    throw 'A hidden payload without shell metadata must still be inventoried by hash.'
}
if ((Get-FileHash -LiteralPath $payload).Hash -ne $expectedHash -or
    -not ((Get-Item -LiteralPath $payload -Force).Attributes -band [IO.FileAttributes]::Hidden)) {
    throw 'Inventory must preserve file content and attributes.'
}

$missing = Get-RawBinInventory (Join-Path $fixtureRoot 'absent')
if ($missing.Status -ne 'Incomplete' -or -not $missing.Error) {
    throw 'A missing or unreadable directory must not be reported as an empty successful read.'
}

$nested = Join-Path $rawDirectory 'nested'
New-Item -ItemType Directory -Path $nested | Out-Null
$withDirectory = Get-RawBinInventory $rawDirectory
if ($withDirectory.Status -ne 'Incomplete' -or $withDirectory.Entries.Count -ne 2) {
    throw 'Uninspected directories must be visible as incomplete coverage.'
}

$redirected = Join-Path $fixtureRoot 'redirected'
New-Item -ItemType Junction -Path $redirected -Target $rawDirectory | Out-Null
$withJunction = Get-RawBinInventory $redirected
if ($withJunction.Status -ne 'Incomplete' -or $withJunction.Entries.Count -ne 0 -or -not $withJunction.Error) {
    throw 'A redirected inventory root must be refused without enumeration.'
}

Write-Host 'Five read-only inventory cases passed. No native Recycle Bin operation was invoked.'
Write-Host "Fictional fixtures retained at: $fixtureRoot"
