param([Parameter(Mandatory)][string]$ManifestPath)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function ConvertFrom-RecycleMetadata([byte[]]$Bytes) {
    # Version 2 layout: https://github.com/libyal/dtformats/blob/main/documentation/Windows%20Recycle.Bin%20file%20formats.asciidoc
    if ($Bytes.Length -lt 30 -or $Bytes.Length -gt 65564) { throw 'Invalid metadata length.' }
    $version = [BitConverter]::ToUInt64($Bytes, 0)
    if ($version -ne 2) { throw 'Unsupported metadata version; retain the raw bytes for inspection.' }
    $characters = [BitConverter]::ToUInt32($Bytes, 24)
    if ($characters -lt 2 -or $characters -gt 32768 -or (28 + 2L * $characters) -ne $Bytes.Length) {
        throw 'Invalid metadata path length.'
    }
    $unicode = [Text.UnicodeEncoding]::new($false, $false, $true)
    $pathWithTerminator = $unicode.GetString($Bytes, 28, $Bytes.Length - 28)
    if ($pathWithTerminator[$pathWithTerminator.Length - 1] -ne [char]0) { throw 'Missing path terminator.' }
    $originalPath = $pathWithTerminator.Substring(0, $pathWithTerminator.Length - 1)
    if ($originalPath.Contains([string][char]0) -or $originalPath -notmatch '^[A-Za-z]:\\') {
        throw 'Invalid original path.'
    }
    return [ordered]@{
        Version = $version
        OriginalBytes = [BitConverter]::ToUInt64($Bytes, 8)
        DeletedAtUtc = [DateTime]::FromFileTimeUtc([BitConverter]::ToInt64($Bytes, 16)).ToString('o')
        OriginalPath = $originalPath
    }
}

function Assert-UnredirectedPath([string]$Path) {
    $ancestor = [IO.Path]::GetFullPath($Path)
    while ($ancestor) {
        $item = Get-Item -LiteralPath $ancestor -Force -ErrorAction Stop
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing a redirected evidence path.' }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}

$computer = Get-CimInstance Win32_ComputerSystem
if ($computer.Manufacturer -ne 'Microsoft Corporation' -or $computer.Model -ne 'Virtual Machine' -or $env:USERNAME -ne 'DowlumeTester') {
    throw 'Read metadata only inside the disposable DowlumeTester Hyper-V guest.'
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try { $accountSid = $identity.User.Value } finally { $identity.Dispose() }
$fixtureRoot = Join-Path $env:LOCALAPPDATA 'Dowlume.RecycleAcceptance'
$rawBinPath = Join-Path (Join-Path ([IO.Path]::GetPathRoot($fixtureRoot)) '$Recycle.Bin') $accountSid
Assert-UnredirectedPath $rawBinPath
Assert-UnredirectedPath $ManifestPath
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$records = foreach ($expected in $manifest.Records) {
    if ($expected.Name -notmatch '^\$I[A-Za-z0-9]{6}\.bin$' -or $expected.Sha256 -notmatch '^[A-Fa-f0-9]{64}$') {
        throw 'Invalid metadata manifest entry.'
    }
    $path = Join-Path $rawBinPath $expected.Name
    $result = [ordered]@{ Name = $expected.Name; Sha256 = $null; RawBase64 = $null; Metadata = $null; ProbeReport = $null; Error = $null }
    try {
        Assert-UnredirectedPath $path
        $stream = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            if ($stream.Length -ne $expected.Bytes -or $stream.Length -gt 65564) { throw 'Metadata length changed since inventory.' }
            $reader = [IO.BinaryReader]::new($stream)
            try { $bytes = $reader.ReadBytes([int]$stream.Length) } finally { $reader.Dispose() }
        }
        finally { $stream.Dispose() }
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $result.Sha256 = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') } finally { $sha.Dispose() }
        $result.RawBase64 = [Convert]::ToBase64String($bytes)
        if ($result.Sha256 -ne $expected.Sha256) { throw 'Metadata checksum changed since inventory; do not correlate it as the earlier record.' }
        $result.Metadata = ConvertFrom-RecycleMetadata $bytes
        $relativePattern = '^' + [regex]::Escape($fixtureRoot + '\') + '(?<run>[a-fA-F0-9]{32})\\probe-(?<case>ordinary|disabled|oversized|sentinel|near-capacity)\.bin$'
        if ($result.Metadata.OriginalPath -notmatch $relativePattern) { throw 'Original path is outside the known probe layout; no report path was accessed.' }
        $run = $Matches['run']
        $case = $Matches['case']
        $reportPath = Join-Path (Join-Path $fixtureRoot $run) ($case + '.json')
        Assert-UnredirectedPath $reportPath
        $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        $result.ProbeReport = [ordered]@{
            Run = $run
            Case = $case
            ReportSha256 = (Get-FileHash -LiteralPath $reportPath).Hash
            ExpectedContentSha256 = $report.ExpectedHash
            RecordedAtUtc = $report.RecordedAtUtc
            AdapterReturnedSuccess = $report.AdapterReturnedSuccess
            SourceExistsImmediatelyAfterAdapter = $report.SourceExists
            Note = 'Association by original path. The content hash comes from the probe report, not the metadata; missing payload content is not verified.'
        }
    }
    catch { $result.Error = $_.Exception.Message }
    $result
}
[ordered]@{
    CollectedAtUtc = [DateTime]::UtcNow.ToString('o')
    Note = 'Read-only, checksum-pinned metadata and existing probe reports. Console output only; no restore, deletion, policy changes or report files written.'
    Records = @($records)
} | ConvertTo-Json -Depth 8
