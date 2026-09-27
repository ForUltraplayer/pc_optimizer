<#
 @file    : check-links.ps1
 @author  : rudals252
 @brief   : rules/vendor-links.json의 모든 공식 링크를 실제로 요청해 상태 코드·최종 주소(리디렉션)를 표로 출력한다(배포 전 점검용, 파일을 받지 않음)
#>
param([string]$LinksPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "rules\vendor-links.json"), [int]$TimeoutSeconds = 20)
$ErrorActionPreference = "Continue"
$USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PcOptimizer-link-check"
$json = Get-Content $LinksPath -Raw -Encoding utf8 | ConvertFrom-Json
$rows = foreach ($entry in $json.entries) {
    try {
        $response = Invoke-WebRequest -Uri $entry.url -Method Get -MaximumRedirection 5 -TimeoutSec $TimeoutSeconds -UseBasicParsing -UserAgent $USER_AGENT
        $final = $response.BaseResponse.ResponseUri; if (-not $final) { $final = $response.BaseResponse.RequestMessage.RequestUri }
        [pscustomobject]@{ Id = $entry.id; Kind = $entry.kind; Status = [int]$response.StatusCode; Redirected = ($final.AbsoluteUri -ne $entry.url); Final = $final.AbsoluteUri }
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        # 403은 봇 차단인 경우가 많아(예: hwinfo.com) 브라우저에서 따로 확인한다.
        [pscustomobject]@{ Id = $entry.id; Kind = $entry.kind; Status = $(if ($code) { $code } else { "ERR" }); Redirected = $false; Final = ($_.Exception.Message -split "`n")[0] }
    }
}
$rows | Format-Table -AutoSize | Out-String -Width 220
$bad = @($rows | Where-Object { $_.Status -ne 200 })
$moved = @($rows | Where-Object { $_.Redirected })
Write-Host ("총 {0}개 · 200 아님 {1}개 · 리디렉션 {2}개(저장 주소를 최종 주소로 바꾸는 것을 권장)" -f $rows.Count, $bad.Count, $moved.Count)
if ($bad.Count -gt 0) { exit 1 }
