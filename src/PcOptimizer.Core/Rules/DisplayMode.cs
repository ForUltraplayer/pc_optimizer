/**
 * @file    : DisplayMode.cs
 * @author  : rudals252
 * @brief   : 드라이버가 보고한 디스플레이 모드 하나(해상도·방향·색 깊이·주사 방식·정수 주사율) 레코드
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 디스플레이 모드 하나입니다(EnumDisplaySettings의 DEVMODE 값에서 필요한 필드만).
/// </summary>
/// <param name="Width">가로 해상도(px).</param>
/// <param name="Height">세로 해상도(px).</param>
/// <param name="Orientation">방향 원시 값(DMDO_*).</param>
/// <param name="BitsPerPixel">픽셀당 비트 수.</param>
/// <param name="Interlaced">인터레이스 여부.</param>
/// <param name="RefreshHz">정수 주사율(Hz). 0·1은 "하드웨어 기본값"이라는 뜻이다.</param>
public sealed record DisplayMode(int Width, int Height, int Orientation, int BitsPerPixel, bool Interlaced, int RefreshHz);
