/**
 * @file    : WindowsUpdateStateRule.cs
 * @author  : rudals252
 * @brief   : 업데이트 진행·재부팅·서비스 관측을 설명하며 삭제 가능 판정을 만들지 않음
 */
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Rules;

/// <summary>순간 상태를 배타적 유휴 상태 또는 자동 정리 승인으로 해석하지 않습니다.</summary>
public sealed class WindowsUpdateStateRule : IRule
{
    /// <summary>로컬 상태 전용 프로브 식별자입니다.</summary>
    public const string ProbeId = "windows-update.local-state";
    /// <inheritdoc />
    public string Id => "windows-update.cleanup-state";
    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        if (!snapshot.TryGetProbe(ProbeId, out var probe) || probe.Status is not (ProbeStatus.Success or ProbeStatus.Partial)) { return []; }
        bool? Bool(string key) => snapshot.GetMeasurement(ProbeId, key)?.Value is BooleanValue b ? b.Value : null;
        string? Text(string key) => snapshot.GetMeasurement(ProbeId, key)?.Value is TextValue t ? t.Value : null;
        var busy = Bool("installing"); var reboot = Bool("rebootRequired"); var wu = Text("wuauserv"); var bits = Text("bits");
        var missing = busy is null || reboot is null || wu is null || bits is null;
        var transition = new[] { wu, bits }.Any(s => s is not null && s is not ("Running" or "Stopped"));
        var title = reboot == true ? "Windows 업데이트 완료에 재시작이 필요합니다"
            : busy == true ? "Windows 업데이트 설치·제거가 진행 중입니다"
            : missing ? "Windows 업데이트 상태를 일부 확인하지 못했습니다"
            : transition ? "업데이트 관련 서비스가 전환 중이거나 일시 중지됐습니다"
            : "Windows 업데이트의 현재 상태를 확인했습니다";
        string Flag(bool? flag) => flag is true ? "예" : flag is false ? "아니요" : "확인 불가";
        string State(string? state) => state switch { "Running" => "실행 중", "Stopped" => "정지", "StartPending" => "시작 중", "StopPending" => "정지 중", "Paused" => "일시 중지", null => "확인 불가", _ => "전환 중" };
        return [new Finding(Id, FindingCategory.Storage, title, probe.Measurements,
            "로컬 Windows Update API와 서비스 관리자의 보고값입니다. 네트워크 업데이트 검색·서비스 중지·파일 삭제는 하지 않았습니다.",
            missing ? Verdict.CannotVerify : Verdict.Info, missing ? CannotVerifyReason.PartialData : null,
            $"설치·제거 진행: {Flag(busy)}\n업데이트 재시작 필요: {Flag(reboot)}\nWindows Update: {State(wu)}\nBITS: {State(bits)}\n설치가 진행되지 않는다는 보고만으로 다운로드·설치 작업 전체가 멈췄다고 판단할 수 없습니다. Download 직접 정리는 아직 제공하지 않습니다.",
            null, new Impact("업데이트 작업 또는 재시작을 먼저 마쳐야 하는지 확인합니다.", "서비스가 정지됐다는 이유만으로 오류로 판단하거나 자동으로 켜지 않습니다."),
            [new ShowDetailsAction(), new OpenSettingsAction("ms-settings:windowsupdate-optionalupdates")])];
    }
}
