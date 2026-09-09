#requires -Version 7.0
# Source-owned validation. Real isolated browser acceptance is opt-in by executable.
param([string]$ChromeExecutable, [switch]$Gui, [switch]$Clean)
$ErrorActionPreference = 'Stop'
$scriptOutputRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Path $scriptOutputRoot -Force | Out-Null
$scriptLog = Join-Path $scriptOutputRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Test-All.log')
New-Item -ItemType File -Path $scriptLog -ErrorAction Stop | Out-Null
$scriptExit = 1
try {
    $sourceRoot = Split-Path -Parent $PSScriptRoot
    Push-Location -LiteralPath $sourceRoot
    try {
        if ($Gui -and -not $ChromeExecutable) { throw '-Gui requires -ChromeExecutable.' }
        if ($Clean) {
            & dotnet clean '.\LazyChromeWindowBridge.sln' 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Clean failed.' }
        }
        & dotnet restore '.\LazyChromeWindowBridge.sln' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Restore failed.' }
        & dotnet build '.\LazyChromeWindowBridge.sln' --no-restore 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Build failed.' }
        & dotnet '.\tests\LazyChromeWindowBridge.Core.Tests\bin\Debug\net10.0-windows\LazyChromeWindowBridge.Core.Tests.dll' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Caller/transport tests failed.' }
        & dotnet '.\tests\LazyChromeWindowBridge.PublicApi.Tests\bin\Debug\net10.0-windows\LazyChromeWindowBridge.PublicApi.Tests.dll' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Public API consumer tests failed.' }
        & node --test '.\tests\LazyChromeWindowBridge.Extension.Tests\bindings.test.mjs' '.\tests\LazyChromeWindowBridge.Extension.Tests\monitor.test.mjs' '.\tests\LazyChromeWindowBridge.Extension.Tests\downloads.test.mjs' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Extension rehydration tests failed.' }
        & node (Join-Path $PSScriptRoot 'Internal\AuditSource.mjs') 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Source name/security/hygiene audit failed.' }
        & (Join-Path $PSScriptRoot 'Internal\PackageExtension.ps1') -SourceRoot $sourceRoot 2>&1 | Tee-Object -FilePath $scriptLog -Append
        & node (Join-Path $PSScriptRoot 'Internal\AuditPublicRelease.mjs') 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Public release/image/link/package audit failed.' }
        if ($ChromeExecutable -and -not $Gui) {
            & node (Join-Path $PSScriptRoot 'Internal\BrowserAcceptance.mjs') $ChromeExecutable --downloads 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Download lifecycle acceptance failed.' }
            & node (Join-Path $PSScriptRoot 'Internal\BrowserAcceptance.mjs') $ChromeExecutable --geometry 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Session/native geometry regressions failed.' }
            & node (Join-Path $PSScriptRoot 'Internal\BrowserAcceptance.mjs') $ChromeExecutable --monitor 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Monitor regressions failed.' }
        }
        if ($ChromeExecutable) {
            $testMode = if ($Gui) { '--gui' } else { '--multi-monitor' }
            & node (Join-Path $PSScriptRoot 'Internal\BrowserAcceptance.mjs') $ChromeExecutable $testMode 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Browser/sample acceptance failed.' }
        } else {
            'Real browser acceptance skipped: provide -ChromeExecutable with Chrome for Testing.' | Tee-Object -FilePath $scriptLog -Append
        }
        $scriptExit = 0
    } finally { Pop-Location }
} catch {
    $_ | Out-String | Add-Content -LiteralPath $scriptLog
    Write-Host $_
} finally {
    Add-Content -LiteralPath $scriptLog -Value "Exit: $scriptExit"
    Write-Host ('Output: Scripts/Outputs/' + [IO.Path]::GetFileName($scriptLog))
}
exit $scriptExit
