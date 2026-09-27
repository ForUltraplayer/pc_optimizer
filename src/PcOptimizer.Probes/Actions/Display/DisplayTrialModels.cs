/**
 * @file    : DisplayTrialModels.cs
 * @author  : rudals252
 * @brief   : 주사율 시험의 물리 경로·원본 모드·단일 확인 결정·결과 계약
 */
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Actions.Display;

/// <summary>같은 부팅에서 실제 출력 경로를 재식별하는 값입니다.</summary>
public sealed record DisplayIdentity(string MonitorPath, string AdapterPath, string GdiName, long AdapterLuid, uint SourceId, uint TargetId, int OutputTechnology = 5);
/// <summary>조회한 모드와 원래 DEVMODE bytes를 함께 보관합니다.</summary>
public sealed record DisplayFrame(DisplayMode Mode, byte[] Native)
{
    /// <summary>네이티브 패딩 대신 실제 표시 모드를 비교합니다. 원문은 별도로 검증합니다.</summary>
    public bool SameMode(DisplayFrame other) => Mode == other.Mode;
}
/// <summary>시험 전 실제 모드·사용자 저장 모드와 선택한 모드입니다.</summary>
public sealed record DisplayChange(DisplayIdentity Identity, DisplayFrame Before, DisplayFrame RegisteredBefore, DisplayFrame Desired);
/// <summary>재관측한 실제 표시·프로필 모드입니다. 장치 경로가 달라지면 공급자가 거절합니다.</summary>
public sealed record DisplayObservation(DisplayFrame Current, DisplayFrame Registered);
/// <summary>UI에 원문 장치 경로/bytes를 넘기지 않는 준비 결과입니다.</summary>
public sealed record DisplayTrialPreparation(Guid? Id, int? BeforeHz, int? DesiredHz, string Code);
/// <summary>단조 기한과 현재 상태를 UI와 독립적으로 보존합니다.</summary>
public sealed record DisplayTrialStatus(Guid Id, string State, TimeSpan Remaining);
/// <summary>유지와 실제 원복 성공을 구분하며 API 반환만으로 성공을 주장하지 않습니다.</summary>
public sealed record DisplayTrialResult(Guid Id, bool Started, bool Kept, bool Restored, string Code);
/// <summary>드라이버가 지원하는 동일 해상도 주사율 후보입니다. 경로는 실행기 식별에만 사용합니다.</summary>
public sealed record DisplayTrialChoice(string DeviceKey, string Title, int CurrentHz, int DesiredHz);
/// <summary>원문 경로·bytes 없이 보여주는 복구 항목입니다.</summary>
public sealed record DisplayTrialRecord(Guid Id, int BeforeHz, int AppliedHz, bool NeedsRecovery, bool CanRestore);
/// <summary>화면 후보와 이 실행기의 복구 기록이며 확인 실패를 빈 성공으로 숨기지 않습니다.</summary>
public sealed record DisplayTrialCatalog(IReadOnlyList<DisplayTrialChoice> Choices, IReadOnlyList<DisplayTrialRecord> Records, string Code);

internal interface IDisplaySettingsPlatform
{
    IReadOnlyList<DisplayTrialChoice> Choices() => [];
    DisplayChange Capture(string monitorPath, int hz);
    DisplayObservation Observe(DisplayIdentity identity);
    int Test(DisplayIdentity identity, DisplayFrame frame);
    int Apply(DisplayIdentity identity, DisplayFrame frame, bool persist, DisplayObservation expected, Action beforeWrite);
    int SaveProfileOnly(DisplayIdentity identity, DisplayFrame frame, DisplayObservation expected, Action beforeWrite);
}
