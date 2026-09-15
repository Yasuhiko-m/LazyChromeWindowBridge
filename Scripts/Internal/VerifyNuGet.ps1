#requires -Version 7.0
# Source-owned helper. Test-NuGet owns the invocation log; no network publish path.
param([Parameter(Mandatory)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$packageRoot = Join-Path $SourceRoot 'artifacts/nuget/0.3.0'
$packageId = 'LazyChromeWindowBridge.Core'
$version = '0.3.0'
$head = git -C $SourceRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine repository commit.' }
$expectedNames = @("$packageId.$version.nupkg", "$packageId.$version.snupkg")
$actualNames = @(Get-ChildItem -LiteralPath $packageRoot -File | Where-Object Extension -in @('.nupkg', '.snupkg') | Select-Object -ExpandProperty Name)
if ($actualNames.Count -ne 2 -or (Compare-Object $expectedNames $actualNames)) { throw 'Unexpected package output set.' }
$tfms = @('net10.0-windows7.0', 'net10.0-windows10.0.18362')
$dllHashes = @{}
foreach ($extension in @('nupkg', 'snupkg')) {
    $package = Join-Path $packageRoot "$packageId.$version.$extension"
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    $packageReadmeDistribution = $null
    $chromeExtensionPayload = $null
    try {
        $nuspecEntry = $archive.GetEntry("$packageId.nuspec")
        if ($null -eq $nuspecEntry) { throw 'Nuspec missing.' }
        $reader = [IO.StreamReader]::new($nuspecEntry.Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        if ($metadata.id -cne $packageId -or $metadata.version -cne $version) { throw 'PackageId/version mismatch.' }
        $sourceProject = [xml](Get-Content -LiteralPath (Join-Path $SourceRoot 'src/LazyChromeWindowBridge.Core/LazyChromeWindowBridge.Core.csproj') -Raw)
        # The SDK omits authors/license/readme from SymbolsPackage metadata.
        if ($extension -eq 'nupkg' -and $metadata.authors -cne $sourceProject.Project.PropertyGroup.Authors) { throw 'Author mismatch.' }
        if ($metadata.repository.type -ne 'git' -or $metadata.repository.commit -cne $head -or
            $metadata.repository.url -cne 'https://github.com/Yasuhiko-m/LazyChromeWindowBridge') { throw 'Package repository provenance mismatch.' }
        $payloads = @($tfms | ForEach-Object {
            if ($extension -eq 'nupkg') { "lib/$_/$packageId.dll" } else { "lib/$_/$packageId.pdb" }
        })
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -notin @($payloads + @("$packageId.nuspec", '_rels/.rels', '[Content_Types].xml')) -and
                $entry.FullName -notmatch '^package/services/metadata/core-properties/(?:[a-f0-9]+|nuget)\.psmdcp$' -and
                -not ($extension -eq 'nupkg' -and $entry.FullName -in @('README.md','icon-128.png'))) { throw "Unexpected package member: $($entry.FullName)" }
        }
        foreach ($tfm in $tfms) {
            $payload = if ($extension -eq 'nupkg') { "lib/$tfm/$packageId.dll" } else { "lib/$tfm/$packageId.pdb" }
            $payloadEntry = $archive.GetEntry($payload)
            if ($null -eq $payloadEntry) { throw "Core payload missing for $tfm." }
            $payloadStream = $payloadEntry.Open()
            $memory = [IO.MemoryStream]::new()
            try { $payloadStream.CopyTo($memory); $bytes = $memory.ToArray() } finally { $memory.Dispose(); $payloadStream.Dispose() }
            $payloadHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
            $buildExtension = if ($extension -eq 'nupkg') { 'dll' } else { 'pdb' }
            $buildTfm = if ($tfm -eq 'net10.0-windows7.0') { 'net10.0-windows' }
                elseif ($tfm -eq 'net10.0-windows10.0.18362') { 'net10.0-windows10.0.18362.0' }
                else { $tfm }
            $buildFile = Join-Path $SourceRoot "src/$packageId/bin/Release/$buildTfm/$packageId.$buildExtension"
            if ($payloadHash -ne (Get-FileHash -LiteralPath $buildFile).Hash) { throw "Packed $tfm payload differs from the tested Release build." }
            if ($extension -eq 'nupkg') {
                $dllHashes[$tfm] = $payloadHash
            } else {
                if ([Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'BSJB') { throw "Expected portable $tfm PDB symbol package." }
                $pdbText = [Text.Encoding]::UTF8.GetString($bytes)
                if (-not $pdbText.Contains('raw.githubusercontent.com') -or -not $pdbText.Contains($head)) { throw "Source Link commit mapping missing for $tfm." }
                if ($pdbText -match '[A-Z]:[\\/]Users[\\/]|[A-Z]:[\\/]LazyAIDeckProjects[\\/]') { throw "Physical source path leaked into $tfm portable symbols." }
            }
        }
        if ($extension -eq 'nupkg') {
            if ($metadata.title -cne $packageId -or $metadata.projectUrl -cne $metadata.repository.url -or
                $metadata.tags -cne 'chrome windows win32 browser-integration window-management multi-monitor' -or
                $metadata.icon -cne 'icon-128.png') { throw 'Title/project URL/tags/icon metadata mismatch.' }
            $iconEntry = $archive.GetEntry('icon-128.png')
            if ($null -eq $iconEntry) { throw 'Package icon missing.' }
            $iconStream = $iconEntry.Open()
            try { $iconHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($iconStream)) } finally { $iconStream.Dispose() }
            if ($iconHash -cne (Get-FileHash -LiteralPath (Join-Path $SourceRoot 'src/LazyChromeWindowBridge.Extension/icons/icon-128.png')).Hash) { throw 'Package icon differs from reviewed extension icon.' }
            if ($metadata.license.type -ne 'expression' -or $metadata.license.InnerText -ne 'MIT' -or $metadata.readme -ne 'README.md') { throw 'License/readme metadata mismatch.' }
            $readmeEntry = $archive.GetEntry('README.md')
            if ($null -eq $readmeEntry) { throw 'Package README missing.' }
            $readmeStream = $readmeEntry.Open()
            try {
                $readmeBytes = [IO.MemoryStream]::new()
                try {
                    $readmeStream.CopyTo($readmeBytes)
                    $readmePayload = $readmeBytes.ToArray()
                    $readmeText = [Text.Encoding]::UTF8.GetString($readmePayload)
                } finally { $readmeBytes.Dispose() }
                $readmeHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($readmePayload))
            } finally { $readmeStream.Dispose() }
            if ($readmeHash -cne (Get-FileHash -LiteralPath (Join-Path $SourceRoot 'docs/nuget.md')).Hash) { throw 'Package README differs from Source.' }
            foreach ($requiredReadmeText in @(
                'NuGet contains **`LazyChromeWindowBridge.Core` only**.',
                'matching Chrome extension',
                'Chrome Web Store',
                'Store review',
                'GitHub Release',
                '`v0.3.0`',
                'chrome://extensions',
                'Developer mode',
                'Load unpacked')) {
                if (-not $readmeText.Contains($requiredReadmeText)) { throw "Package README is missing required distribution guidance: $requiredReadmeText" }
            }
            $packageReadmeDistribution = 'PASS'
            $chromeExtensionPayload = 'absent'
            $frameworkGroups = @($metadata.frameworkReferences.group)
            $dependencyGroups = @($metadata.dependencies.group)
            if ((Compare-Object $tfms @($frameworkGroups.targetFramework)) -or
                (Compare-Object $tfms @($dependencyGroups.targetFramework))) { throw 'Target framework metadata mismatch.' }
            foreach ($group in $frameworkGroups) {
                $frameworks = @($group.frameworkReference.name)
                if ($frameworks.Count -ne 2 -or (Compare-Object @('Microsoft.AspNetCore.App','Microsoft.WindowsDesktop.App') $frameworks)) { throw "Consumer framework references missing or unexpected for $($group.targetFramework)." }
            }
            if (@($metadata.dependencies.group.dependency | Where-Object { $null -ne $_ }).Count -ne 0) { throw 'Unexpected external NuGet dependency.' }
        } else {
            if ($metadata.packageTypes.packageType.name -ne 'SymbolsPackage') { throw 'Expected SymbolsPackage metadata.' }
        }
        [pscustomobject]@{ Check='nuget-package'; Result='PASS'; File=[IO.Path]::GetFileName($package); Bytes=(Get-Item -LiteralPath $package).Length; SHA256=(Get-FileHash -LiteralPath $package).Hash; Commit=$head; Entries=@($archive.Entries.FullName); PackageReadmeDistribution=$packageReadmeDistribution; ChromeExtensionPayload=$chromeExtensionPayload } | ConvertTo-Json -Depth 3 -Compress
    } finally { $archive.Dispose() }
}

