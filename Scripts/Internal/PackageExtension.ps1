#requires -Version 7.0
# Source-owned helper; public wrappers own logging. No network or Git mutation.
param([Parameter(Mandatory)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
$packageRoot = [IO.Path]::GetFullPath($SourceRoot)
$release = & (Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1') -SourceRoot $packageRoot
$extensionRoot = Join-Path $packageRoot 'src/LazyChromeWindowBridge.Extension'
$packageNames = @('bindings.js','bootstrap.js','downloads.js','icons/icon-16.png','icons/icon-32.png','icons/icon-48.png','icons/icon-128.png','manifest.json','monitor.js','service-worker.js')
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
            $bytes = if ($name.EndsWith('.png')) { [IO.File]::ReadAllBytes((Join-Path $extensionRoot $name)) } else {
                $text = [IO.File]::ReadAllText((Join-Path $extensionRoot $name)).Replace("`r`n", "`n")
                [Text.UTF8Encoding]::new($false).GetBytes($text)
            }
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
$manifest = Get-Content -LiteralPath (Join-Path $extensionRoot 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.version -cne $release.Version) { throw 'Extension manifest and resolved product version disagree.' }
$artifactRoot = Join-Path $packageRoot 'artifacts/cws'
New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null
$artifactName = "LazyChromeWindowBridge.Extension-$($release.Version)-cws.zip"
$artifact = Join-Path $artifactRoot $artifactName
[IO.File]::WriteAllBytes($artifact, $firstArchive)
[pscustomobject]@{check='extension-package';result='PASS';artifact=(Join-Path 'artifacts/cws' $artifactName).Replace('\','/');files=$packageNames;bytes=$firstArchive.Length;sha256=$firstHash;repeatIdentical=$true;manifestVersion=$manifest.version;releaseVersion=$release.Version} |
    ConvertTo-Json -Compress
