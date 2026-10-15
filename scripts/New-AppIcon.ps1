# Rebuild the original code-drawn application icon; no external artwork is used.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$iconPath = Join-Path $repositoryRoot 'Dowlume/Dowlume.App/Assets/dowlume.ico'
$bitmap = New-Object System.Drawing.Bitmap(64, 64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$background = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(52, 86, 207))
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 5)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
try {
    $graphics.FillEllipse($background, 1, 1, 62, 62)
    $graphics.DrawLine($pen, 32, 15, 32, 38)
    $graphics.DrawLine($pen, 23, 30, 32, 39)
    $graphics.DrawLine($pen, 41, 30, 32, 39)
    $graphics.DrawLine($pen, 19, 46, 45, 46)
    $imageStream = New-Object System.IO.MemoryStream
    $bitmap.Save($imageStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $imageStream.ToArray()
    $output = [IO.File]::Create($iconPath)
    $writer = New-Object System.IO.BinaryWriter($output)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
        $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22)
        $writer.Write($bytes)
    } finally { $writer.Dispose(); $output.Dispose(); $imageStream.Dispose() }
} finally { $pen.Dispose(); $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
