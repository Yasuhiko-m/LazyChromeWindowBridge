#requires -Version 7.0
# Source-owned version authority resolver. It is local-only and never changes Git or network state.
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [string[]]$AuthorityVersion
)
$ErrorActionPreference = 'Stop'

function Assert-StableVersion([string]$Value, [string]$Authority) {
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -notmatch '^(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)$') {
        throw "$Authority must declare a stable three-part product version."
    }
    return $Value
}

if ($PSBoundParameters.ContainsKey('AuthorityVersion')) {
    if ($AuthorityVersion.Count -ne 3) { throw 'Exactly three authority fixture values are required.' }
    $versions = @($AuthorityVersion | ForEach-Object { Assert-StableVersion $_ 'Version authority fixture' })
} else {
    $root = [IO.Path]::GetFullPath($SourceRoot)
    $corePath = Join-Path $root 'src/LazyChromeWindowBridge.Core/LazyChromeWindowBridge.Core.csproj'
    $samplePath = Join-Path $root 'samples/LazyChromeWindowBridge.SampleCaller/LazyChromeWindowBridge.SampleCaller.csproj'
    $manifestPath = Join-Path $root 'src/LazyChromeWindowBridge.Extension/manifest.json'
    foreach ($path in @($corePath, $samplePath, $manifestPath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required version authority is missing: $path" }
    }
    [xml]$core = Get-Content -LiteralPath $corePath -Raw
    [xml]$sample = Get-Content -LiteralPath $samplePath -Raw
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    $versions = @(
        (Assert-StableVersion ([string]$core.Project.PropertyGroup.Version) 'Core package'),
        (Assert-StableVersion ([string]$manifest.version) 'Extension manifest'),
        (Assert-StableVersion ([string]$sample.Project.PropertyGroup.Version) 'SampleCaller'))
}
if (@($versions | Select-Object -Unique).Count -ne 1) { throw "Product version authorities disagree: $($versions -join ', ')." }
[pscustomobject]@{ Version = $versions[0]; Tag = 'v' + $versions[0] }
