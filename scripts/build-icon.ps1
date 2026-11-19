param(
    [string]$Source,
    [Parameter(Mandatory=$true)][string]$Output
)
$ErrorActionPreference = 'Stop'
if (!$Source) { $Source = Join-Path $PSScriptRoot '..\src\Nocturne\Assets\Brand\shion.png' }
Add-Type -AssemblyName System.Drawing
$original = [Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Source).Path)
try {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $frames = foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $scale = $size / [Math]::Max($original.Width, $original.Height)
            $width = [float]($original.Width * $scale)
            $height = [float]($original.Height * $scale)
            $graphics.DrawImage($original, [Drawing.RectangleF]::new(($size-$width)/2, ($size-$height)/2, $width, $height))
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            [pscustomobject]@{ Size = $size; Png = $stream.ToArray() }
        } finally {
            $stream.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
    $path = [IO.Path]::GetFullPath($Output)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    $writer = [IO.BinaryWriter]::new([IO.File]::Create($path))
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = [byte]($frame.Size % 256)
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Png.Length)
            $writer.Write([uint32]$offset)
            $offset += $frame.Png.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Png) }
    } finally { $writer.Dispose() }
} finally { $original.Dispose() }
