#requires -Version 7.0
# Source-owned local release preparation. Never authenticates, publishes, tags, commits or pushes.
$ErrorActionPreference = 'Stop'
$outputRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$log = Join-Path $outputRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Prepare-Release.log')
New-Item -ItemType File -Path $log -ErrorAction Stop | Out-Null
$exitCode = 1
try {
    $sourceRoot = Split-Path -Parent $PSScriptRoot
    Push-Location -LiteralPath $sourceRoot
    try {
        & (Join-Path $PSScriptRoot 'Internal/AssertSdkPolicy.ps1') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'SDK policy check failed.' }
        $release = & (Join-Path $PSScriptRoot 'Internal/Resolve-ReleaseVersion.ps1') -SourceRoot $sourceRoot
        $notes = Join-Path $sourceRoot "docs/releases/$($release.Tag).md"
        if (-not (Test-Path -LiteralPath $notes -PathType Leaf)) { throw "Release notes are required: docs/releases/$($release.Tag).md" }
        & (Join-Path $PSScriptRoot 'Internal/Test-ReleaseAutomation.ps1') -SourceRoot $sourceRoot 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Release automation contract checks failed.' }
        & (Join-Path $PSScriptRoot 'Test-NuGet.ps1') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'NuGet validation failed.' }
        & (Join-Path $PSScriptRoot 'Package-Extension.ps1') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Extension packaging failed.' }
        & (Join-Path $PSScriptRoot 'Package-Windows.ps1') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Windows packaging failed.' }
        & node (Join-Path $PSScriptRoot 'Internal/AuditSource.mjs') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Source audit failed.' }
        & node (Join-Path $PSScriptRoot 'Internal/AuditPublicRelease.mjs') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Public release audit failed.' }
        $artifacts = @(
            "artifacts/nuget/$($release.Version)/LazyChromeWindowBridge.Core.$($release.Version).nupkg",
            "artifacts/nuget/$($release.Version)/LazyChromeWindowBridge.Core.$($release.Version).snupkg",
            "artifacts/cws/LazyChromeWindowBridge.Extension-$($release.Version)-cws.zip",
            "artifacts/release/$($release.Version)/LazyChromeWindowBridge-v$($release.Version)-win-x64.zip")
        $inventory = foreach ($relative in $artifacts) {
            $path = Join-Path $sourceRoot $relative
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Expected release artifact missing: $relative" }
            [pscustomobject]@{ path = $relative.Replace('\','/'); bytes = (Get-Item -LiteralPath $path).Length; sha256 = (Get-FileHash -LiteralPath $path).Hash }
        }
        [pscustomobject]@{ check='prepare-release'; result='PASS'; version=$release.Version; tag=$release.Tag; notes=[IO.Path]::GetRelativePath($sourceRoot,$notes).Replace('\','/'); artifacts=$inventory } | ConvertTo-Json -Depth 4 -Compress | Tee-Object -FilePath $log -Append
        $exitCode = 0
    } finally { Pop-Location }
} catch { $_ | Out-String | Add-Content -LiteralPath $log; Write-Host $_ }
finally { Add-Content -LiteralPath $log -Value "Exit: $exitCode"; Write-Host ('Output: Scripts/Outputs/' + [IO.Path]::GetFileName($log)) }
exit $exitCode
