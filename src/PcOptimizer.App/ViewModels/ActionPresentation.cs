/**
 * @file    : ActionPresentation.cs
 * @author  : rudals252
 * @brief   : 발급 계획과 관측 결과를 초보자용 문구로 표시, 예상/실측과 거절/부분 변경 구분
 */
using System.Globalization;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>실행기가 발급한 계획의 읽기 전용 확인 화면입니다.</summary>
public sealed class ActionPreviewViewModel(ActionPlan plan)
{
    /// <summary>실행할 계획 ID입니다. UI가 대상이나 원문을 재구성하지 않습니다.</summary>
    public Guid Id => plan.Id;
    /// <summary>조치 종류입니다.</summary>
    public ActionId ActionId => plan.Definition.Id;
    /// <summary>되돌리기 계획인지 여부입니다.</summary>
    public bool IsRestore => plan.IsRestore;
    /// <summary>변경하려는 내용을 한 문장으로 보여 줍니다.</summary>
    public string Title => IsRestore ? $"{ActionText.Name(ActionId)} 되돌리기" : ActionText.Name(ActionId);
    /// <summary>조치 설명입니다.</summary>
    public string Summary => plan.Preview.Summary;
    /// <summary>실행기의 관측 대상 표시입니다.</summary>
    public string Target => plan.Preview.Details?.TargetLabel ?? Title;
    /// <summary>효과와 부작용 설명입니다.</summary>
    public string Impact => plan.Preview.Impact;
    /// <summary>안전 조건이 제공되지 않았으면 확인 불가라고 표시합니다.</summary>
    public string Safety => plan.Preview.Details?.SafetyNote ?? "실행 직전에 대상과 현재 상태를 다시 확인합니다.";
    /// <summary>예상 논리 크기이며 실제 확보 공간이 아닙니다.</summary>
    public string Estimate => plan.Preview.Details?.EstimatedLogicalBytes is >= 0 and var bytes
        ? $"대상 파일의 논리 크기: {ActionText.Bytes(bytes)} (예상치)" : "대상 파일의 논리 크기: 제공되지 않음";
    /// <summary>공간 관련 조치 또는 실제 추정값이 있는 경우에만 공간 항목을 보여 줍니다.</summary>
    public bool HasSpaceEstimate => ActionText.IsSpaceAction(ActionId) || plan.Preview.Details?.EstimatedLogicalBytes is not null;
    /// <summary>재부팅 필요 여부를 추정하지 않습니다.</summary>
    public string Restart => plan.Preview.Details?.RequiresRestart switch { true => "재부팅 필요", false => "재부팅 불필요", _ => "재부팅 필요 여부: 확인되지 않음" };
    /// <summary>지원되는 복구와 파일 삭제의 차이를 설명합니다.</summary>
    public string Restore => IsRestore ? "현재 값이 앱의 적용 값과 같을 때만 되돌립니다."
        : plan.Definition.SupportsRestore ? "변경 전 원래 값을 저장합니다. 이 앱에서 되돌릴 수 있습니다." : "이 조치는 이 앱에서 되돌릴 수 없습니다.";
    /// <summary>확인 화면을 오래 열어 두었을 때의 만료 안내입니다.</summary>
    public string Expiry => $"확인 유효 시간: {plan.ExpiresAt.ToLocalTime():HH:mm:ss}까지. 만료되면 다시 확인해 주세요.";
}

