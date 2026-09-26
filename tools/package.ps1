<#
 @file    : package.ps1
 @author  : rudals252
 @brief   : 단일 파일 publish 후 포터블 zip을 dist/에 만든다(최상위 항목 최소화, 실행 파일 하나)
#>
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"

# ---- 상수 ----
$RUNTIME = "win-x64"
$EXE_NAME = "PcOptimizer.exe"
$README_NAME = "실행방법.txt"
$LICENSES_DIR = "LICENSES"
$RULES_DIR = "rules"
# zip 최상위 항목 상한(실행 파일·안내문·LICENSES·rules와 여유분)
$MAX_TOP_LEVEL_ENTRIES = 6

# ---- 경로·버전 ----
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "src\PcOptimizer.App\PcOptimizer.App.csproj"
$version = ([xml](Get-Content $csproj -Raw -Encoding UTF8)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Version not found in $csproj" }
$distDir = Join-Path $root "dist"
$publishDir = Join-Path $distDir "publish"
$stage = Join-Path $distDir "PcOptimizer-v$version-$RUNTIME"
$zip = "$stage.zip"

# ---- publish(단일 파일, 런타임 포함) ----
foreach ($dir in @($publishDir, $stage)) { if (Test-Path $dir) { Remove-Item $dir -Recurse -Force } }
& "$env:ProgramFiles\dotnet\dotnet.exe" publish $csproj -c $Configuration -r $RUNTIME --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed: $LASTEXITCODE" }

# ---- 배포 폴더 구성: 실행 파일 하나 + 안내문 + 고지 + 출처 메타데이터 ----
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item (Join-Path $publishDir $EXE_NAME) $stage
Copy-Item (Join-Path $PSScriptRoot "README-in-zip.txt") (Join-Path $stage $README_NAME)
New-Item -ItemType Directory -Path (Join-Path $stage $LICENSES_DIR) | Out-Null
Copy-Item (Join-Path $root "rules\LICENSE-winapp2.md") (Join-Path $stage "$LICENSES_DIR\winapp2-CC-BY-SA-4.0.md")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $stage "$LICENSES_DIR\THIRD-PARTY-NOTICES.md")
New-Item -ItemType Directory -Path (Join-Path $stage $RULES_DIR) | Out-Null
Copy-Item (Join-Path $root "rules\sources.json") (Join-Path $stage "$RULES_DIR\sources.json")

$top = (Get-ChildItem $stage).Count
if ($top -gt $MAX_TOP_LEVEL_ENTRIES) { throw "zip top-level entries too many: $top" }

# ---- zip: 배포 폴더의 내용을 zip 루트에 둔다(항목 이름은 '/' 구분, UTF-8) ----
# Windows PowerShell 5.1의 Compress-Archive·ZipFile.CreateFromDirectory는 항목 경로를 '\'로 기록하므로(zip 규격 위반)
# 항목을 하나씩 만들며 이름을 직접 정한다.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $zip) { Remove-Item $zip -Force }
$zipStream = [System.IO.File]::Open($zip, [System.IO.FileMode]::CreateNew)
try {
    $archive = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create, $false, [System.Text.Encoding]::UTF8)
    try {
        $stageFull = (Resolve-Path $stage).Path.TrimEnd('\') + '\'
        foreach ($file in (Get-ChildItem $stage -Recurse -File | Sort-Object FullName)) {
            $entryName = $file.FullName.Substring($stageFull.Length).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $zipStream.Dispose()
}

$exeBytes = (Get-Item (Join-Path $stage $EXE_NAME)).Length
Write-Host "created $zip (top-level entries: $top, $EXE_NAME $exeBytes bytes)"
