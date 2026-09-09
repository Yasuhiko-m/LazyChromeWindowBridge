#requires -Version 7.0
# Source-owned helper. Test-NuGet owns the invocation log; no network publish path.
param([Parameter(Mandatory)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$packageRoot = Join-Path $SourceRoot 'artifacts/nuget'
$packageId = 'LazyChromeWindowBridge.Core'
$version = '0.1.0'
$head = git -C $SourceRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine repository commit.' }
$expectedNames = @("$packageId.$version.nupkg", "$packageId.$version.snupkg")
$actualNames = @(Get-ChildItem -LiteralPath $packageRoot -File | Where-Object Extension -in @('.nupkg', '.snupkg') | Select-Object -ExpandProperty Name)
if ($actualNames.Count -ne 2 -or (Compare-Object $expectedNames $actualNames)) { throw 'Unexpected package output set.' }
$tfm = 'net10.0-windows7.0'
$dllHash = $null
foreach ($extension in @('nupkg', 'snupkg')) {
    $package = Join-Path $packageRoot "$packageId.$version.$extension"
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $nuspecEntry = $archive.GetEntry("$packageId.nuspec")
        if ($null -eq $nuspecEntry) { throw 'Nuspec missing.' }
        $reader = [IO.StreamReader]::new($nuspecEntry.Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        if ($metadata.id -cne $packageId -or $metadata.version -cne $version) { throw 'PackageId/version mismatch.' }
        if ($metadata.repository.type -ne 'git' -or $metadata.repository.commit -cne $head -or
            $metadata.repository.url -cne 'https://github.com/Yasuhiko-m/LazyChromeWindowBridge') { throw 'Package repository provenance mismatch.' }
        $payload = if ($extension -eq 'nupkg') { "lib/$tfm/$packageId.dll" } else { "lib/$tfm/$packageId.pdb" }
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName -notin @($payload, "$packageId.nuspec", '_rels/.rels', '[Content_Types].xml') -and
                $entry.FullName -notmatch '^package/services/metadata/core-properties/(?:[a-f0-9]+|nuget)\.psmdcp$' -and
                -not ($extension -eq 'nupkg' -and $entry.FullName -in @('README.md','icon-128.png'))) { throw "Unexpected package member: $($entry.FullName)" }
        }
        $payloadEntry = $archive.GetEntry($payload)
        if ($null -eq $payloadEntry) { throw 'Core payload missing.' }
        $payloadStream = $payloadEntry.Open()
        $memory = [IO.MemoryStream]::new()
        try { $payloadStream.CopyTo($memory); $bytes = $memory.ToArray() } finally { $memory.Dispose(); $payloadStream.Dispose() }
        $payloadHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        $buildExtension = if ($extension -eq 'nupkg') { 'dll' } else { 'pdb' }
        $buildFile = Join-Path $SourceRoot "src/$packageId/bin/Release/net10.0-windows/$packageId.$buildExtension"
        if ($payloadHash -ne (Get-FileHash -LiteralPath $buildFile).Hash) { throw 'Packed payload differs from the tested Release build.' }
        if ($extension -eq 'nupkg') {
            if ($metadata.title -cne $packageId -or $metadata.projectUrl -cne $metadata.repository.url -or
                $metadata.tags -cne 'chrome windows win32 browser-integration window-management multi-monitor' -or
                $metadata.icon -cne 'icon-128.png') { throw 'Title/project URL/tags/icon metadata mismatch.' }
            $iconEntry = $archive.GetEntry('icon-128.png')
            if ($null -eq $iconEntry) { throw 'Package icon missing.' }
            $iconStream = $iconEntry.Open()
            try { $iconHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($iconStream)) } finally { $iconStream.Dispose() }
            if ($iconHash -cne (Get-FileHash -LiteralPath (Join-Path $SourceRoot 'src/LazyChromeWindowBridge.Extension/icons/icon-128.png')).Hash) { throw 'Package icon differs from reviewed extension icon.' }
            $dllHash = $payloadHash
            if ($metadata.license.type -ne 'expression' -or $metadata.license.InnerText -ne 'MIT' -or $metadata.readme -ne 'README.md') { throw 'License/readme metadata mismatch.' }
            if ($null -eq $archive.GetEntry('README.md')) { throw 'Package README missing.' }
            $frameworks = @($metadata.frameworkReferences.group.frameworkReference.name)
            if ($frameworks.Count -ne 2 -or (Compare-Object @('Microsoft.AspNetCore.App','Microsoft.WindowsDesktop.App') $frameworks)) { throw 'Consumer framework references missing or unexpected.' }
            if (@($metadata.dependencies.group.dependency | Where-Object { $null -ne $_ }).Count -ne 0) { throw 'Unexpected external NuGet dependency.' }
        } else {
            if ($metadata.packageTypes.packageType.name -ne 'SymbolsPackage' -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne 'BSJB') { throw 'Expected portable PDB symbol package.' }
            $pdbText = [Text.Encoding]::UTF8.GetString($bytes)
            if (-not $pdbText.Contains('raw.githubusercontent.com') -or -not $pdbText.Contains($head)) { throw 'Source Link commit mapping missing.' }
            if ($pdbText -match '[A-Z]:[\\/]Users[\\/]|[A-Z]:[\\/]LazyAIDeckProjects[\\/]') { throw 'Physical source path leaked into portable symbols.' }
        }
        [pscustomobject]@{ Check='nuget-package'; Result='PASS'; File=[IO.Path]::GetFileName($package); Bytes=(Get-Item -LiteralPath $package).Length; SHA256=(Get-FileHash -LiteralPath $package).Hash; Commit=$head; Entries=@($archive.Entries.FullName) } | ConvertTo-Json -Depth 3 -Compress
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
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="LazyChromeWindowBridge.Core" Version="[0.1.0]" />
  </ItemGroup>
</Project>
'@
[IO.File]::WriteAllText($consumerProject, $projectXml, [Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $SourceRoot 'tests/LazyChromeWindowBridge.PublicApi.Tests/Program.cs') -Destination (Join-Path $consumerRoot 'Program.cs')
& dotnet restore $consumerProject --source $packageRoot --packages (Join-Path $consumerRoot 'packages') -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Local nupkg consumer restore failed.' }
$assets = Get-Content -LiteralPath (Join-Path $consumerRoot 'obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
if ($assets.libraries["$packageId/$version"].type -ne 'package' -or $assets.libraries.Count -ne 1) { throw 'Consumer did not resolve only the intended local package.' }
& dotnet build $consumerProject -c Release --no-restore -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Local nupkg consumer build failed.' }
$consumerOutput = Join-Path $consumerRoot 'bin/Release/net10.0-windows'
if ((Get-FileHash -LiteralPath (Join-Path $consumerOutput "$packageId.dll")).Hash -ne $dllHash) { throw 'Consumer loaded another Core binary.' }
& dotnet (Join-Path $consumerOutput 'NuGetConsumer.dll')
if ($LASTEXITCODE -ne 0) { throw 'Local nupkg consumer runtime checks failed.' }
'PASS: local-only nupkg consumer; 19 existing API checks, exact DLL and transitive shared frameworks.'
