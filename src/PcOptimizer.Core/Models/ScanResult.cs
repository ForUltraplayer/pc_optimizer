/**
 * @file    : ScanResult.cs
 * @author  : rudals252
 * @brief   : 검사 실행 결과(리포트와 규칙 입력에 쓰인 불변 스냅샷) 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 검사 한 번의 실행 결과입니다. 리포트와 함께, 상세 화면이 측정값·Issue를 보여 줄 수 있도록 스냅샷을 돌려줍니다.
/// </summary>
/// <param name="Report">검사 리포트.</param>
/// <param name="Snapshot">규칙 입력에 쓰인 불변 스냅샷(늦게 도착한 결과는 포함되지 않음).</param>
public sealed record ScanResult(ScanReport Report, ScanSnapshot Snapshot);
