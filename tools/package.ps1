<#
 @file    : package.ps1
 @author  : rudals252
 @brief   : 단일 파일 publish 후 포터블 zip을 dist/에 만든다(최상위 항목 최소화, 실행 파일 하나)
#>
param([string]$Configuration = "Release", [string]$OutputDirectory = "dist")

$ErrorActionPreference = "Stop"

# ---- 상수 ----
$RUNTIME = "win-x64"
$EXE_NAME = "PcOptimizer.exe"
$README_NAME = "실행방법.txt"
$LICENSES_DIR = "LICENSES"
$RULES_DIR = "rules"
# zip의 rules/에 동봉할 규칙 원문(LICENSE-winapp2.md에 적은 CC-BY-SA-4.0 배포 파일)
$RULE_SOURCE_FILES = @("winapp2.ini", "supplement.ini", "rule-metadata.json")
# zip 최상위 항목 상한(실행 파일·안내문·LICENSES·rules와 여유분)
$MAX_TOP_LEVEL_ENTRIES = 6

# ---- 경로·버전 ----
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "src\PcOptimizer.App\PcOptimizer.App.csproj"
$version = ([xml](Get-Content $csproj -Raw -Encoding UTF8)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Version not found in $csproj" }
$distDir = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$allowedDist = [IO.Path]::GetFullPath((Join-Path $root "dist"))
if ($distDir -ne $allowedDist -and -not $distDir.StartsWith($allowedDist + "\", [StringComparison]::OrdinalIgnoreCase)) { throw "Output must remain within dist" }
$publishDir = Join-Path $distDir "publish"
$stage = Join-Path $distDir "PcOptimizer-v$version-$RUNTIME"
$zip = "$stage.zip"

# ---- publish(단일 파일, 런타임 포함) ----
# 상위 출력 경로도 링크면 쓰지 않는다.
for ($ancestor = $distDir; $ancestor; $ancestor = Split-Path -Parent $ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked output ancestor" }
}
foreach ($dir in @($publishDir, $stage)) {
    $resolved = [IO.Path]::GetFullPath($dir)
    if (-not $resolved.StartsWith($distDir + "\", [StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe build directory" }
    if (Test-Path -LiteralPath $resolved) {
        if ((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked build directory" }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
& "$env:ProgramFiles\dotnet\dotnet.exe" restore $csproj -p:PublishProfile=win-x64 --locked-mode
if ($LASTEXITCODE -ne 0) { throw "locked restore failed: $LASTEXITCODE" }
& "$env:ProgramFiles\dotnet\dotnet.exe" publish $csproj -c $Configuration -p:PublishProfile=win-x64 --no-restore -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed: $LASTEXITCODE" }

# ---- 배포 폴더 구성: 실행 파일 하나 + 안내문 + 고지 + 출처 메타데이터 ----
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item (Join-Path $publishDir $EXE_NAME) $stage
Copy-Item (Join-Path $PSScriptRoot "README-in-zip.txt") (Join-Path $stage $README_NAME)
New-Item -ItemType Directory -Path (Join-Path $stage $LICENSES_DIR) | Out-Null
Copy-Item (Join-Path $root "rules\LICENSE-winapp2.md") (Join-Path $stage "$LICENSES_DIR\winapp2-CC-BY-SA-4.0.md")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $stage "$LICENSES_DIR\THIRD-PARTY-NOTICES.md")
# 복원된 버전의 원문 고지/라이선스만 동봉한다(최신 버전 탐색/다운로드 없음).
$assets = Get-Content (Join-Path (Split-Path $csproj) "obj\project.assets.json") -Raw | ConvertFrom-Json
$packageRoots = @($assets.packageFolders.PSObject.Properties.Name)
function Copy-PackageNotice([string]$relative, [string]$name) {
    $source = $packageRoots | ForEach-Object { Join-Path $_ $relative } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $source) { throw "Missing notice: $relative" }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stage "$LICENSES_DIR\$name")
}
$framework = $assets.project.frameworks.PSObject.Properties | Select-Object -First 1
foreach ($dependency in $framework.Value.downloadDependencies) {
    $id = $dependency.name.ToLowerInvariant()
    if ($id -notin @("microsoft.netcore.app.runtime.win-x64", "microsoft.windowsdesktop.app.runtime.win-x64")) { continue }
    $runtimeVersion = $dependency.version.Trim('[',']').Split(',')[0].Trim()
    $prefix = "$id/$runtimeVersion"
    if ($id -eq "microsoft.netcore.app.runtime.win-x64") {
        Copy-PackageNotice "$prefix/THIRD-PARTY-NOTICES.TXT" "dotnet-THIRD-PARTY-NOTICES.txt"
        Copy-PackageNotice "$prefix/LICENSE.TXT" "dotnet-LICENSE.txt"
    } else { Copy-PackageNotice "$prefix/LICENSE" "windowsdesktop-LICENSE.txt" }
}
$wpf = $assets.libraries.PSObject.Properties.Name | Where-Object { $_ -like 'WPF-UI/*' } | Select-Object -First 1
if (-not $wpf) { throw "WPF-UI dependency not found" }
Copy-PackageNotice "$($wpf.ToLowerInvariant())/ThirdPartyNotices.txt" "WPF-UI-ThirdPartyNotices.txt"
Copy-PackageNotice "$($wpf.ToLowerInvariant())/LICENSE.md" "WPF-UI-LICENSE.md"
New-Item -ItemType Directory -Path (Join-Path $stage $RULES_DIR) | Out-Null
Copy-Item (Join-Path $root "rules\sources.json") (Join-Path $stage "$RULES_DIR\sources.json")
# CC-BY-SA-4.0 규칙 원문을 함께 배포한다(앱은 실행 중 이 사본을 읽지 않고 실행 파일에 포함된 같은 내용을 사용)
foreach ($ruleFile in $RULE_SOURCE_FILES) { Copy-Item (Join-Path $root "rules\$ruleFile") (Join-Path $stage "$RULES_DIR\$ruleFile") }

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

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $([IO.Path]::GetFileName($zip))" -Encoding ascii
Write-Host "SHA256 $hash"
& (Join-Path $PSScriptRoot 'verify-package.ps1') -ZipPath $zip
