#requires -Version 7.0
# Source-owned package validation only; this entry never authenticates or publishes.
$ErrorActionPreference = 'Stop'
$logRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$log = Join-Path $logRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Test-NuGet.log')
New-Item -ItemType File -Path $log -ErrorAction Stop | Out-Null
$testExit = 1
try {
    Push-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
    try {
        if ((dotnet --version) -ne '10.0.400') { throw 'SDK 10.0.400 is required.' }
        & dotnet restore ./LazyChromeWindowBridge.sln 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
        & dotnet build ./LazyChromeWindowBridge.sln -c Release --no-restore -p:ContinuousIntegrationBuild=true -warnaserror 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
        foreach ($test in @('Core', 'PublicApi')) {
            & dotnet "./tests/LazyChromeWindowBridge.$test.Tests/bin/Release/net10.0-windows/LazyChromeWindowBridge.$test.Tests.dll" 2>&1 | Tee-Object -FilePath $log -Append
            if ($LASTEXITCODE -ne 0) { throw "$test tests failed." }
        }
        & node --test ./tests/LazyChromeWindowBridge.Extension.Tests/bindings.test.mjs ./tests/LazyChromeWindowBridge.Extension.Tests/monitor.test.mjs ./tests/LazyChromeWindowBridge.Extension.Tests/downloads.test.mjs 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Extension tests failed.' }
        $packageRoot = Join-Path (Get-Location).Path 'artifacts/nuget'
        New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
        # Remove only these two known generated outputs, never a directory or Source.
        foreach ($extension in @('nupkg', 'snupkg')) {
            $oldPackage = Join-Path $packageRoot "LazyChromeWindowBridge.Core.0.1.0.$extension"
            if (Test-Path -LiteralPath $oldPackage) { Remove-Item -LiteralPath $oldPackage -ErrorAction Stop }
        }
        & dotnet pack ./src/LazyChromeWindowBridge.Core/LazyChromeWindowBridge.Core.csproj -c Release --no-build --no-restore -p:ContinuousIntegrationBuild=true -warnaserror -o $packageRoot 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'Core pack failed.' }
        & (Join-Path $PSScriptRoot 'Internal/VerifyNuGet.ps1') -SourceRoot (Get-Location).Path 2>&1 | Tee-Object -FilePath $log -Append
        'PASS: Release deterministic tests, package audit and local-feed consumer.' | Tee-Object -FilePath $log -Append
        $testExit = 0
    } finally { Pop-Location }
} catch {
    $_ | Out-String | Add-Content -LiteralPath $log
    Write-Host $_
} finally {
    Add-Content -LiteralPath $log -Value "Exit: $testExit"
    Write-Host ('Output: Scripts/Outputs/' + [IO.Path]::GetFileName($log))
}
exit $testExit
