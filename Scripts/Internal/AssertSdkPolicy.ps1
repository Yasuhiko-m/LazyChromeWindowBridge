# Source-owned helper. Invoke from the Source root after global.json resolution.
$ErrorActionPreference = 'Stop'
$policy = Get-Content ./global.json -Raw | ConvertFrom-Json
if ($policy.sdk.version -cne '10.0.401' -or $policy.sdk.rollForward -cne 'latestPatch' -or $policy.sdk.allowPrerelease) {
    throw 'global.json must require stable .NET SDK 10.0.401 with latestPatch roll-forward.'
}
$resolved = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $resolved -notmatch '^10\.0\.4\d{2}$') {
    throw "Resolved SDK '$resolved' is not a stable .NET 10.0.4xx servicing SDK."
}
Write-Output "SDK policy PASS: stable .NET 10.0.4xx servicing SDK $resolved resolved from global.json."
