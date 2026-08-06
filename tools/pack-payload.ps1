<#
.SYNOPSIS
    Packs a built C# output folder into the single payload the C++ bootstrapper embeds.

.DESCRIPTION
    Walks the source directory, and writes every file (except .pdb) into one archive that keeps
    each file's path relative to the source root, so the bootstrapper can recreate the folder
    structure on disk. The format is deliberately trivial - no compression, no dependency - so the
    native reader stays a few lines:

        magic   : 4 bytes  "BTP1"
        count   : uint32   little-endian
        repeated 'count' times:
            pathLen : uint32              little-endian, bytes of the UTF-8 relative path
            path    : pathLen bytes       UTF-8, backslash separators
            dataLen : uint64              little-endian
            data    : dataLen bytes

    Run at build time by the bootstrapper project, before the resource compiler.

.PARAMETER SourceDir
    The built C# output folder to embed (for example BetterTerminal.Shell\bin\x64\Release).

.PARAMETER OutFile
    The archive to write (referenced by the .rc as RCDATA).
#>
param(
    [Parameter(Mandatory = $true)][string]$SourceDir,
    [Parameter(Mandatory = $true)][string]$OutFile
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SourceDir)) {
    throw "Payload source directory not found: $SourceDir. Build the application first."
}

$root = (Resolve-Path -LiteralPath $SourceDir).Path.TrimEnd('\')
$files = Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -ne '.pdb' }

$outDir = Split-Path -Parent $OutFile
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) {
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null
}

$stream = [System.IO.File]::Create($OutFile)
try {
    $writer = New-Object System.IO.BinaryWriter($stream)
    $writer.Write([System.Text.Encoding]::ASCII.GetBytes('BTP1'))
    $writer.Write([uint32]$files.Count)

    foreach ($file in $files) {
        $relative = $file.FullName.Substring($root.Length).TrimStart('\')
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($relative)
        $writer.Write([uint32]$pathBytes.Length)
        $writer.Write($pathBytes)

        $data = [System.IO.File]::ReadAllBytes($file.FullName)
        $writer.Write([uint64]$data.LongLength)
        $writer.Write($data)
    }

    $writer.Flush()
}
finally {
    $stream.Dispose()
}

Write-Host "Packed $($files.Count) file(s) from $root into $OutFile"
