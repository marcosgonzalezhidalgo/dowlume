param(
    [Parameter(Mandatory = $true)][string]$Installer,
    [string]$Archive = '',
    [string]$PreviousInstaller = '',
    [Parameter(Mandatory = $true)][string]$ReportPath,
    [switch]$RequireNoSharedDotNet
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Installer = (Resolve-Path -LiteralPath $Installer).Path
if ($Archive) { $Archive = (Resolve-Path -LiteralPath $Archive).Path }
if ($PreviousInstaller) { $PreviousInstaller = (Resolve-Path -LiteralPath $PreviousInstaller).Path }
$ReportPath = [IO.Path]::GetFullPath($ReportPath)
$sharedRuntimeFolders = @(
    (Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'dotnet/shared'),
    (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'dotnet/shared'),
    (Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Microsoft/dotnet/shared')
) | Where-Object { Test-Path -LiteralPath $_ }
$dotnetRuntimes = @()
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $dotnetRuntimes = @(dotnet --list-runtimes) }
if ($RequireNoSharedDotNet -and (@($sharedRuntimeFolders).Count -gt 0 -or $dotnetRuntimes.Count -gt 0)) {
    throw 'Shared .NET runtimes were detected. Run clean-machine acceptance on a fresh Windows image without .NET installed.'
}
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{A65DE461-2F0E-47BC-8352-3F980674F3DB}_is1'
if (Test-Path -LiteralPath $uninstallKey) {
    throw 'An existing Dowlume installation is registered. Use a disposable Windows account or VM; this test will not replace it.'
}

$runId = [Guid]::NewGuid().ToString('N')
$workspace = Join-Path ([IO.Path]::GetTempPath()) "Dowlume.PackageAcceptance\$runId"
$installDirectory = Join-Path $workspace 'App'
$profile = Join-Path $workspace 'Profile'
$downloads = Join-Path $workspace 'Downloads'
New-Item -ItemType Directory -Path $profile,$downloads | Out-Null
$sentinel = Join-Path $downloads 'sample-report.txt'
'Disposable user content that installation must preserve.' | Set-Content -LiteralPath $sentinel
$sentinelHash = (Get-FileHash -LiteralPath $sentinel).Hash
$executable = Join-Path $installDirectory 'Dowlume.App.exe'
$database = Join-Path $profile 'dowlume.db'
$uninstaller = Join-Path $installDirectory 'unins000.exe'
$application = $null

function Invoke-Setup([string]$Path, [string[]]$SetupArguments) {
    $process = Start-Process -FilePath $Path -ArgumentList $SetupArguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(120000)) {
        throw "Setup timed out. Inspect the disposable workspace: $workspace"
    }
    $process.Refresh()
    return $process.ExitCode
}

function Install-Package([string]$Path, [string]$LogName) {
    $setupArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/NOFORCECLOSEAPPLICATIONS',
        "/DIR=""$installDirectory""", "/GROUP=""Dowlume acceptance $runId""", "/LOG=""$(Join-Path $workspace $LogName)""")
    $exitCode = Invoke-Setup $Path $setupArguments
    if ($exitCode -ne 0) { throw "Installation failed with exit code $exitCode." }
}

function Start-Application([string]$ExpectedTitle) {
    $process = Start-Process -FilePath $executable -ArgumentList @('--data-directory', """$profile""") -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.HasExited) { throw 'The packaged application exited before displaying a window.' }
    } while ($process.MainWindowHandle -eq 0 -and [DateTime]::UtcNow -lt $deadline)
    if ($process.MainWindowHandle -eq 0 -or $process.MainWindowTitle -notlike $ExpectedTitle) {
        throw "The packaged application did not show the expected window: $ExpectedTitle"
    }
    return $process
}

function Close-Application([Diagnostics.Process]$Process) {
    if (-not $Process.CloseMainWindow() -or -not $Process.WaitForExit(30000)) {
        throw 'The packaged application did not shut down gracefully.'
    }
    if ($Process.ExitCode -ne 0) { throw "The packaged application exited with code $($Process.ExitCode)." }
}

