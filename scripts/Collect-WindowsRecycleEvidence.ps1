$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $evidenceDirectory 'EVIDENCE.json') -Encoding UTF8
$evidence | ConvertTo-Json -Depth 12
Write-Host "Evidence copies: $evidenceDirectory"
