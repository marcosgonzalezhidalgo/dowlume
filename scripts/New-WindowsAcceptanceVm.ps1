#Requires -Modules Hyper-V
param(
    [Parameter(Mandatory)]
    [string]$Path,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{0,63}$')]
    [string]$Name = 'Dowlume-Windows11-Acceptance',
    [string]$SwitchName = 'Default Switch'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($Path -notmatch '^[A-Za-z]:\\') { throw 'Use an absolute path on a local fixed disk.' }
$vmDirectory = [IO.Path]::GetFullPath($Path)
if (Test-Path -LiteralPath $vmDirectory) { throw 'Choose a new VM directory. Existing files are never reused or deleted.' }
if (@(Get-VM | Where-Object Name -eq $Name).Count) { throw "A VM named '$Name' already exists." }
$parentDirectory = Get-Item -LiteralPath (Split-Path $vmDirectory -Parent)
if (-not $parentDirectory.PSIsContainer) { throw 'The parent must be an existing directory.' }
for ($ancestor = $parentDirectory; $null -ne $ancestor; $ancestor = $ancestor.Parent) {
    if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'VM storage must not traverse a junction or symbolic link.' }
}
$volume = Get-Volume -DriveLetter $vmDirectory.Substring(0, 1)
if ($volume.DriveType -ne 'Fixed' -or $volume.FileSystem -ne 'NTFS' -or $volume.SizeRemaining -lt 100GB) {
    throw 'Choose a fixed NTFS volume with at least 100 GiB free for the disk and checkpoints.'
}
$networkSwitch = Get-VMSwitch | Where-Object Name -eq $SwitchName
if (-not $networkSwitch) { throw "Virtual switch '$SwitchName' does not exist. No host network changes were made." }

New-Item -ItemType Directory -Path $vmDirectory | Out-Null
$diskPath = Join-Path $vmDirectory 'Windows11.vhdx'
try {
    $vm = New-VM -Name $Name -Generation 2 -Path $vmDirectory -MemoryStartupBytes 4GB `
        -NewVHDPath $diskPath -NewVHDSizeBytes 96GB -SwitchName $networkSwitch.Name
    Set-VM -VM $vm -ProcessorCount 2 -AutomaticStartAction Nothing -AutomaticStopAction ShutDown `
        -AutomaticCheckpointsEnabled $false -CheckpointType Standard `
        -Notes 'Disposable Dowlume acceptance VM. Use fictional data only. Do not publish the release from this guest.'
    Set-VMMemory -VM $vm -DynamicMemoryEnabled $true -MinimumBytes 4GB -StartupBytes 4GB -MaximumBytes 6GB
    Set-VMFirmware -VM $vm -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
    Set-VMKeyProtector -VM $vm -NewLocalKeyProtector
    Enable-VMTPM -VM $vm
    Add-VMDvdDrive -VM $vm | Out-Null

    # The service ID is stable across localized host installations.
    $guestService = Get-VMIntegrationService -VM $vm | Where-Object Id -like '*\6C09BB55-D683-4DA0-8931-C9BF705F6480'
    if (-not $guestService) { throw 'The Hyper-V Guest Service Interface was not found.' }
    Enable-VMIntegrationService -VMIntegrationService $guestService

    [ordered]@{
        CreatedAtUtc = [DateTime]::UtcNow.ToString('o')
        VmName = $vm.Name
        VmId = $vm.Id.ToString()
        Directory = $vmDirectory
        DiskPath = $diskPath
        Generation = 2
        ProcessorCount = 2
        MemoryMinimumGiB = 4
        MemoryMaximumGiB = 6
        DiskMaximumGiB = 96
        SwitchName = $networkSwitch.Name
        SecureBoot = 'MicrosoftWindows'
        VirtualTpm = $true
        Status = 'Awaiting verified Windows 11 x64 installation media; no guest tests run.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $vmDirectory 'VM-PREPARATION.json') -Encoding UTF8
    Get-VM -Id $vm.Id | Select-Object Name, Id, State, Generation, Path
}
catch {
    Write-Warning "Preparation did not complete. Any VM and files created at '$vmDirectory' have been retained for inspection."
    throw
}
