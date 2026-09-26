<#
 @file    : make-icon.ps1
 @author  : rudals252
 @brief   : 외부 자산 없이 System.Drawing으로 앱 아이콘(파란 둥근 사각형 + 흰 체크 + 작은 렌치)을 그려 PNG 프레임 ICO로 저장한다(결정적, 재실행 가능)
#>
param(
    # 저장할 ICO 경로(상대 경로는 저장소 루트 기준)
    [string]$Output = "src\PcOptimizer.App\Assets\app.ico"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

# ---- 상수 ----
# 그리기 좌표계: 모든 도형은 256x256 기준 좌표로 정의하고 프레임 크기에 맞게 축소한다
$DESIGN_SIZE = 256.0
# ICO에 담을 프레임 크기(작은 것부터)
$FRAME_SIZES = @(16, 24, 32, 48, 64, 128, 256)
# 이 크기 미만에서는 렌치를 생략한다(너무 작아 뭉개짐)
$WRENCH_MIN_SIZE = 32
# 배경 둥근 사각형: 가장자리 여백, 모서리 반지름
$BG_INSET = 8.0
$BG_RADIUS = 52.0
$BG_COLOR = "#2563EB"
# 체크 표시: 세 꼭짓점과 선 두께
$CHECK_POINTS = @(@(58.0, 122.0), @(104.0, 168.0), @(190.0, 76.0))
$CHECK_WIDTH = 30.0
$CHECK_COLOR = "#FFFFFF"
# 렌치: 머리 중심, 머리 반지름, 손잡이 길이·반폭, 입 벌림 반폭·깊이, 회전 각도(도)
$WRENCH_CENTER = @(194.0, 194.0)
$WRENCH_HEAD_RADIUS = 24.0
$WRENCH_HANDLE_LENGTH = 50.0
$WRENCH_HANDLE_HALF = 9.0
$WRENCH_JAW_HALF = 9.0
$WRENCH_JAW_DEPTH = 16.0
$WRENCH_ANGLE = 225.0
$WRENCH_COLOR = "#BFDBFE"
# ICO 형식 값
$ICO_HEADER_SIZE = 6
$ICO_ENTRY_SIZE = 16
$ICO_TYPE_ICON = 1
$ICO_PLANES = 1
$ICO_BIT_COUNT = 32
# 256px 프레임은 폭·높이 바이트를 0으로 기록한다
$ICO_LARGE_SIZE = 256

# ---- 함수 ----

# 둥근 사각형 경로를 만든다
function New-RoundedRectPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

# 256 기준 좌표계로 한 프레임을 그려 PNG 바이트로 돌려준다
function New-IconFrame([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $scale = [single]($size / $DESIGN_SIZE)
        $g.ScaleTransform($scale, $scale)

        $bgColor = [System.Drawing.ColorTranslator]::FromHtml($BG_COLOR)
        $bgBrush = New-Object System.Drawing.SolidBrush($bgColor)
        $bgSide = $DESIGN_SIZE - 2 * $BG_INSET
        $bgPath = New-RoundedRectPath $BG_INSET $BG_INSET $bgSide $bgSide $BG_RADIUS
        $g.FillPath($bgBrush, $bgPath)
        $bgPath.Dispose()

        if ($size -ge $WRENCH_MIN_SIZE) {
            $state = $g.Save()
            $g.TranslateTransform([single]$WRENCH_CENTER[0], [single]$WRENCH_CENTER[1])
            $g.RotateTransform([single]$WRENCH_ANGLE)
            $wrenchBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($WRENCH_COLOR))
            # 머리(원) + 손잡이(끝이 둥근 막대)
            $r = $WRENCH_HEAD_RADIUS
            $g.FillEllipse($wrenchBrush, [single](-$r), [single](-$r), [single](2 * $r), [single](2 * $r))
            $handle = New-RoundedRectPath 0 (-$WRENCH_HANDLE_HALF) ($WRENCH_HANDLE_LENGTH + $r) (2 * $WRENCH_HANDLE_HALF) $WRENCH_HANDLE_HALF
            $g.FillPath($wrenchBrush, $handle)
            $handle.Dispose()
            # 입 벌림: 손잡이 반대쪽 머리를 배경색으로 파낸다
            $g.FillRectangle($bgBrush, [single](-$r - 1), [single](-$WRENCH_JAW_HALF), [single]($WRENCH_JAW_DEPTH + 1), [single](2 * $WRENCH_JAW_HALF))
            $wrenchBrush.Dispose()
            $g.Restore($state)
        }
        $bgBrush.Dispose()

        $pen = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($CHECK_COLOR), [single]$CHECK_WIDTH)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $points = [System.Drawing.PointF[]]($CHECK_POINTS | ForEach-Object { New-Object System.Drawing.PointF([single]$_[0], [single]$_[1]) })
        $g.DrawLines($pen, $points)
        $pen.Dispose()
    }
    finally {
        $g.Dispose()
    }
    $stream = New-Object System.IO.MemoryStream
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return , $stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

# ---- 본문 ----
$root = Split-Path -Parent $PSScriptRoot
$target = if ([System.IO.Path]::IsPathRooted($Output)) { $Output } else { Join-Path $root $Output }
$targetDir = Split-Path -Parent $target
if (-not (Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir | Out-Null }

$frames = @()
foreach ($size in $FRAME_SIZES) { $frames += , (New-IconFrame $size) }

# ICO 컨테이너: 헤더(6바이트) + 항목(16바이트 x 개수) + PNG 데이터
$file = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]$ICO_TYPE_ICON)
    $writer.Write([uint16]$FRAME_SIZES.Count)
    $offset = $ICO_HEADER_SIZE + $ICO_ENTRY_SIZE * $FRAME_SIZES.Count
    for ($i = 0; $i -lt $FRAME_SIZES.Count; $i++) {
        $size = $FRAME_SIZES[$i]
        $dimension = if ($size -ge $ICO_LARGE_SIZE) { 0 } else { $size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]$ICO_PLANES)
        $writer.Write([uint16]$ICO_BIT_COUNT)
        $writer.Write([uint32]$frames[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    $writer.Flush()
    [System.IO.File]::WriteAllBytes($target, $file.ToArray())
}
finally {
    $writer.Dispose()
}
Write-Host "created $target ($((Get-Item $target).Length) bytes, $($FRAME_SIZES.Count) frames)"
