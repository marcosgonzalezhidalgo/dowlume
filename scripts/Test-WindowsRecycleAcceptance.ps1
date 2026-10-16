param(
    [string]$WorkerDirectory = (Join-Path $PSScriptRoot 'recycle-worker'),
    [string]$WorkerArchive = '',
    [string]$WorkerArchiveSha256 = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$computer = Get-CimInstance Win32_ComputerSystem
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine' -or
    $env:USERNAME -ne 'DowlumeTester' -or @($identity.Groups | Where-Object Value -eq 'S-1-5-32-544').Count) {
    throw 'Run only as the standard DowlumeTester account inside the disposable Hyper-V guest.'
}
$sessionId = (Get-Process -Id $PID).SessionId
if ($sessionId -eq 0 -or -not @(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $sessionId).Count) {
    throw 'Use the signed-in guest desktop.'
}
if ($WorkerArchive) {
    if ($WorkerArchiveSha256 -notmatch '^[A-Fa-f0-9]{64}$' -or
        (Get-FileHash -LiteralPath $WorkerArchive).Hash -ne $WorkerArchiveSha256) {
        throw 'Worker archive checksum mismatch.'
    }
    $WorkerDirectory = Join-Path $env:LOCALAPPDATA ('Dowlume.AcceptanceWorker\' + [Guid]::NewGuid().ToString('N'))
    Expand-Archive -LiteralPath $WorkerArchive -DestinationPath $WorkerDirectory
}
$worker = Join-Path $WorkerDirectory 'FileOperationAcceptance.exe'
$assemblyHashes = Get-Content -LiteralPath (Join-Path $WorkerDirectory 'CANDIDATE-ASSEMBLIES.json') -Raw | ConvertFrom-Json
foreach ($fileName in @('Dowlume.Core.dll', 'Dowlume.Infrastructure.dll', 'Dowlume.Monitoring.dll')) {
    $entries = @($assemblyHashes | Where-Object FileName -eq $fileName)
    if ($entries.Count -ne 1 -or $entries[0].Sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or
        (Get-FileHash -LiteralPath (Join-Path $WorkerDirectory $fileName)).Hash -ne $entries[0].Sha256) {
        throw 'The probe must load the exact reviewed candidate assemblies.'
    }
}
$shell = New-Object -ComObject Shell.Application
$bin = $shell.Namespace(10)
if ($null -eq $bin -or $bin.Items().Count -ne 0) {
    throw 'Use a fresh test account with an empty Recycle Bin. Existing contents will not be emptied.'
}

$runDirectory = Join-Path $env:LOCALAPPDATA ('Dowlume.RecycleAcceptance\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$driveLetter = [IO.Path]::GetPathRoot($runDirectory).TrimEnd('\')
$volume = Get-CimInstance Win32_Volume -Filter "DriveLetter = '$driveLetter'"
if ($null -eq $volume -or $volume.FileSystem -ne 'NTFS' -or $volume.DeviceID -notmatch '(\{[A-Fa-f0-9-]{36}\})') {
    throw 'Cannot identify the guest NTFS test volume.'
}
$policyPath = 'Software\Microsoft\Windows\CurrentVersion\Explorer\BitBucket\Volume\' + $Matches[1]
$policy = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($policyPath)
$originalPolicy = @{}
foreach ($name in @('NukeOnDelete', 'MaxCapacity')) {
    $exists = $policy.GetValueNames() -contains $name
    $originalPolicy[$name] = @{ Exists = $exists; Value = if ($exists) { $policy.GetValue($name) } else { $null }; Kind = if ($exists) { $policy.GetValueKind($name).ToString() } else { $null } }
}
$originalPolicy | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $runDirectory 'original-policy.json') -Encoding UTF8
$results = [Collections.Generic.List[object]]::new()
$policyRestored = $false
$failure = $null

function Set-TestBinPolicy([int]$Disabled, [int]$CapacityMiB) {
    $policy.SetValue('NukeOnDelete', $Disabled, [Microsoft.Win32.RegistryValueKind]::DWord)
    $policy.SetValue('MaxCapacity', $CapacityMiB, [Microsoft.Win32.RegistryValueKind]::DWord)
    $policy.Flush()
}

function Invoke-RecycleProbe([string]$Name, [int]$SizeKiB) {
    $path = Join-Path $runDirectory ('probe-' + $Name + '.bin')
    $reportPath = Join-Path $runDirectory ($Name + '.json')
    $bytes = New-Object byte[] ($SizeKiB * 1024)
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($bytes) } finally { $random.Dispose() }
    $stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    $expectedHash = (Get-FileHash -LiteralPath $path).Hash
    & $worker probe-recycle $path $expectedHash $reportPath
    if ($LASTEXITCODE -ne 0) { throw "Probe execution failed: $Name" }
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    foreach ($mapping in @(@('InfrastructureSha256', 'Dowlume.Infrastructure.dll'), @('CoreSha256', 'Dowlume.Core.dll'))) {
        $expectedAssembly = @($assemblyHashes | Where-Object FileName -eq $mapping[1])
        if ($expectedAssembly.Count -ne 1 -or $report.($mapping[0]) -ne $expectedAssembly[0].Sha256) {
            throw 'The running probe did not load the candidate assemblies.'
        }
    }
    if ($report.ErrorType -eq 'System.TimeoutException') { throw 'Native recycling timed out; its outcome is inconclusive. Preserve the guest for inspection.' }
    return [pscustomobject]@{ Name = $Name; Path = $path; Hash = $expectedHash; Report = $report }
}

