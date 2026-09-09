# Source-owned Windows validation entry. Requires PowerShell 7 and Node.js 22+.
param([Parameter(Mandatory = $true)][string]$ChromeExecutable, [switch]$Gui)
$ErrorActionPreference = 'Stop'
$scriptOutputRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Path $scriptOutputRoot -Force | Out-Null
$scriptLog = Join-Path $scriptOutputRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Test-R005.log')
New-Item -ItemType File -Path $scriptLog -ErrorAction Stop | Out-Null
$scriptExit = 1
try {
    $sourceRoot = Split-Path -Parent $PSScriptRoot
    Push-Location -LiteralPath $sourceRoot
    try {
        & dotnet '.\tests\CallerHarness.Tests\bin\Debug\net10.0-windows\CallerHarness.Tests.dll' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Caller/transport tests failed.' }
        & node --test '.\tests\bindings.test.mjs' '.\tests\monitor.test.mjs' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Extension rehydration tests failed.' }
        if (-not $Gui) {
            & node (Join-Path $PSScriptRoot 'Internal\Test-R002.mjs') $ChromeExecutable --geometry 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'M001/M002 browser regressions failed.' }
            & node (Join-Path $PSScriptRoot 'Internal\Test-R002.mjs') $ChromeExecutable --monitor 2>&1 | Tee-Object -FilePath $scriptLog -Append
            if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'M003 monitor regressions failed.' }
        }
        $testMode = if ($Gui) { '--gui' } else { '--multi-monitor' }
        & node (Join-Path $PSScriptRoot 'Internal\Test-R002.mjs') $ChromeExecutable $testMode 2>&1 | Tee-Object -FilePath $scriptLog -Append
        $scriptExit = $LASTEXITCODE
    } finally { Pop-Location }
} catch {
    $_ | Out-String | Add-Content -LiteralPath $scriptLog
    Write-Host $_
} finally {
    Add-Content -LiteralPath $scriptLog -Value "Exit: $scriptExit"
    Write-Host ('Output: Scripts/Outputs/' + [IO.Path]::GetFileName($scriptLog))
}
exit $scriptExit
