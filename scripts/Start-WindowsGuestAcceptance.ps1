param([string]$KitDirectory = $PSScriptRoot)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine') {
    throw 'Run this launcher only inside the disposable Hyper-V guest.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if ($env:USERNAME -ne 'DowlumeTester' -or @($identity.Groups | Where-Object Value -eq 'S-1-5-32-544').Count) {
    throw 'Sign in as the standard DowlumeTester account. Do not run as administrator.'
}
$sessionId = (Get-Process -Id $PID).SessionId
$desktop = @(Get-Process -Name explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $sessionId)
if ($sessionId -eq 0 -or -not [Environment]::UserInteractive -or -not $desktop.Count) {
    throw 'Run from the signed-in Windows desktop and keep it unlocked during the test.'
}

$KitDirectory = (Resolve-Path -LiteralPath $KitDirectory).Path
$manifest = Get-Content -LiteralPath (Join-Path $KitDirectory 'KIT-VERIFICATION.json') -Raw | ConvertFrom-Json
$requiredFiles = @('BUILD-INFO.json', 'Dowlume-0.1.0-win-x64-setup.exe', 'Dowlume-0.1.0-win-x64.zip',
    'Dowlume-previous-setup.exe', 'Test-WindowsPackage.ps1', 'Test-WindowsTray.ps1')
foreach ($fileName in $requiredFiles) {
    $entries = @($manifest | Where-Object FileName -eq $fileName)
    if ($entries.Count -ne 1 -or $entries[0].Sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        throw "Missing or ambiguous verification entry: $fileName"
    }
    $filePath = Join-Path $KitDirectory $fileName
    if ((Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash -ne $entries[0].Sha256) {
        throw "Checksum mismatch: $fileName"
    }
}
$build = Get-Content -LiteralPath (Join-Path $KitDirectory 'BUILD-INFO.json') -Raw | ConvertFrom-Json
if ($build.DirtyWorkingTree -ne $false -or $build.RuntimeIdentifier -ne 'win-x64' -or $build.SelfContained -ne $true) {
    throw 'The candidate must be a clean, self-contained Windows x64 build.'
}

$runDirectory = Join-Path $env:LOCALAPPDATA ('Dowlume.Acceptance\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runDirectory | Out-Null
$reportPath = Join-Path $runDirectory 'package-result.json'
$summaryPath = Join-Path $runDirectory 'SUMMARY.json'
$os = Get-CimInstance Win32_OperatingSystem
$summary = [ordered]@{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
    Passed = $false
    WindowsCaption = $os.Caption
    WindowsVersion = $os.Version
    WindowsBuild = $os.BuildNumber
    StandardTestAccount = $true
    InteractiveDesktop = $true
    SourceCommit = $build.SourceCommit
    InstallerSha256 = (Get-FileHash -LiteralPath (Join-Path $KitDirectory 'Dowlume-0.1.0-win-x64-setup.exe')).Hash
    Error = $null
}
Start-Transcript -LiteralPath (Join-Path $runDirectory 'transcript.txt') | Out-Null
try {
    & (Join-Path $KitDirectory 'Test-WindowsPackage.ps1') `
        -Installer (Join-Path $KitDirectory 'Dowlume-0.1.0-win-x64-setup.exe') `
        -Archive (Join-Path $KitDirectory 'Dowlume-0.1.0-win-x64.zip') `
        -PreviousInstaller (Join-Path $KitDirectory 'Dowlume-previous-setup.exe') `
        -ReportPath $reportPath -RequireNoSharedDotNet
    $packageResult = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if ($packageResult.Passed -ne $true) { throw 'The package test did not report success.' }
    $summary.Passed = $true
}
catch {
    $summary.Error = $_.Exception.Message
    Write-Warning $summary.Error
}
finally {
    $summary | ConvertTo-Json | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    Stop-Transcript | Out-Null
}
Get-Content -LiteralPath $summaryPath
Write-Host "Reports and logs: $runDirectory"
Write-Host 'Keep the reports and disposable fixtures. Manual first-use and Recycle Bin policy acceptance remain separate.'
if (-not $summary.Passed) { throw "Acceptance failed. Inspect $summaryPath before rerunning." }