/// <summary>원문 SID/명령/복구 바이트 없이 실행 결과와 다음 행동을 보존합니다.</summary>
public sealed class ActionResultViewModel(ActionResult result, ActionId actionId, bool restore, string estimate)
{
    /// <summary>실행/복구 종류와 결과 제목입니다.</summary>
    public string Title => IsDraining ? "실제 작업이 끝나기를 기다리는 중입니다"
        : result.Succeeded ? restore ? "이전 설정으로 되돌렸습니다" : "선택한 조치를 완료했습니다"
        : result.Code == "AlreadyOriginal" ? "이미 원래 설정입니다"
        : result.Code == "AlreadyApplied" ? "이미 선택한 설정이 적용되어 있습니다"
        : result.Code == "NoEligibleFiles" ? "지금 정리할 대상이 없습니다"
        : result.Started ? restore ? "되돌리기를 끝내지 못했습니다" : "변경 결과를 확인해야 합니다" : "설정을 변경하기 전에 멈췄습니다";
    /// <summary>뒤늦게 종료되는 작업을 완료로 표시하지 않습니다.</summary>
    public bool IsDraining => result.Code is "Draining" or "Running" or "Validating";
    /// <summary>어떤 조치의 결과인지 표시합니다.</summary>
    public string ActionName => ActionText.Name(actionId);
    /// <summary>실행 전 관측된 예상치입니다.</summary>
    public string Estimate => estimate;
    /// <summary>설정 조치에 관련 없는 공간 확인 불가 문구를 나열하지 않습니다.</summary>
    public bool HasSpaceEffect => ActionText.IsSpaceAction(actionId) || result.Effect is not null;
    /// <summary>파일 단위 결과이며 건너뜀/실패를 삭제 성공으로 합산하지 않습니다.</summary>
    public string FileCounts => result.Effect?.ChangedFiles is { } changed
        ? $"삭제 {changed:N0}개 · 건너뜀 {result.Effect.SkippedFiles ?? 0:N0}개 · 사용 중/실패 {result.Effect.FailedFiles ?? 0:N0}개" : "";
    /// <summary>관측된 볼륨 여유 변화는 음수/미확인을 그대로 표시합니다.</summary>
    public string Actual => result.Effect?.FreeSpaceDeltaBytes switch
    {
        null => "실제 여유 공간 변화: 확인되지 않음",
        0 => "실제 여유 공간 변화: 변화 없음 (0 B)",
        < 0 and var bytes => $"실제 여유 공간 변화: {ActionText.Bytes(-(decimal)bytes)} 감소",
        var bytes => $"실제 여유 공간 변화: {ActionText.Bytes(bytes.Value)} 증가",
    };
    /// <summary>오류 원문 대신 알려진 사유와 필요한 다음 행동만 보여 줍니다.</summary>
    public string Detail => IsDraining ? "중단 요청 뒤에도 실제 작업이 남아 있을 수 있습니다. 종료가 확인될 때까지 다른 조치는 시작하지 않습니다."
        : result.Succeeded ? HasSpaceEffect ? "완료 후 상태를 다시 검사합니다. 여유 공간 변화에는 다른 프로그램의 작업도 영향을 줍니다." : "선택한 설정의 변경을 확인했습니다. 재검사 결과는 메인 화면에서 확인해 주세요."
        : result.Code switch
        {
            "AlreadyOriginal" => "다시 쓸 필요가 없어 설정을 변경하지 않았습니다.",
            "AlreadyApplied" => "현재 값이 선택한 설정과 같아 변경하지 않았습니다.",
            "NoEligibleFiles" => "최근 파일과 보호 대상을 제외하면 정리할 파일이 없습니다. 파일을 변경하지 않았습니다.",
            "FilesUnavailable" => "사용 중이거나 접근할 수 없는 파일은 그대로 두었습니다. 관련 앱을 닫은 뒤 다시 확인할 수 있습니다.",
            "TargetChanged" => "미리보기 이후 파일 또는 위치가 달라져 실행하지 않았습니다. 대상을 다시 확인해 주세요.",
            "CurrentValueChanged" => "확인 이후 설정이 달라졌습니다. 사용자가 바꾼 값을 덮어쓰지 않습니다. 다시 검사해 주세요.",
            "PlanExpired" => "확인 시간이 만료되었거나 이미 사용한 계획입니다. 미리보기를 다시 열어 주세요.",
            "SessionChanged" or "ScopeExcluded" => "사용자 또는 실행 범위가 달라졌습니다. 현재 사용자로 다시 확인해 주세요.",
            "Unsupported" or "TargetRejected" or "Blocked" => "이 대상은 현재 앱에서 처리할 수 없습니다. 지원 도구나 Windows 설정에서 확인해 주세요.",
            "Busy" => "다른 검사나 조치가 아직 진행 중입니다. 종료 후 다시 확인해 주세요.",
            "Cancelled" or "TimedOut" when !result.Started => "실행 전에 취소되었거나 확인 시간이 초과되었습니다. 변경을 시작하지 않았습니다.",
            _ when result.Started => restore ? "복구가 일부 진행됐을 수 있습니다. 아래 복구 기록과 현재 상태를 확인해 주세요." : "변경이 일부 진행됐을 수 있습니다. 재검사 결과와 아래 복구 기록을 확인해 주세요.",
            _ => "대상 확인 또는 원본 저장에 실패해 변경을 시작하지 않았습니다. 다시 확인해 주세요.",
        };
}

/// <summary>복구 목록에서 필요한 식별자와 표시만 보존합니다. 원래 설정 bytes/SID는 포함하지 않습니다.</summary>
public sealed record RollbackItemViewModel(Guid Id, ActionId ActionId, string Title, string Detail, bool NeedsRecovery, bool CanRestore)
{
    /// <summary>지원되지 않는 기록의 비활성 사유도 화면에 남깁니다.</summary>
    public string Availability => CanRestore ? "현재 설정을 확인한 뒤 되돌릴 수 있습니다." : "완료된 기록이거나 현재 버전·사용자 범위에서 복구를 지원하지 않습니다.";
}

internal static class ActionText
{
    internal static bool IsSpaceAction(ActionId id) => id is ActionId.UserFiles or ActionId.SystemFiles or ActionId.AppFiles or ActionId.OfficialCache or ActionId.DeliveryOptimization;
    internal static string Name(ActionId id) => id switch
    {
        ActionId.UserFiles => "사용자 임시 파일 정리", ActionId.SystemFiles => "Windows 캐시 정리", ActionId.AppFiles => "앱 캐시 정리",
        ActionId.Startup => "시작 프로그램 변경", ActionId.Power => "전원 계획 변경", ActionId.Display => "화면 주사율 변경",
        ActionId.OfficialCache => "공식 도구 캐시 정리", ActionId.DeliveryOptimization => "배달 최적화 캐시 정리", _ => "지원하지 않는 조치",
    };
    internal static string Bytes(decimal bytes)
    {
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        var index = 0;
        while (bytes >= 1024 && index < units.Length - 1) { bytes /= 1024; index++; }
        return bytes.ToString("0.##", CultureInfo.CurrentCulture) + " " + units[index];
    }
}
