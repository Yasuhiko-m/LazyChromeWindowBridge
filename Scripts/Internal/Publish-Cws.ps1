#requires -Version 7.0
# Existing-item Chrome Web Store V2 publisher. Workflow-only mutation path; no Git or visibility changes.
param(
    [Parameter(Mandatory)][string]$SourceRoot,
    [Parameter(Mandatory)][string]$Version,
    [switch]$ValidateOnly,
    [switch]$ContractTest
)
$ErrorActionPreference = 'Stop'
$oauthScope = 'https://www.googleapis.com/auth/chromewebstore'
$tokenEndpoint = 'https://oauth2.googleapis.com/token'
function To-Base64Url([byte[]]$Bytes) { ([Convert]::ToBase64String($Bytes)).TrimEnd('=').Replace('+','-').Replace('/','_') }
function Get-PropertyValue($Object, [string]$Name) { if ($null -ne $Object -and $null -ne $Object.PSObject.Properties[$Name]) { return [string]$Object.$Name }; return $null }
function Assert-OptionalItemIdentity($Response, [string]$ExpectedItemId, [string]$ResponseName) {
    $itemId = Get-PropertyValue $Response 'itemId'
    if ($itemId -and $itemId -cne $ExpectedItemId) { throw "$ResponseName returned a different existing item." }
}
function Get-UploadState($Response, [string]$ExpectedVersion, [string]$ExpectedItemId) {
    Assert-OptionalItemIdentity $Response $ExpectedItemId 'CWS upload'
    $state = Get-PropertyValue $Response 'uploadState'
    switch ($state) {
        'SUCCEEDED' {
            if ((Get-PropertyValue $Response 'crxVersion') -cne $ExpectedVersion) { throw 'Synchronous CWS upload did not identify the expected extension version.' }
            return $state
        }
        'IN_PROGRESS' { return $state }
        default { throw "CWS upload returned an unsupported state: $state" }
    }
}
function Get-AsyncUploadState($Response) {
    $state = Get-PropertyValue $Response 'lastAsyncUploadState'
    if ($state -in @('SUCCEEDED','IN_PROGRESS')) { return $state }
    throw "CWS asynchronous upload returned an unsupported state: $state"
}
function Get-SubmittedRevision($Response, [string]$ExpectedVersion, [string]$ExpectedItemId, [switch]$AllowMissing) {
    Assert-OptionalItemIdentity $Response $ExpectedItemId 'CWS status'
    $revision = if ($null -ne $Response -and $null -ne $Response.PSObject.Properties['submittedItemRevisionStatus']) { $Response.submittedItemRevisionStatus } else { $null }
    if ($null -eq $revision) { if ($AllowMissing) { return $null }; throw 'CWS status did not contain submitted revision metadata.' }
    $state = Get-PropertyValue $revision 'state'
    if ([string]::IsNullOrWhiteSpace($state)) { if ($AllowMissing) { return $null }; throw 'CWS submitted revision state is missing.' }
    if ($state -notin @('PENDING_REVIEW','PUBLISHED')) { throw "CWS submitted revision state is not acceptable: $state" }
    $channels = @($revision.distributionChannels)
    if ($channels.Count -eq 0) { if ($AllowMissing) { return $null }; throw 'CWS submitted revision did not contain distribution channels.' }
    if (-not @($channels | Where-Object { (Get-PropertyValue $_ 'crxVersion') -ceq $ExpectedVersion }).Count) { throw 'CWS submitted revision did not identify the expected extension version.' }
    return [pscustomobject]@{ State=$state; Version=$ExpectedVersion }
}
function Assert-Throws([scriptblock]$Action, [string]$Name) {
    try { & $Action } catch { return }
    throw "Expected rejection did not occur: $Name"
}
function Invoke-ResponseContractTests([string]$ExpectedVersion) {
    $id = 'abcdefghijklmnopabcdefghijklmnop'
    if ((Get-UploadState ([pscustomobject]@{ itemId=$id; uploadState='SUCCEEDED'; crxVersion=$ExpectedVersion }) $ExpectedVersion $id) -ne 'SUCCEEDED') { throw 'Synchronous upload fixture failed.' }
    Assert-Throws { Get-UploadState ([pscustomobject]@{ uploadState='SUCCEEDED'; crxVersion='9.9.9' }) $ExpectedVersion $id } 'synchronous wrong version'
    if ((Get-UploadState ([pscustomobject]@{ uploadState='IN_PROGRESS' }) $ExpectedVersion $id) -ne 'IN_PROGRESS' -or
        (Get-AsyncUploadState ([pscustomobject]@{ lastAsyncUploadState='IN_PROGRESS' })) -ne 'IN_PROGRESS' -or
        (Get-AsyncUploadState ([pscustomobject]@{ lastAsyncUploadState='SUCCEEDED' })) -ne 'SUCCEEDED') { throw 'Asynchronous upload fixture failed.' }
    foreach ($state in @('FAILED','NOT_FOUND','UNKNOWN','')) { Assert-Throws { Get-AsyncUploadState ([pscustomobject]@{ lastAsyncUploadState=$state }) } "async $state" }
    $nested = [pscustomobject]@{ itemId=$id; state='REJECTED'; itemVersion='9.9.9'; submittedItemRevisionStatus=[pscustomobject]@{ state='PENDING_REVIEW'; distributionChannels=@([pscustomobject]@{ crxVersion=$ExpectedVersion; deployPercentage=100 }) } }
    $revision = Get-SubmittedRevision $nested $ExpectedVersion $id
    if ($revision.State -ne 'PENDING_REVIEW' -or $revision.Version -ne $ExpectedVersion) { throw 'Nested submitted revision fixture failed.' }
    Assert-Throws { Get-SubmittedRevision ([pscustomobject]@{ submittedItemRevisionStatus=[pscustomobject]@{ state='PENDING_REVIEW'; distributionChannels=@([pscustomobject]@{ crxVersion='9.9.9' }) } }) $ExpectedVersion $id } 'submitted wrong version'
    foreach ($state in @('REJECTED','CANCELLED','','UNKNOWN')) { Assert-Throws { Get-SubmittedRevision ([pscustomobject]@{ submittedItemRevisionStatus=[pscustomobject]@{ state=$state; distributionChannels=@([pscustomobject]@{ crxVersion=$ExpectedVersion }) } }) $ExpectedVersion $id } "submitted $state" }
    [pscustomobject]@{ synchronous=$true; asyncSequence=$true; nestedSubmittedRevision=$true; rootStatusIgnored=$true }
}
function New-ServiceAccountAssertion([string]$ClientEmail, [string]$PrivateKey) {
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $header = To-Base64Url([Text.Encoding]::UTF8.GetBytes('{"alg":"RS256","typ":"JWT"}'))
    $claims = @{ iss=$ClientEmail; scope=$oauthScope; aud=$tokenEndpoint; iat=$now; exp=($now + 300) } | ConvertTo-Json -Compress
    $unsigned = "$header.$(To-Base64Url([Text.Encoding]::UTF8.GetBytes($claims)))"
    $rsa = [Security.Cryptography.RSA]::Create()
    try { $rsa.ImportFromPem($PrivateKey.ToCharArray()); $signature = To-Base64Url($rsa.SignData([Text.Encoding]::UTF8.GetBytes($unsigned),[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)) }
    finally { $rsa.Dispose() }
    return "$unsigned.$signature"
}

$release = & (Join-Path $PSScriptRoot 'Resolve-ReleaseVersion.ps1') -SourceRoot $SourceRoot
if ($Version -cne $release.Tag) { throw 'Requested CWS tag does not match resolved Source version.' }
$publisherId = [string]$env:CWS_PUBLISHER_ID
$extensionId = [string]$env:CWS_EXTENSION_ID
if (-not ($ValidateOnly -or $ContractTest)) {
    if ($publisherId -notmatch '^\d+$') { throw 'CWS_PUBLISHER_ID must be the existing numeric publisher identifier.' }
    if ($extensionId -notmatch '^[a-p]{32}$') { throw 'CWS_EXTENSION_ID must be the existing Chrome extension identifier.' }
}
$artifact = Join-Path $SourceRoot "artifacts/cws/LazyChromeWindowBridge.Extension-$($release.Version)-cws.zip"
if (-not (Test-Path -LiteralPath $artifact -PathType Leaf)) { throw 'Expected deterministic extension ZIP is missing.' }
$base = "https://chromewebstore.googleapis.com/v2/publishers/$publisherId/items/$extensionId"
$uploadEndpoint = "https://chromewebstore.googleapis.com/upload/v2/publishers/$publisherId/items/$extensionId`:upload"
$statusEndpoint = "$base`:fetchStatus"
$publishEndpoint = "$base`:publish"
if ($ContractTest) {
    $contract = Invoke-ResponseContractTests $release.Version
    [pscustomobject]@{ check='cws-v2-response-contract'; result='PASS'; version=$release.Version; synchronous=$contract.synchronous; asyncSequence=$contract.asyncSequence; nestedSubmittedRevision=$contract.nestedSubmittedRevision; rootStatusIgnored=$contract.rootStatusIgnored } | ConvertTo-Json -Compress
    return
}
if ($ValidateOnly) {
    $testRsa = [Security.Cryptography.RSA]::Create(2048)
    try { $assertion = New-ServiceAccountAssertion 'validation@example.test' ($testRsa.ExportPkcs8PrivateKeyPem()) }
    finally { $testRsa.Dispose() }
    if ($assertion.Split('.').Count -ne 3) { throw 'Synthetic RS256 assertion construction failed.' }
    [pscustomobject]@{ check='cws-v2-request-construction'; result='PASS'; version=$release.Version; oauthScope=$oauthScope; endpoints=@($uploadEndpoint,$statusEndpoint,$publishEndpoint); jwtRs256Constructed=$true; networkMutation=$false } | ConvertTo-Json -Compress
    return
}
$rawCredential = [string]$env:CWS_SERVICE_ACCOUNT_JSON
if ([string]::IsNullOrWhiteSpace($rawCredential)) { throw 'CWS_SERVICE_ACCOUNT_JSON is required only for CWS publication.' }
try { $credential = $rawCredential | ConvertFrom-Json } catch { throw 'CWS_SERVICE_ACCOUNT_JSON is malformed.' }
foreach ($field in @('client_email','private_key')) { if ([string]::IsNullOrWhiteSpace([string]$credential.$field)) { throw "Service account $field is missing." } }
if ([string]$credential.token_uri -ne $tokenEndpoint) { throw 'Service account token_uri is not the approved Google OAuth endpoint.' }
$assertion = New-ServiceAccountAssertion ([string]$credential.client_email) ([string]$credential.private_key)
$token = Invoke-RestMethod -Method Post -Uri $tokenEndpoint -ContentType 'application/x-www-form-urlencoded' -Body @{ grant_type='urn:ietf:params:oauth:grant-type:jwt-bearer'; assertion=$assertion }
if ([string]::IsNullOrWhiteSpace([string]$token.access_token)) { throw 'OAuth token response did not contain an access token.' }
$headers = @{ Authorization = "Bearer $($token.access_token)" }
$upload = Invoke-RestMethod -Method Post -Uri $uploadEndpoint -Headers $headers -ContentType 'application/zip' -InFile $artifact
$uploadState = Get-UploadState $upload $release.Version $extensionId
if ($uploadState -eq 'IN_PROGRESS') {
    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do {
        $status = Invoke-RestMethod -Method Get -Uri $statusEndpoint -Headers $headers
        $uploadState = Get-AsyncUploadState $status
        if ($uploadState -eq 'SUCCEEDED') { break }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'CWS upload did not complete within two minutes.' }
        Start-Sleep -Seconds 10
    } while ($true)
}
$submitted = Invoke-RestMethod -Method Post -Uri $publishEndpoint -Headers $headers -ContentType 'application/json' -Body '{"publishType":"DEFAULT_PUBLISH"}'
Assert-OptionalItemIdentity $submitted $extensionId 'CWS publish'
$revision = $null
for ($attempt = 0; $attempt -lt 4 -and $null -eq $revision; $attempt++) {
    $status = Invoke-RestMethod -Method Get -Uri $statusEndpoint -Headers $headers
    $revision = Get-SubmittedRevision $status $release.Version $extensionId -AllowMissing
    if ($null -eq $revision -and $attempt -lt 3) { Start-Sleep -Seconds 5 }
}
if ($null -eq $revision) { throw 'CWS submitted revision metadata did not propagate within the bounded wait.' }
[pscustomobject]@{ check='cws-v2-publish'; result='PASS'; version=$release.Version; itemIdHash=([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($extensionId))).Substring(0,12)); uploadState=$uploadState; submittedState=$revision.State; submittedVersion=$revision.Version; requestResult=(Get-PropertyValue $submitted 'state') } | ConvertTo-Json -Compress
