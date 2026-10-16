param(
    [string]$InnoCompiler = '',
    [string]$OutputDirectory = '',
    [switch]$RequireInstaller,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repositoryRoot 'Dowlume/Dowlume.slnx'
$project = Join-Path $repositoryRoot 'Dowlume/Dowlume.App/Dowlume.App.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
$sourceCommit = git -C $repositoryRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }
$workingChanges = @(git -C $repositoryRoot status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the working tree.' }

if (-not $InnoCompiler) {
    $installedCompiler = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6/ISCC.exe'
    if (Test-Path -LiteralPath $installedCompiler) { $InnoCompiler = $installedCompiler }
}
if ($InnoCompiler -and -not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) {
    throw 'The Inno Setup compiler was not found.'
}
if ($RequireInstaller -and -not $InnoCompiler) { throw 'Release packaging requires an Inno Setup compiler.' }

if (-not $OutputDirectory) {
    $buildName = '{0}-{1}-{2}' -f $sourceCommit.Substring(0, 8), (Get-Date -Format 'yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $OutputDirectory = Join-Path $repositoryRoot "artifacts/releases/$buildName"
}
$artifactDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $artifactDirectory) { throw 'Choose a new output directory. Existing artifacts are never reused or deleted.' }
New-Item -ItemType Directory -Path $artifactDirectory | Out-Null
$packageName = "Dowlume-$version-win-x64"
$publishDirectory = Join-Path $artifactDirectory $packageName

if (-not $SkipTests) {
    dotnet test $solution -c Release --filter 'Category!=WindowsShell'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed. No release was packaged.' }
}

dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDirectory -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }

# Native NuGet runtime assets can include PDBs despite DebugSymbols=false.
Get-ChildItem -LiteralPath $publishDirectory -Filter '*.pdb' -File -Recurse |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $publishDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'packaging/START-HERE.txt') -Destination $publishDirectory
$buildInfo = [ordered]@{
    Version = $version
    SourceCommit = $sourceCommit
    DirtyWorkingTree = $workingChanges.Count -gt 0
    RuntimeIdentifier = 'win-x64'
    SelfContained = $true
    BuiltAtUtc = [DateTime]::UtcNow.ToString('o')
    Signing = 'Unsigned'
}
$buildInfoPath = Join-Path $artifactDirectory 'BUILD-INFO.json'
$buildInfo | ConvertTo-Json | Set-Content -LiteralPath $buildInfoPath -Encoding utf8
Copy-Item -LiteralPath $buildInfoPath -Destination $publishDirectory
$archive = Join-Path $artifactDirectory "$packageName.zip"
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $archive
$releaseFiles = @($archive, $buildInfoPath)
$acceptanceDirectory = Join-Path $artifactDirectory 'acceptance-kit'
New-Item -ItemType Directory -Path $acceptanceDirectory | Out-Null
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts/Test-WindowsPackage.ps1') -Destination $acceptanceDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts/Test-WindowsTray.ps1') -Destination $acceptanceDirectory
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/WINDOWS_ACCEPTANCE.md') -Destination $acceptanceDirectory
$acceptanceArchive = Join-Path $artifactDirectory "Dowlume-$version-windows-acceptance.zip"
Compress-Archive -Path (Join-Path $acceptanceDirectory '*') -DestinationPath $acceptanceArchive
$releaseFiles += $acceptanceArchive

if ($InnoCompiler) {
    & $InnoCompiler /Q "/DApplicationVersion=$version" "/DPublishDirectory=$publishDirectory" "/DOutputDirectory=$artifactDirectory" (Join-Path $repositoryRoot 'packaging/dowlume.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $releaseFiles += Join-Path $artifactDirectory "$packageName-setup.exe"
} else {
    Write-Warning 'Portable ZIP created. Pass -InnoCompiler to also build the Windows installer.'
}

$releaseFiles | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash)  $([IO.Path]::GetFileName($_))"
} | Set-Content -LiteralPath (Join-Path $artifactDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Host "Release files: $artifactDirectory"
