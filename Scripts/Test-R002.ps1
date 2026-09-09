# Source-owned Windows validation entry. Requires PowerShell 7 and Node.js 22+.
param([Parameter(Mandatory = $true)][string]$ChromeExecutable)
$ErrorActionPreference = 'Stop'
$scriptOutputRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Path $scriptOutputRoot -Force | Out-Null
$scriptLog = Join-Path $scriptOutputRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Test-R002.log')
New-Item -ItemType File -Path $scriptLog -ErrorAction Stop | Out-Null
$scriptExit = 1
try {
    $sourceRoot = Split-Path -Parent $PSScriptRoot
    Push-Location -LiteralPath $sourceRoot
    try {
        & dotnet '.\tests\CallerHarness.Tests\bin\Debug\net10.0-windows\CallerHarness.Tests.dll' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Caller/transport tests failed.' }
        & node --test '.\tests\bindings.test.mjs' 2>&1 | Tee-Object -FilePath $scriptLog -Append
        if ($LASTEXITCODE -ne 0) { $scriptExit = $LASTEXITCODE; throw 'Extension rehydration tests failed.' }
        & node (Join-Path $PSScriptRoot 'Internal\Test-R002.mjs') $ChromeExecutable 2>&1 | Tee-Object -FilePath $scriptLog -Append
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
