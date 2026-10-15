param(
    [Parameter(Mandatory = $true)]
    [string]$PlantUmlJar,
    [string]$JavaPath = 'java',
    [switch]$PreviewPng
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$diagramRoot = Join-Path $repositoryRoot 'docs/requirements/diagrams/domain-model'
$sourcePaths = @(Get-ChildItem -LiteralPath (Join-Path $diagramRoot 'src') -Filter '*.puml' |
    Sort-Object Name | Select-Object -ExpandProperty FullName)
$jarPath = (Resolve-Path -LiteralPath $PlantUmlJar).Path
if ($sourcePaths.Count -eq 0) { throw 'No PlantUML sources found.' }

& $JavaPath '-Djava.awt.headless=true' -jar $jarPath -charset UTF-8 -checkonly @sourcePaths
if ($LASTEXITCODE -ne 0) { throw 'PlantUML syntax validation failed.' }

$svgDirectory = Join-Path $diagramRoot 'svg'
New-Item -ItemType Directory -Path $svgDirectory -Force | Out-Null
& $JavaPath '-Djava.awt.headless=true' -jar $jarPath -charset UTF-8 -failfast2 -nometadata -tsvg -o $svgDirectory @sourcePaths
if ($LASTEXITCODE -ne 0) { throw 'PlantUML SVG rendering failed.' }

foreach ($sourcePath in $sourcePaths) {
    $svgPath = Join-Path $svgDirectory ([IO.Path]::GetFileNameWithoutExtension($sourcePath) + '.svg')
    [xml]$svg = [IO.File]::ReadAllText($svgPath)
    if ($svg.DocumentElement.LocalName -ne 'svg' -or
        $svg.DocumentElement.NamespaceURI -ne 'http://www.w3.org/2000/svg') {
        throw "Invalid SVG: $svgPath"
    }
    if ($svg.SelectNodes('//*[local-name()="script" or local-name()="foreignObject"]').Count -gt 0) {
        throw "Unexpected active or embedded HTML content in $svgPath"
    }
    Write-Output ([IO.Path]::GetFileName($svgPath) + ': ' + $svg.DocumentElement.GetAttribute('width') +
        ' x ' + $svg.DocumentElement.GetAttribute('height'))
}

if ($PreviewPng) {
    $previewDirectory = Join-Path $repositoryRoot 'artifacts/domain-model-preview'
    New-Item -ItemType Directory -Path $previewDirectory -Force | Out-Null
    & $JavaPath '-Djava.awt.headless=true' -jar $jarPath -charset UTF-8 -failfast2 -nometadata -tpng -o $previewDirectory @sourcePaths
    if ($LASTEXITCODE -ne 0) { throw 'PlantUML PNG preview rendering failed.' }
}

Write-Output "Validated and rendered $($sourcePaths.Count) domain-model diagrams."
