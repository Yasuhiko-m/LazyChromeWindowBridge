#requires -Version 7.0
# Source-owned helper; public wrappers own logging. No network or Git mutation.
param([Parameter(Mandatory)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($SourceRoot)
$extensionRoot = Join-Path $packageRoot 'src/LazyChromeWindowBridge.Extension'
$packageNames = @('bindings.js','bootstrap.js','downloads.js','manifest.json','monitor.js','service-worker.js')
$actualNames = @(Get-ChildItem -LiteralPath $extensionRoot -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($extensionRoot, $_.FullName).Replace('\','/') } | Sort-Object)
if (@(Compare-Object $packageNames $actualNames).Count) { throw 'Unexpected extension files; review the explicit release allowlist.' }
function New-ExtensionArchive {
    $memory = [IO.MemoryStream]::new()
    $archive = [IO.Compression.ZipArchive]::new($memory, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($name in $packageNames) {
            $entry = $archive.CreateEntry($name, [IO.Compression.CompressionLevel]::NoCompression)
            $entry.LastWriteTime = [DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
            $entry.ExternalAttributes = 0
            # Canonical archive text; never rewrites Source working files.
            $text = [IO.File]::ReadAllText((Join-Path $extensionRoot $name)).Replace("`r`n", "`n")
            $bytes = [Text.UTF8Encoding]::new($false).GetBytes($text)
            $stream = $entry.Open()
            try { $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() }
        }
    } finally { $archive.Dispose() }
    try { return ,$memory.ToArray() } finally { $memory.Dispose() }
}
[byte[]]$firstArchive = New-ExtensionArchive
[byte[]]$secondArchive = New-ExtensionArchive
$firstHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($firstArchive))
$secondHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($secondArchive))
if ($firstHash -cne $secondHash) { throw 'Reproducibility check failed.' }
$artifactRoot = Join-Path $packageRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$artifact = Join-Path $artifactRoot 'LazyChromeWindowBridge.Extension-v0.1.0.zip'
[IO.File]::WriteAllBytes($artifact, $firstArchive)
[pscustomobject]@{check='extension-package';result='PASS';artifact='artifacts/LazyChromeWindowBridge.Extension-v0.1.0.zip';files=$packageNames;bytes=$firstArchive.Length;sha256=$firstHash;repeatIdentical=$true;manifestVersion='0.0.7';releaseVersion='0.1.0'} |
    ConvertTo-Json -Compress
