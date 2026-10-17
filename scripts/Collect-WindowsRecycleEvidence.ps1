param([switch]$IncludeRawBinInventory)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-RawBinInventory([string]$Directory) {
    $entries = [Collections.Generic.List[object]]::new()
    $status = 'Complete'
    $failure = $null
    try {
        $ancestor = [IO.Path]::GetFullPath($Directory)
        while ($ancestor) {
            $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Refusing a redirected raw bin path.'
            }
            $ancestor = [IO.Path]::GetDirectoryName($ancestor)
        }
        foreach ($item in Get-ChildItem -LiteralPath $Directory -Force -ErrorAction Stop) {
            $hash = $null
            $entryError = $null
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                $entryError = 'Reparse point; not followed.'
            }
            elseif ($item.PSIsContainer) {
                $entryError = 'Directory; not traversed. The probes create individual files only.'
            }
            else {
                try { $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256 -ErrorAction Stop).Hash }
                catch { $entryError = $_.Exception.Message }
            }
            if ($entryError) { $status = 'Incomplete' }
            $entries.Add([ordered]@{
                Name = $item.Name
                Bytes = if ($item.PSIsContainer) { $null } else { $item.Length }
                Sha256 = $hash
                Error = $entryError
            })
        }
    }
    catch {
        $status = 'Incomplete'
        $failure = $_.Exception.Message
    }
    return [ordered]@{ Directory = $Directory; Status = $status; Entries = $entries.ToArray(); Error = $failure }
}

$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine' -or $env:USERNAME -ne 'DowlumeTester') {
    throw 'Collect evidence only from the disposable DowlumeTester Hyper-V guest.'
}
$fixtureRoot = Join-Path $env:LOCALAPPDATA 'Dowlume.RecycleAcceptance'
$runs = @(Get-ChildItem -LiteralPath $fixtureRoot -Directory | Where-Object Name -match '^[a-fA-F0-9]{32}$')
if (-not $runs.Count) { throw 'No existing recycle-probe run was found. No test was started.' }
$evidenceDirectory = Join-Path $env:LOCALAPPDATA ('Dowlume.RecycleEvidence\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null
$runEvidence = foreach ($run in $runs) {
    if ($run.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a redirected fixture directory.' }
    $copyDirectory = Join-Path $evidenceDirectory $run.Name
    New-Item -ItemType Directory -Path $copyDirectory | Out-Null
    $reports = foreach ($name in @('SUMMARY.json', 'original-policy.json', 'ordinary.json', 'disabled.json', 'oversized.json', 'sentinel.json', 'near-capacity.json')) {
        $path = Join-Path $run.FullName $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a redirected report.' }
        $hash = (Get-FileHash -LiteralPath $path).Hash
        Copy-Item -LiteralPath $path -Destination $copyDirectory
        if ((Get-FileHash -LiteralPath (Join-Path $copyDirectory $name)).Hash -ne $hash) { throw 'Evidence copy checksum mismatch.' }
        [ordered]@{ Name = $name; Sha256 = $hash; Content = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json) }
    }
    $fixtures = foreach ($file in Get-ChildItem -LiteralPath $run.FullName -Filter 'probe-*.bin' -File) {
        if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a redirected fixture.' }
        [ordered]@{ Name = $file.Name; Bytes = $file.Length; Sha256 = (Get-FileHash -LiteralPath $file.FullName).Hash }
    }
    $transcript = Join-Path $run.FullName 'transcript.txt'
    if (Test-Path -LiteralPath $transcript -PathType Leaf) { Copy-Item -LiteralPath $transcript -Destination $copyDirectory }
    [ordered]@{ Run = $run.Name; Reports = @($reports); RemainingSourceFixtures = @($fixtures) }
}
$shell = New-Object -ComObject Shell.Application
$bin = $shell.Namespace(10)
$binEvidence = foreach ($item in $bin.Items()) {
    $deletedFrom = [string]$item.ExtendedProperty('System.Recycle.DeletedFrom')
    if (-not $deletedFrom.StartsWith($fixtureRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
    [ordered]@{
        Name = $item.Name
        DeletedFrom = $deletedFrom
        ContentPresent = Test-Path -LiteralPath $item.Path -PathType Leaf
        Sha256 = if (Test-Path -LiteralPath $item.Path -PathType Leaf) { (Get-FileHash -LiteralPath $item.Path).Hash } else { $null }
    }
}
$evidence = [ordered]@{
    CollectedAtUtc = [DateTime]::UtcNow.ToString('o')
    Note = 'Existing reports and fixture hashes only. No recycling, restore, deletion, or policy changes were requested.'
    Runs = @($runEvidence)
    RecycleBinItemCount = $bin.Items().Count
    RemainingProbeBinItems = @($binEvidence)
}
if ($IncludeRawBinInventory) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { $accountSid = $identity.User.Value } finally { $identity.Dispose() }
    $volumeRoot = [IO.Path]::GetPathRoot($fixtureRoot)
    $rawBinPath = Join-Path (Join-Path $volumeRoot '$Recycle.Bin') $accountSid
    $evidence.RawBinInventory = Get-RawBinInventory $rawBinPath
    $evidence.RawBinScope = 'Current account on the fixture volume only; direct entries including hidden files. No restoration, recursion, deletion, ownership or ACL changes. Incomplete inventory is not an empty bin.'
}
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidenceDirectory 'EVIDENCE.json') -Encoding UTF8
$evidence | ConvertTo-Json -Depth 12
Write-Host "Evidence copies: $evidenceDirectory"
