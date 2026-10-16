param([string]$OutputDirectory = '')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot ('artifacts/security/' + [Guid]::NewGuid().ToString('N'))
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Choose a new security report directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$solution = Join-Path $repositoryRoot 'Dowlume/Dowlume.slnx'

dotnet restore $solution --force-evaluate
if ($LASTEXITCODE -ne 0) { throw 'Dependency restore/audit failed.' }
$auditPath = Join-Path $output 'dependencies.json'
dotnet list $solution package --vulnerable --include-transitive --format json --output-version 1 |
    Out-File -LiteralPath $auditPath -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Dependency scan failed.' }
$audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
if (@($audit.projects).Count -lt 5) { throw 'Dependency scan did not cover all application projects.' }
if ($audit.PSObject.Properties.Name -contains 'problems' -and @($audit.problems).Count -gt 0) {
    throw 'Dependency scan reported incomplete results; inspect dependencies.json.'
}
foreach ($project in $audit.projects) {
    if ($project.PSObject.Properties.Name -contains 'frameworks') {
        foreach ($framework in $project.frameworks) {
            foreach ($group in @('topLevelPackages', 'transitivePackages')) {
                if ($framework.PSObject.Properties.Name -contains $group -and @($framework.$group).Count -gt 0) {
                    throw 'Vulnerable dependencies were found; inspect dependencies.json.'
                }
            }
        }
    }
}

dotnet build $solution -c Release --no-restore --no-incremental "-p:SecurityScanDirectory=$output"
if ($LASTEXITCODE -ne 0) { throw 'Code analysis failed; inspect the SARIF reports.' }
foreach ($helper in @('FileOperationAcceptance', 'DocumentationScreenshots')) {
    dotnet build (Join-Path $repositoryRoot "scripts/$helper") -c Release "-p:SecurityScanDirectory=$output"
    if ($LASTEXITCODE -ne 0) { throw "Code analysis failed for $helper." }
}

$version = '8.30.1'
$archive = Join-Path $output 'gitleaks.zip'
Invoke-WebRequest -UseBasicParsing -Uri "https://github.com/gitleaks/gitleaks/releases/download/v$version/gitleaks_$($version)_windows_x64.zip" -OutFile $archive
$expectedHash = 'D29144DEFF3A68AA93CED33DDDF84B7FDC26070ADD4AA0F4513094C8332AFC4E'
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Gitleaks download failed its pinned SHA-256 check.'
}
$toolDirectory = Join-Path $output 'tools'
Expand-Archive -LiteralPath $archive -DestinationPath $toolDirectory
$gitleaks = Join-Path $toolDirectory 'gitleaks.exe'
& $gitleaks git $repositoryRoot --log-opts=--all --redact --no-banner --report-format json --report-path (Join-Path $output 'secrets-history.json')
if ($LASTEXITCODE -ne 0) { throw 'Git history secret scan failed; inspect the redacted report.' }

# Include pending source changes without scanning build outputs, private profiles, or downloaded tools.
$snapshot = Join-Path $output 'source-snapshot'
$files = @(git -C $repositoryRoot -c core.quotePath=false ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files.' }
foreach ($relativePath in $files) {
    $source = Join-Path $repositoryRoot $relativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { continue }
    $target = [IO.Path]::GetFullPath((Join-Path $snapshot $relativePath))
    if (-not $target.StartsWith($snapshot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source snapshot path escaped the report directory.'
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
}
& $gitleaks dir $snapshot --redact --no-banner --report-format json --report-path (Join-Path $output 'secrets-worktree.json')
if ($LASTEXITCODE -ne 0) { throw 'Working-tree secret scan failed; inspect the redacted report.' }

[ordered]@{
    Passed = $true
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o')
    SourceCommit = (git -C $repositoryRoot rev-parse HEAD)
    DirtyWorkingTree = @(git -C $repositoryRoot status --porcelain).Count -gt 0
    GitleaksVersion = $version
    GitleaksArchiveSha256 = $expectedHash
    DependencyScan = 'NuGet direct and transitive packages'
    CodeScan = 'All .NET security analyzers; warnings treated as errors'
    SecretScan = 'All local Git refs and tracked/unignored source snapshot; redacted'
    Note = 'A clean scan does not prove absence of vulnerabilities or replace native/manual acceptance.'
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
Write-Output "Security checks passed. Reports: $output"
