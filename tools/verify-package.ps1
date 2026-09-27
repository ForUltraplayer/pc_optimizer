<#
 @file    : verify-package.ps1
 @author  : rudals252
 @brief   : 실제 ZIP의 경로·필수 파일·PE 헤더·규칙 및 라이선스 원문·SHA-256을 추출 없이 검증
#>
param([Parameter(Mandatory = $true)][string]$ZipPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo = Split-Path -Parent $PSScriptRoot
$zipFull = (Resolve-Path -LiteralPath $ZipPath).Path
$expectedHash = (Get-Content -LiteralPath "$zipFull.sha256" -Raw).Split(' ')[0].Trim()
if ($expectedHash -notmatch '^[A-Fa-f0-9]{64}$' -or (Get-FileHash -LiteralPath $zipFull -Algorithm SHA256).Hash -ne $expectedHash) { throw 'ZIP hash mismatch' }
$required = @('PcOptimizer.exe', '실행방법.txt', 'LICENSES/THIRD-PARTY-NOTICES.md', 'LICENSES/winapp2-CC-BY-SA-4.0.md',
    'LICENSES/dotnet-THIRD-PARTY-NOTICES.txt', 'LICENSES/dotnet-LICENSE.txt', 'LICENSES/windowsdesktop-LICENSE.txt',
    'LICENSES/WPF-UI-ThirdPartyNotices.txt', 'LICENSES/WPF-UI-LICENSE.md',
    'rules/sources.json', 'rules/winapp2.ini', 'rules/supplement.ini', 'rules/rule-metadata.json')
$sourceFiles = @{
    'rules/sources.json' = 'rules/sources.json'; 'rules/winapp2.ini' = 'rules/winapp2.ini'
    'rules/supplement.ini' = 'rules/supplement.ini'; 'rules/rule-metadata.json' = 'rules/rule-metadata.json'
    'LICENSES/THIRD-PARTY-NOTICES.md' = 'THIRD-PARTY-NOTICES.md'
    'LICENSES/winapp2-CC-BY-SA-4.0.md' = 'rules/LICENSE-winapp2.md'; '실행방법.txt' = 'tools/README-in-zip.txt'
}
$assets = Get-Content (Join-Path $repo 'src/PcOptimizer.App/obj/project.assets.json') -Raw | ConvertFrom-Json
function Add-NoticeSource([string]$relative, [string]$name) {
    $path = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ $relative } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $path) { throw "Notice source not found: $relative" }
    $sourceFiles["LICENSES/$name"] = $path
}
$framework = $assets.project.frameworks.PSObject.Properties | Select-Object -First 1
foreach ($dependency in $framework.Value.downloadDependencies) {
    $id = $dependency.name.ToLowerInvariant()
    $version = $dependency.version.Trim('[',']').Split(',')[0].Trim()
    if ($id -eq 'microsoft.netcore.app.runtime.win-x64') {
        Add-NoticeSource "$id/$version/THIRD-PARTY-NOTICES.TXT" 'dotnet-THIRD-PARTY-NOTICES.txt'
        Add-NoticeSource "$id/$version/LICENSE.TXT" 'dotnet-LICENSE.txt'
    } elseif ($id -eq 'microsoft.windowsdesktop.app.runtime.win-x64') {
        Add-NoticeSource "$id/$version/LICENSE" 'windowsdesktop-LICENSE.txt'
    }
}
$wpf = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'WPF-UI/*' } | Select-Object -First 1
if (-not $wpf) { throw 'WPF-UI source not found' }
Add-NoticeSource "$($wpf.ToLowerInvariant())/ThirdPartyNotices.txt" 'WPF-UI-ThirdPartyNotices.txt'
Add-NoticeSource "$($wpf.ToLowerInvariant())/LICENSE.md" 'WPF-UI-LICENSE.md'
if ($sourceFiles.Count -ne $required.Count - 1) { throw 'Missing license source mapping' }
$archive = [IO.Compression.ZipFile]::OpenRead($zipFull)
try {
    $names = @($archive.Entries.FullName)
    if ($names.Count -ne $required.Count -or @($names | Select-Object -Unique).Count -ne $names.Count) { throw 'Unexpected or duplicate ZIP entries' }
    foreach ($name in $required) { if ($names -cnotcontains $name) { throw "Missing ZIP entry: $name" } }
    foreach ($entry in $archive.Entries) {
        if ($entry.Length -le 0 -or $entry.FullName.Contains('\') -or $entry.FullName.Split('/') -contains '..') { throw 'Invalid ZIP entry' }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            if ($entry.FullName -eq 'PcOptimizer.exe') {
                if ($stream.ReadByte() -ne 0x4D -or $stream.ReadByte() -ne 0x5A) { throw 'Executable PE header missing' }
            }
            # 전체 압축 스트림을 읽는다. EXE는 헤더 다음부터 읽고 원문 비교 대상에는 포함하지 않는다.
            $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
            if ($sourceFiles.ContainsKey($entry.FullName)) {
                $source = $sourceFiles[$entry.FullName]
                if (-not [IO.Path]::IsPathRooted($source)) { $source = Join-Path $repo $source }
                if ($hash -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw "Source mismatch: $($entry.FullName)" }
            }
        } finally { $sha.Dispose(); $stream.Dispose() }
    }
    Write-Host "Verified ZIP: $($names.Count) files, required notices/rules, source hashes and archive SHA-256."
} finally { $archive.Dispose() }
