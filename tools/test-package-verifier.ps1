<#
 @file    : test-package-verifier.ps1
 @author  : rudals252
 @brief   : 배포 ZIP 복사본에 누락·변조·중복·해시 불일치를 만들어 검증기의 실제 거절을 확인
#>
param([Parameter(Mandatory = $true)][string]$ZipPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$original = (Resolve-Path -LiteralPath $ZipPath).Path
$fixture = Join-Path ([IO.Path]::GetTempPath()) ("PcOptimizer-package-" + [Guid]::NewGuid().ToString('N') + '.zip')
$verify = Join-Path $PSScriptRoot 'verify-package.ps1'
try {
    & $verify -ZipPath $original
    foreach ($case in @('Hash', 'MissingNotice', 'ChangedRule', 'Duplicate')) {
        Copy-Item -LiteralPath $original -Destination $fixture -Force
        if ($case -ne 'Hash') {
            $archive = [IO.Compression.ZipFile]::Open($fixture, [IO.Compression.ZipArchiveMode]::Update)
            try {
                switch ($case) {
                    'MissingNotice' { $archive.GetEntry('LICENSES/dotnet-LICENSE.txt').Delete() }
                    'ChangedRule' {
                        $archive.GetEntry('rules/supplement.ini').Delete()
                        $stream = $archive.CreateEntry('rules/supplement.ini').Open()
                        try { $stream.WriteByte(42) } finally { $stream.Dispose() }
                    }
                    'Duplicate' { $null = $archive.CreateEntry('rules/supplement.ini') }
                }
            } finally { $archive.Dispose() }
        }
        $digest = if ($case -eq 'Hash') { '0' * 64 } else { (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash }
        Set-Content -LiteralPath "$fixture.sha256" -Value "$digest  fixture.zip" -Encoding ascii
        $expected = switch ($case) { 'Hash' { 'ZIP hash mismatch' }; 'ChangedRule' { 'Source mismatch' }; default { 'Unexpected or duplicate ZIP entries' } }
        $rejected = $false
        try { & $verify -ZipPath $fixture } catch {
            if (-not $_.Exception.Message.Contains($expected)) { throw }
            $rejected = $true
        }
        if (-not $rejected) { throw "Verifier accepted invalid package: $case" }
        Write-Host "Rejected $case as expected."
    }
} finally {
    # 이 실행에서 정한 두 임시 파일만 지운다. 경로 열거·재귀 삭제는 하지 않는다.
    foreach ($file in @($fixture, "$fixture.sha256")) { if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force } }
}
