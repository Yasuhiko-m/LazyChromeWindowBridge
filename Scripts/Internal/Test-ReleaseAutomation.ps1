#requires -Version 7.0
# Source-owned deterministic release-automation contract checks; caller owns logging.
param([Parameter(Mandatory)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
$resolver = Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1'
$release = & $resolver -SourceRoot $SourceRoot
if ($release.Version -cne '0.3.2' -or $release.Tag -cne 'v0.3.2') { throw 'Resolved current release version is incorrect.' }
$fixture = & $resolver -SourceRoot $SourceRoot -AuthorityVersion @('0.3.2','0.3.2','0.3.2')
if ($fixture.Version -cne '0.3.2') { throw 'Matching version fixture did not resolve.' }
$rejected = $false
try { $null = & $resolver -SourceRoot $SourceRoot -AuthorityVersion @('0.3.2','0.3.3','0.3.2') } catch { $rejected = $true }
if (-not $rejected) { throw 'Mismatched version fixture was accepted.' }
$prereleaseRejected = $false
try { $null = & $resolver -SourceRoot $SourceRoot -AuthorityVersion @('0.3.2-preview.1','0.3.2-preview.1','0.3.2-preview.1') } catch { $prereleaseRejected = $true }
if (-not $prereleaseRejected) { throw 'Prerelease version fixture was accepted.' }
$manifestPath = Join-Path $SourceRoot '.lazy-ai-deck/github-operations.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -AsHashtable
if ($manifest.schemaVersion -ne 1 -or @($manifest.releases.Keys).Count -ne 1 -or $manifest.releases.Keys[0] -cne 'v0.3.2' -or
    (Compare-Object @('publish-cws','publish-nuget') @($manifest.workflows.Keys | Sort-Object))) { throw 'GitHub operations allowlist is not exact.' }
foreach ($name in @('publish-nuget','publish-cws')) {
    $workflow = $manifest.workflows[$name]
    if ($workflow.ref -cne 'releaseTag' -or @($workflow.inputs.Keys).Count -ne 1 -or $workflow.inputs.Keys[0] -cne 'version' -or $workflow.inputs.version.source -cne 'version' -or -not $workflow.inputs.version.required -or @($workflow.inputs.version.allowedValues).Count -ne 1 -or $workflow.inputs.version.allowedValues[0] -cne 'v0.3.2') { throw "Workflow allowlist is not bounded: $name" }
}
foreach ($path in @('Scripts/Test-NuGet.ps1','Scripts/Internal/VerifyNuGet.ps1','Scripts/Package-Windows.ps1')) {
    $text = Get-Content -LiteralPath (Join-Path $SourceRoot $path) -Raw
    if ($text -match 'artifacts/nuget/0\.3\.1|LazyChromeWindowBridge\.Core\.0\.3\.1|\$version\s*=\s*[\x27\x22]0\.3\.1') { throw "Active fixed 0.3.1 release path remains: $path" }
    if (-not $text.Contains('Resolve-ReleaseVersion.ps1')) { throw "Version resolver is not used: $path" }
}
foreach ($workflowPath in @('.github/workflows/publish-nuget.yml','.github/workflows/publish-cws.yml')) {
    $text = Get-Content -LiteralPath (Join-Path $SourceRoot $workflowPath) -Raw
    foreach ($required in @('workflow_dispatch:','version:','refs/tags/','Resolve-ReleaseVersion.ps1','persist-credentials: false')) { if (-not $text.Contains($required)) { throw "Immutable tag workflow check missing $required in $workflowPath" } }
}
$cws = Get-Content -LiteralPath (Join-Path $SourceRoot 'Scripts/Internal/Publish-Cws.ps1') -Raw
foreach ($required in @('https://www.googleapis.com/auth/chromewebstore','https://chromewebstore.googleapis.com/upload/v2/','chromewebstore.googleapis.com/v2/','DEFAULT_PUBLISH')) { if (-not $cws.Contains($required)) { throw "CWS V2 contract missing: $required" } }
foreach ($forbidden in @('skipReview','/v1/','[string]$Uri','[string]$Method')) { if ($cws.Contains($forbidden)) { throw "Forbidden CWS helper route or option: $forbidden" } }
if ($cws -match '(?im)^.*\bvisibility\s*=') { throw 'CWS helper must not mutate visibility.' }
$cwsContract = & (Join-Path $PSScriptRoot 'Publish-Cws.ps1') -SourceRoot $SourceRoot -Version $release.Tag -ContractTest | ConvertFrom-Json
if ($cwsContract.result -cne 'PASS' -or -not $cwsContract.synchronous -or -not $cwsContract.asyncSequence -or -not $cwsContract.nestedSubmittedRevision -or -not $cwsContract.rootStatusIgnored) { throw 'CWS V2 synthetic response-contract checks failed.' }
[pscustomobject]@{check='release-automation';result='PASS';version=$release.Version;tag=$release.Tag;resolverMismatchRejected=$rejected;resolverPrereleaseRejected=$prereleaseRejected;operationsManifest=$manifestPath;workflows=@('publish-nuget','publish-cws');cwsResponseContract=$true;cwsValidateOnly=$true} | ConvertTo-Json -Compress