function Find-RecycledProbe($Probe) {
    foreach ($item in $bin.Items()) {
        if ((Test-Path -LiteralPath $item.Path -PathType Leaf) -and (Get-FileHash -LiteralPath $item.Path).Hash -eq $Probe.Hash) {
            return $item
        }
    }
    return $null
}

function Restore-RecycledProbe($Probe) {
    $item = Find-RecycledProbe $Probe
    if ($null -eq $item) { return $false }
    if (Test-Path -LiteralPath $Probe.Path) { throw 'The original path is occupied; Windows restore was not requested.' }
    $item.InvokeVerb('undelete')
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while (-not (Test-Path -LiteralPath $Probe.Path) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    return (Test-Path -LiteralPath $Probe.Path) -and (Get-FileHash -LiteralPath $Probe.Path).Hash -eq $Probe.Hash
}

Start-Transcript -LiteralPath (Join-Path $runDirectory 'transcript.txt') | Out-Null
try {
    Set-TestBinPolicy 0 16
    $ordinary = Invoke-RecycleProbe 'ordinary' 64
    $restored = Restore-RecycledProbe $ordinary
    $results.Add([ordered]@{ Case = 'Ordinary recycling and Windows shell restore'; Passed = $ordinary.Report.AdapterReturnedSuccess -and -not $ordinary.Report.SourceExists -and $restored; RestoredHashMatches = $restored })
    if (-not $restored) { throw 'Ordinary shell restore failed; stop before testing altered bin policies.' }

    Set-TestBinPolicy 1 16
    $disabled = Invoke-RecycleProbe 'disabled' 64
    $disabledPreserved = -not $disabled.Report.AdapterReturnedSuccess -and $disabled.Report.SourceExists -and $disabled.Report.SourceHash -eq $disabled.Hash
    $results.Add([ordered]@{ Case = 'Disabled Recycle Bin'; Passed = $disabledPreserved; SourcePreserved = $disabledPreserved })
    if (-not $disabled.Report.SourceExists) { $null = Restore-RecycledProbe $disabled }

    Set-TestBinPolicy 0 1
    $oversized = Invoke-RecycleProbe 'oversized' 2048
    $oversizedPreserved = -not $oversized.Report.AdapterReturnedSuccess -and $oversized.Report.SourceExists -and $oversized.Report.SourceHash -eq $oversized.Hash
    $results.Add([ordered]@{ Case = 'File exceeds bin capacity'; Passed = $oversizedPreserved; SourcePreserved = $oversizedPreserved })
    if (-not $oversized.Report.SourceExists) { $null = Restore-RecycledProbe $oversized }
    if ($bin.Items().Count -ne 0) { throw 'Earlier probes remain in the bin; retain them and stop before the near-capacity case.' }

    $sentinel = Invoke-RecycleProbe 'sentinel' 768
    if (-not $sentinel.Report.AdapterReturnedSuccess -or $null -eq (Find-RecycledProbe $sentinel)) { throw 'Cannot establish the near-capacity sentinel.' }
    $nearCapacity = Invoke-RecycleProbe 'near-capacity' 768
    $sentinelRetained = $null -ne (Find-RecycledProbe $sentinel)
    $sourcePreserved = $nearCapacity.Report.SourceExists -and $nearCapacity.Report.SourceHash -eq $nearCapacity.Hash
    $newItemRecycled = $nearCapacity.Report.AdapterReturnedSuccess -and $null -ne (Find-RecycledProbe $nearCapacity)
    $results.Add([ordered]@{ Case = 'Bin near capacity'; Passed = $sentinelRetained -and ($sourcePreserved -or $newItemRecycled); ExistingBinItemRetained = $sentinelRetained; SourcePreserved = $sourcePreserved; NewItemRecycled = $newItemRecycled })
    if ($sentinelRetained) { $null = Restore-RecycledProbe $sentinel }
    if ($newItemRecycled) { $null = Restore-RecycledProbe $nearCapacity }
}
catch {
    $failure = $_.Exception.Message
    Write-Warning $failure
}
finally {
    try {
        foreach ($name in @('NukeOnDelete', 'MaxCapacity')) {
            $original = $originalPolicy[$name]
            if ($original.Exists) { $policy.SetValue($name, $original.Value, [Microsoft.Win32.RegistryValueKind]$original.Kind) }
            else { $policy.DeleteValue($name, $false) }
        }
        $policy.Flush()
        $policyRestored = $true
    }
    catch {
        $failure = 'Restoring the original guest bin policy failed: ' + $_.Exception.Message
        Write-Warning $failure
    }
    finally { $policy.Dispose() }
    $summary = [ordered]@{
        RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
        Passed = $null -eq $failure -and $results.Count -eq 4 -and @($results | Where-Object { -not $_.Passed }).Count -eq 0 -and $policyRestored
        GuestPolicyRestored = $policyRestored
        PolicyPath = $policyPath
        Cases = $results.ToArray()
        Error = $failure
        Note = 'Automated native adapter and shell checks; does not replace manual UI or independent first-use acceptance.'
    }
    $summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runDirectory 'SUMMARY.json') -Encoding UTF8
    Stop-Transcript | Out-Null
}
$summary | ConvertTo-Json -Depth 5
Write-Host "Reports and fictional fixtures retained at: $runDirectory"
if (-not $summary.Passed) { throw 'Recycle Bin acceptance did not pass. Preserve the evidence and inspect failures before repeating.' }
