#requires -Version 7.0
# Source-owned release preparation. Never uploads or changes Git state.
$ErrorActionPreference = 'Stop'
$outputRoot = Join-Path $PSScriptRoot 'Outputs'
New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$log = Join-Path $outputRoot ((Get-Date -Format 'yyyyMMdd-HHmmssfff') + '-Package-Windows.log')
New-Item -ItemType File -Path $log -ErrorAction Stop | Out-Null
$packageExit = 1
try {
    $sourceRoot = Split-Path -Parent $PSScriptRoot
    Push-Location -LiteralPath $sourceRoot
    try {
        & (Join-Path $PSScriptRoot 'Internal/AssertSdkPolicy.ps1') 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE -ne 0) { throw 'SDK policy check failed.' }
        $version = '0.3.1'
        $name = "LazyChromeWindowBridge-v$version-win-x64"
        $artifactRoot = Join-Path $sourceRoot "artifacts/release/$version"
        $work = Join-Path $artifactRoot ('build-' + [Guid]::NewGuid().ToString('N'))
        $publish = Join-Path $work 'publish'
        $bundle = Join-Path $work $name
        New-Item -ItemType Directory -Force -Path $publish,(Join-Path $bundle 'SampleCaller'),(Join-Path $bundle 'Extension') | Out-Null
        & dotnet publish ./samples/LazyChromeWindowBridge.SampleCaller/LazyChromeWindowBridge.SampleCaller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishAot=false -p:ContinuousIntegrationBuild=true -warnaserror -o $publish 2>&1 | Tee-Object -FilePath $log -Append
        if ($LASTEXITCODE) { throw 'Windows publish failed.' }
        foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse) {
            if ($file.Extension -eq '.pdb') { continue }
            $relative = [IO.Path]::GetRelativePath($publish, $file.FullName)
            $target = Join-Path (Join-Path $bundle 'SampleCaller') $relative
            New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
        }
        & (Join-Path $PSScriptRoot 'Internal/PackageExtension.ps1') -SourceRoot $sourceRoot 2>&1 | Tee-Object -FilePath $log -Append
        $extensionZip = Join-Path $sourceRoot "artifacts/cws/LazyChromeWindowBridge.Extension-$version-cws.zip"
        [IO.Compression.ZipFile]::ExtractToDirectory($extensionZip, (Join-Path $bundle 'Extension'))
        Copy-Item -LiteralPath (Join-Path $sourceRoot 'README.md'),(Join-Path $sourceRoot 'LICENSE') -Destination $bundle
        $exe = Join-Path $bundle 'SampleCaller/LazyChromeWindowBridge.SampleCaller.exe'
        $core = Join-Path $bundle 'SampleCaller/LazyChromeWindowBridge.Core.dll'
        if (-not (Test-Path $exe) -or -not (Test-Path $core)) { throw 'SampleCaller/Core payload missing.' }
        $runtime = Get-Content (Join-Path $bundle 'SampleCaller/LazyChromeWindowBridge.SampleCaller.runtimeconfig.json') -Raw | ConvertFrom-Json
        if ($runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks -or
            @($runtime.runtimeOptions.includedFrameworks).Count -ne 3) { throw 'Expected self-contained three-framework runtime.' }
        foreach ($runtimeFile in @('coreclr.dll','hostfxr.dll','hostpolicy.dll')) {
            if (-not (Test-Path (Join-Path $bundle "SampleCaller/$runtimeFile"))) { throw 'Runtime payload missing.' }
        }
        $files = @(Get-ChildItem -LiteralPath $bundle -Recurse -File | Sort-Object FullName)
        $inventory = @($files | ForEach-Object { [IO.Path]::GetRelativePath($bundle, $_.FullName).Replace('\','/') })
        foreach ($relative in $inventory) {
            if ($relative -match '(?i)(^|/)(obj|bin|logs|TEMP|profiles|evidence|\.git|artifacts|tests)(/|$)|\.(pdb|map|log|tmp|key|pem)$|(^|/)(AGENTS|Exchange-Protocol|CHANGELOG)\.md$' -or
                $relative -notmatch '^(SampleCaller/|Extension/|README\.md$|LICENSE$)') { throw "Unexpected bundle member: $relative" }
        }
        $archivePath = Join-Path $artifactRoot "$name.zip"
        $stream = [IO.File]::Create($archivePath)
        $archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$true)
        try {
            foreach ($file in $files) {
                $relative = [IO.Path]::GetRelativePath($bundle,$file.FullName).Replace('\','/')
                $entry = $archive.CreateEntry("$name/$relative",[IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(1980,1,1,0,0,0,[TimeSpan]::Zero)
                $entry.ExternalAttributes = 0
                $inputStream = $file.OpenRead(); $entryStream = $entry.Open()
                try { $inputStream.CopyTo($entryStream) } finally { $entryStream.Dispose(); $inputStream.Dispose() }
            }
        } finally { $archive.Dispose(); $stream.Dispose() }
        $fresh = Join-Path $artifactRoot ('extracted-' + [Guid]::NewGuid().ToString('N'))
        [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $fresh)
        foreach ($file in $files) {
            $relative = [IO.Path]::GetRelativePath($bundle,$file.FullName)
            if ((Get-FileHash -LiteralPath $file.FullName).Hash -cne (Get-FileHash -LiteralPath (Join-Path (Join-Path $fresh $name) $relative)).Hash) { throw 'Extracted payload differs.' }
        }
        $record = [ordered]@{check='windows-package';result='PASS';version=$version;path=[IO.Path]::GetRelativePath($sourceRoot,$archivePath);bytes=(Get-Item $archivePath).Length;sha256=(Get-FileHash $archivePath).Hash;entries=$inventory;freshExecutable=[IO.Path]::GetRelativePath($sourceRoot,(Join-Path $fresh "$name/SampleCaller/LazyChromeWindowBridge.SampleCaller.exe"));launchSmoke='Required separately through the real GUI fixture';frameworks=$runtime.runtimeOptions.includedFrameworks}
        $record | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $artifactRoot 'inventory.json') -Encoding utf8
        $record | ConvertTo-Json -Depth 6 -Compress | Tee-Object -FilePath $log -Append
        $packageExit = 0
    } finally { Pop-Location }
} catch { $_ | Out-String | Add-Content -LiteralPath $log; Write-Host $_ }
finally { Add-Content -LiteralPath $log -Value "Exit: $packageExit"; Write-Host ('Output: Scripts/Outputs/' + [IO.Path]::GetFileName($log)) }
exit $packageExit
