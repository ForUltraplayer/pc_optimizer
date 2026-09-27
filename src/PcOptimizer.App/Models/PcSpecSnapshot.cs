/**
 * @file    : PcSpecSnapshot.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 스냅샷 모델(라벨·값 항목, 섹션, 캡처 시각·확인 불가 섹션 목록). 값 null은 "확인 불가"로 표시한다
 */

namespace PcOptimizer.App.Models;

/// <summary>
/// 사양 항목 한 줄(라벨: 값)입니다.
/// </summary>
/// <param name="Label">항목 라벨(예: "모델", "슬롯 DIMM A1", "SSD").</param>
/// <param name="Value">표시 값. 알 수 없으면 null이며 화면·텍스트는 "확인 불가"로 표시합니다(0·빈 문자열로 바꾸지 않음).</param>
/// <param name="IsIdentifying">행 전체가 사용자·PC를 식별할 수 있는 정보인지 여부.</param>
/// <param name="IdentifyingValue">식별 정보 포함을 선택한 경우의 표시 값. 기본 값에는 사용자 지정 이름을 넣지 않습니다.</param>
public sealed record PcSpecItem(string Label, string? Value, bool IsIdentifying = false, string? IdentifyingValue = null);

/// <summary>
/// 사양 섹션(예: 운영체제, CPU, 메모리)입니다.
/// </summary>
/// <param name="Title">섹션 제목.</param>
/// <param name="Items">섹션 항목(장치마다 한 줄, 요약으로 합치지 않음).</param>
public sealed record PcSpecSection(string Title, IReadOnlyList<PcSpecItem> Items);

/// <summary>
/// 내 PC 사양 스냅샷입니다. 열 때마다 새로 읽어 만듭니다.
/// </summary>
/// <param name="CapturedAtUtc">캡처 시각(UTC).</param>
/// <param name="Sections">고정 순서의 9개 섹션.</param>
/// <param name="UnavailableSections">모든 항목 값이 null인(확인 불가) 섹션 제목.</param>
public sealed record PcSpecSnapshot(DateTimeOffset CapturedAtUtc, IReadOnlyList<PcSpecSection> Sections, IReadOnlyList<string> UnavailableSections);