# Reuse the actual public API contract checks, compiled with PackageReference only.
# An empty private cache and local-only restore cannot select a project or published copy.
$consumerRoot = Join-Path $packageRoot ('consumer-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $consumerRoot | Out-Null
$consumerProject = Join-Path $consumerRoot 'NuGetConsumer.csproj'
$projectXml = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows10.0.18362.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LazyChromeWindowBridge.Core" Version="[0.3.0]" />
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText($consumerProject, $projectXml, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $SourceRoot 'tests/LazyChromeWindowBridge.PublicApi.Tests/Program.cs') -Destination (Join-Path $consumerRoot 'Program.cs')
$windowsSdkCache = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.nuget/packages/microsoft.windows.sdk.net.ref'
$windowsSdkPackage = Get-ChildItem -LiteralPath $windowsSdkCache -Directory -ErrorAction Stop |
    Where-Object Name -Like '10.0.18362.*' | Sort-Object Name -Descending |
    ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Filter 'microsoft.windows.sdk.net.ref.*.nupkg' -File } |
    Select-Object -First 1
if ($null -eq $windowsSdkPackage) { throw 'Offline Microsoft.Windows.SDK.NET.Ref 10.0.18362 package is unavailable after the tested Source restore.' }
& dotnet restore $consumerProject --source $packageRoot --source $windowsSdkPackage.DirectoryName --packages (Join-Path $consumerRoot 'packages') -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Local nupkg consumer restore failed.' }
$assets = Get-Content -LiteralPath (Join-Path $consumerRoot 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
if ($assets.libraries["$packageId/$version"].type -ne 'package') { throw 'Consumer did not resolve the intended local product package.' }
$unexpectedLibraries = @($assets.libraries.Keys | Where-Object { $_ -ne "$packageId/$version" -and $_ -notmatch '^Microsoft\.Windows\.SDK\.NET\.Ref/10\.0\.18362\.' })
$windowsSdkLibraries = @($assets.libraries.Keys | Where-Object { $_ -match '^Microsoft\.Windows\.SDK\.NET\.Ref/10\.0\.18362\.' })
if ($unexpectedLibraries.Count -ne 0 -or $windowsSdkLibraries.Count -gt 1) {
    throw 'Consumer resolved an unexpected package beyond the product and required Microsoft Windows SDK reference pack.'
}
& dotnet build $consumerProject -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Local nupkg consumer build failed.' }
$consumerOutput = Join-Path $consumerRoot 'bin/Release/net10.0-windows10.0.18362.0'
if ((Get-FileHash -LiteralPath (Join-Path $consumerOutput "$packageId.dll")).Hash -ne $dllHashes['net10.0-windows10.0.18362']) { throw 'Consumer loaded another Core binary.' }
& dotnet (Join-Path $consumerOutput 'NuGetConsumer.dll')
if ($LASTEXITCODE -ne 0) { throw 'Local nupkg consumer runtime checks failed.' }
'PASS: local-only 0.3.0 nupkg consumer; external API checks include both CaptureMode values and SetShowInTaskbar.'
