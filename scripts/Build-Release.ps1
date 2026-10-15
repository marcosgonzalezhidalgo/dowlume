param(
    [string]$InnoCompiler = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repositoryRoot 'Dowlume/Dowlume.slnx'
$artifactDirectory = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactDirectory 'Dowlume-0.1.0-win-x64'

if (-not $SkipTests) {
    dotnet test $solution -c Release --filter 'Category!=WindowsShell'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. No release was packaged.' }
}

dotnet publish (Join-Path $repositoryRoot 'Dowlume/Dowlume.App/Dowlume.App.csproj') `
    -c Release -r win-x64 --self-contained true -o $publishDirectory `
    -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/USER_GUIDE.md') -Destination $publishDirectory
$archive = Join-Path $artifactDirectory 'Dowlume-0.1.0-win-x64.zip'
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archive -Force

if (-not $InnoCompiler) {
    $installedCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    if (Test-Path -LiteralPath $installedCompiler) { $InnoCompiler = $installedCompiler }
}

if ($InnoCompiler) {
    & $InnoCompiler /Q "/DPublishDirectory=$publishDirectory" "/DOutputDirectory=$artifactDirectory" `
        (Join-Path $repositoryRoot 'packaging/dowlume.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
} else {
    Write-Warning 'Portable ZIP created. Pass -InnoCompiler to also build the Windows installer.'
}

Get-ChildItem -LiteralPath $artifactDirectory -File |
    Where-Object { $_.Extension -in '.zip', '.exe' } |
    ForEach-Object { $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256; "$($hash.Hash)  $($_.Name)" } |
    Set-Content -LiteralPath (Join-Path $artifactDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release files: $artifactDirectory"
