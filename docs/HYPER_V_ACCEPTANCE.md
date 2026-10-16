# Prepare a Windows 11 acceptance VM

This is the Hyper-V setup companion to [Windows acceptance](WINDOWS_ACCEPTANCE.md). Preparing a VM does not complete any guest acceptance check. Use only fictional files and keep the GitHub release unpublished until explicitly authorized.

## Create the guest on the host

Run PowerShell from an account authorized to manage Hyper-V. `Get-VM` must succeed; an empty result means there are no VMs. Choose a new directory on a fixed NTFS volume with at least 100 GiB free. The script refuses an existing directory or VM name and retains partial preparation if a command fails.

```powershell
Get-VM
# Choose storage paths suitable for this host; do not reuse a personal folder.
New-Item -ItemType Directory -Path E:\Dowlume.AcceptanceVM\Media
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-WindowsAcceptanceVm.ps1 `
    -Path E:\Dowlume.AcceptanceVM\Windows11Vm
```

The guest has Generation 2 firmware, Microsoft Windows Secure Boot, a virtual TPM, two virtual CPUs, 4–6 GiB dynamic memory, and a 96 GiB dynamic system disk. It uses the existing Default Switch; no host networking or Recycle Bin settings are changed. Automatic start and automatic checkpoints are disabled. The guest stays powered off. Hyper-V Guest Service Interface is enabled for copying the test kit without a network share.

The [Windows 11 VM requirements](https://learn.microsoft.com/en-us/windows/whats-new/windows-11-requirements#virtual-machine-support) still apply. Close unnecessary host applications before starting the guest if memory is limited.

## Attach official installation media

Download **Windows 11 Disk Image (ISO) for x64 devices**, English, from [Microsoft](https://www.microsoft.com/en-us/software-download/windows11). Use the ISO download section. Save it in the dedicated Media directory. If Microsoft blocks an automated download, use the normal browser download rather than unofficial mirrors. Copy the SHA-256 value for the exact language/release from Microsoft's **Verify your download** section.

In host PowerShell, substitute the downloaded filename and published hash:

```powershell
$ErrorActionPreference = 'Stop'
$vm = Get-VM -Name 'Dowlume-Windows11-Acceptance'
$iso = 'E:\Dowlume.AcceptanceVM\Media\<downloaded-file>.iso'
$expectedHash = '<Microsoft SHA-256 for this exact ISO>'
if ($expectedHash -notmatch '^[A-Fa-f0-9]{64}$') { throw 'Enter the official SHA-256 first.' }
$actualHash = (Get-FileHash -LiteralPath $iso -Algorithm SHA256).Hash
if ($actualHash -ne $expectedHash) { throw 'ISO checksum mismatch; do not boot this image.' }
if ($vm.State -ne 'Off') { throw 'Shut down this disposable VM before attaching installation media.' }
Get-VMDvdDrive -VM $vm | Set-VMDvdDrive -Path $iso
$dvd = Get-VMDvdDrive -VM $vm
Set-VMFirmware -VM $vm -FirstBootDevice $dvd
[ordered]@{
    FileName = [IO.Path]::GetFileName($iso)
    Sha256 = $actualHash
    Source = 'https://www.microsoft.com/en-us/software-download/windows11'
    VerifiedAtUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath E:\Dowlume.AcceptanceVM\Windows11Vm\ISO-VERIFICATION.json -Encoding UTF8
# VMConnect is intentionally visible for Windows setup.
Start-Process -FilePath "$env:SystemRoot\System32\vmconnect.exe" -ArgumentList 'localhost','Dowlume-Windows11-Acceptance'
Start-VM -VM $vm
```

Click inside VMConnect and press a key when prompted to boot from DVD. Install Windows onto the guest's blank 96 GiB disk. Complete license, edition, account, and privacy choices yourself. Do not start Windows setup on the host or redirect host drives into the guest. Do not bypass Windows hardware or account requirements.

Finish Windows setup and updates. Create a separate **standard** local test account named `DowlumeTester` through Windows Settings; keep its password private. Sign in to that account for acceptance. Do not install Visual Studio, the .NET SDK, or a shared modern .NET runtime. Windows' built-in .NET Framework is expected and runs Windows PowerShell.

Shut down the guest normally, then create the clean baseline from the host:

```powershell
$vm = Get-VM -Name 'Dowlume-Windows11-Acceptance'
if ($vm.State -ne 'Off') { throw 'Shut down the guest before taking the clean baseline.' }
Get-VMDvdDrive -VM $vm | Set-VMDvdDrive -Path $null
Set-VMFirmware -VM $vm -FirstBootDevice (Get-VMHardDiskDrive -VM $vm)
Checkpoint-VM -VM $vm -SnapshotName 'Clean Windows - before Dowlume acceptance'
Start-VM -VM $vm
```

## Transfer and run the candidate

Stage only the exact release files and the extracted acceptance kit in a dedicated host directory. Include the earlier installer, renamed `Dowlume-previous-setup.exe`, to exercise an actual candidate upgrade. Verify the candidate against its `SHA256SUMS.txt` and record the previous installer's hash separately. Do not copy your application profile or personal files.

With Windows running, copy the staged files from the host. The unique guest directory prevents reuse of an earlier run. This uses [Copy-VMFile](https://learn.microsoft.com/en-us/powershell/module/hyper-v/copy-vmfile); it does not mount a host drive in the guest.

```powershell
$ErrorActionPreference = 'Stop'
$vm = Get-VM -Name 'Dowlume-Windows11-Acceptance'
if ($vm.State -ne 'Running') { throw 'Start Windows and finish setup first.' }
$kit = 'E:\Dowlume.AcceptanceVM\Kit-cf42326'
$guestKit = 'C:\DowlumeAcceptance\' + [Guid]::NewGuid().ToString('N')
Get-ChildItem -LiteralPath $kit -File | ForEach-Object {
    Copy-VMFile -VM $vm -SourcePath $_.FullName `
        -DestinationPath (Join-Path $guestKit $_.Name) -FileSource Host -CreateFullPath
}
Write-Host "Guest kit directory: $guestKit"
```

If the Guest Service Interface is not ready, wait for Windows to finish booting and retry with a new guest directory. Do not disable host security or open a network share as a workaround.

Inside the guest, sign in as `DowlumeTester`, open a normal Windows PowerShell window, change to the printed kit directory, and run:

```powershell
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (@($identity.Groups | Where-Object Value -eq 'S-1-5-32-544').Count) {
    throw 'Use a standard account, not an administrator account or an elevated prompt.'
}
Get-Content .\SHA256SUMS.txt | ForEach-Object {
    if ($_ -notmatch '^([A-Fa-f0-9]{64})  ([A-Za-z0-9_.-]+)$') { throw 'Invalid checksum line.' }
    $expectedHash = $Matches[1]
    $fileName = $Matches[2]
    if ((Get-FileHash -LiteralPath (Join-Path $PWD $fileName) -Algorithm SHA256).Hash -ne $expectedHash) {
        throw "Checksum mismatch: $fileName"
    }
}
Get-FileHash .\Dowlume-previous-setup.exe -Algorithm SHA256
# Compare the previous installer hash with the host's record before continuing.
$report = Join-Path $env:LOCALAPPDATA ('Dowlume.Acceptance\' + [Guid]::NewGuid().ToString('N') + '\package-result.json')
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-WindowsPackage.ps1 `
    -Installer .\Dowlume-0.1.0-win-x64-setup.exe `
    -Archive .\Dowlume-0.1.0-win-x64.zip `
    -PreviousInstaller .\Dowlume-previous-setup.exe `
    -ReportPath $report -RequireNoSharedDotNet
```

Keep the guest desktop unlocked while the script runs. UI Automation and native tray tests require an interactive desktop; PowerShell Direct alone does not provide one. Keep reports and fixture directories when a test fails. Record the guest's Windows build, image provenance, artifact hashes, and observed results. Complete the manual first-use, sign-in, Recycle Bin policy, and Windows restore checks in [Windows acceptance](WINDOWS_ACCEPTANCE.md), taking a guest checkpoint before changing guest Recycle Bin settings.

## Prepared host state — 2026-10-16

`Get-VM` succeeded in the development session and initially returned no VMs. `Dowlume-Windows11-Acceptance` was created with the configuration above under `E:\Dowlume.AcceptanceVM\Windows11Vm`. Secure Boot and virtual TPM were queried and confirmed enabled. The guest remains powered off with no OS installed. Microsoft's ISO link-generation request was blocked; browser download and Windows setup remain pending.

The staged candidate is from clean source `cf42326f4560515b1574ebe7794ef645a551bc21`. Its installer SHA-256 is `73C465CBFC4A47562D4859B9EF9AE0338EF66C9D836C5B07599E4CD37C93F507`; the earlier candidate installer SHA-256 is `859B87DEC3096A263F35C05697A2871EECF471A9C9185912A9751D9D3E04F5B0`. The local preparation manifest records the VM identity. VM creation is infrastructure evidence only: clean-machine smoke, manual acceptance, disabled/full-bin behavior, and Windows restore are still pending. No release was published.
