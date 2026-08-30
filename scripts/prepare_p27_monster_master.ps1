[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$destinationDirectory = Join-Path $RepositoryRoot 'src\Game.Godot\art-source\p27\imagegen'
$destination = Join-Path $destinationDirectory 'p27-monster-family-master.png'
New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

$input = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $Source))
try {
    $output = [System.Drawing.Bitmap]::new($input.Width, $input.Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($output)
        try { $graphics.DrawImageUnscaled($input, 0, 0) } finally { $graphics.Dispose() }
        $bounds = [System.Drawing.Rectangle]::new(0, 0, $output.Width, $output.Height)
        $data = $output.LockBits($bounds, [System.Drawing.Imaging.ImageLockMode]::ReadWrite,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $length = [math]::Abs($data.Stride) * $data.Height
            $bytes = [byte[]]::new($length)
            [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $length)
            for ($offset = 0; $offset -lt $length; $offset += 4) {
                $blue = $bytes[$offset]; $green = $bytes[$offset + 1]; $red = $bytes[$offset + 2]
                $minimum = [math]::Min($red, [math]::Min($green, $blue))
                $maximum = [math]::Max($red, [math]::Max($green, $blue))
                $bytes[$offset + 3] = if ($minimum -ge 228 -and ($maximum - $minimum) -le 14) { 0 } else { 255 }
            }
            [System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $length)
        } finally { $output.UnlockBits($data) }
        $output.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $output.Dispose() }
} finally { $input.Dispose() }

Write-Host "P27 monster family source prepared at $destination"
