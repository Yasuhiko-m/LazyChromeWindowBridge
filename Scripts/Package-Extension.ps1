#requires -Version 7.0
# Source-owned local packaging only. Never commits, publishes or changes the manifest.
$ErrorActionPreference = 'Stop'
$packageOutputRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Path $packageOutputRoot -Force | Out-Null
$packageLog = Join-Path $packageOutputRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Package-Extension.log')
New-Item -ItemType File -Path $packageLog -ErrorAction Stop | Out-Null
$packageExit = 1
try {
    $packageRoot = Split-Path -Parent $PSScriptRoot
    & (Join-Path $PSScriptRoot 'Internal/PackageExtension.ps1') -SourceRoot $packageRoot 2>&1 | Tee-Object -FilePath $packageLog -Append
    $packageExit = 0
} catch {
    $_ | Out-String | Add-Content -LiteralPath $packageLog
    Write-Host $_
} finally {
    Add-Content -LiteralPath $packageLog -Value "Exit: $packageExit"
    Write-Host ('Output: Scripts/Outputs/' + [IO.Path]::GetFileName($packageLog))
}
exit $packageExit