try {
    if ($Archive) {
        $portableDirectory = Join-Path $workspace 'Portable'
        Expand-Archive -LiteralPath $Archive -DestinationPath $portableDirectory
        $executable = Join-Path $portableDirectory 'Dowlume.App.exe'
        $application = Start-Application 'Dowlume'
        Close-Application $application
        $application = $null
        $executable = Join-Path $installDirectory 'Dowlume.App.exe'
    }
    $initialInstaller = if ($PreviousInstaller) { $PreviousInstaller } else { $Installer }
    Install-Package $initialInstaller 'initial-install.log'
    $application = Start-Application 'Dowlume'
    Close-Application $application
    $application = $null
    $databaseHash = (Get-FileHash -LiteralPath $database).Hash

    Install-Package $Installer 'upgrade.log'
    if ((Get-FileHash -LiteralPath $database).Hash -ne $databaseHash) { throw 'Upgrade changed the local database.' }
    if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'START-HERE.txt'))) { throw 'The offline quick-start guide is missing.' }
    foreach ($runtimeFile in @('coreclr.dll', 'hostfxr.dll', 'e_sqlite3.dll', 'libSkiaSharp.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDirectory $runtimeFile))) { throw "Missing packaged runtime dependency: $runtimeFile" }
    }

    $application = Start-Application 'Dowlume'
    $secondInstance = Start-Application 'Dowlume*unable to start'
    Close-Application $secondInstance
    $beforeUpgrade = (Get-FileHash -LiteralPath $executable).Hash
    $blockedInstall = Invoke-Setup $Installer @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=""$installDirectory""", "/LOG=""$(Join-Path $workspace 'blocked-upgrade.log')""")
    if ($blockedInstall -eq 0 -or $application.HasExited) { throw 'Setup did not refuse an upgrade while the application was running.' }
    if ((Get-FileHash -LiteralPath $executable).Hash -ne $beforeUpgrade) { throw 'Setup changed the running executable.' }
    $blockedUninstall = Invoke-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=""$(Join-Path $workspace 'blocked-uninstall.log')""")
    if ($blockedUninstall -eq 0 -or -not (Test-Path -LiteralPath $executable)) { throw 'Uninstall did not refuse while the application was running.' }
    Close-Application $application
    $application = $null
    $databaseHash = (Get-FileHash -LiteralPath $database).Hash

    $uninstallExit = Invoke-Setup $uninstaller @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/LOG=""$(Join-Path $workspace 'uninstall.log')""")
    if ($uninstallExit -ne 0 -or (Test-Path -LiteralPath $executable)) { throw 'Uninstall did not remove the application.' }
    if ((Get-FileHash -LiteralPath $database).Hash -ne $databaseHash -or (Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash) {
        throw 'Uninstall changed the profile or sample user file.'
    }
    if (Test-Path -LiteralPath $uninstallKey) { throw 'Uninstall left its registration behind.' }

    $report = [ordered]@{
        Passed = $true
        RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
        WindowsVersion = [Environment]::OSVersion.VersionString
        InstallerSha256 = (Get-FileHash -LiteralPath $Installer).Hash
        PortableArchiveSha256 = if ($Archive) { (Get-FileHash -LiteralPath $Archive).Hash } else { $null }
        PortableLaunchAndExit = [bool]$Archive
        PreviousInstallerSha256 = if ($PreviousInstaller) { (Get-FileHash -LiteralPath $PreviousInstaller).Hash } else { $null }
        InstallerUpgrade = if ($PreviousInstaller) { 'Previous package to candidate' } else { 'Candidate reinstall' }
        SingleInstanceRefusal = $true
        RunningUpgradeRefused = $true
        RunningUninstallRefused = $true
        GracefulExit = $true
        ProfileAndUserFilePreserved = $true
        SharedRuntimes = $dotnetRuntimes
        SharedRuntimeFolders = @($sharedRuntimeFolders)
        RequiredNoSharedDotNet = [bool]$RequireNoSharedDotNet
        Workspace = $workspace
        Note = 'This automated smoke test does not certify a clean machine or replace first-use testing by a person.'
    }
    New-Item -ItemType Directory -Path (Split-Path $ReportPath -Parent) -Force | Out-Null
    $report | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding utf8
    Write-Output "Package acceptance passed. Report: $ReportPath"
} finally {
    if ($null -ne $application -and -not $application.HasExited) {
        $null = $application.CloseMainWindow()
        $null = $application.WaitForExit(30000)
    }
    Write-Output "Disposable acceptance files retained at: $workspace"
}
