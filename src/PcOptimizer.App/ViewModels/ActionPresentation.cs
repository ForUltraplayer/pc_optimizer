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
    public string Title => IsRestore ? ActionId is PcOptimizer.Core.Actions.ActionId.Startup or PcOptimizer.Core.Actions.ActionId.MachineStartup ? "자동 실행 등록 복원"
        : ActionId == PcOptimizer.Core.Actions.ActionId.UpdateServices ? "업데이트 서비스 원상복구" : $"{ActionText.Name(ActionId)} 되돌리기" : ActionText.Name(ActionId);
    /// <summary>조치 설명입니다.</summary>
    public string Summary => plan.Preview.Summary;
    /// <summary>실행기의 관측 대상 표시입니다.</summary>
    public string Target => plan.Preview.Details?.TargetLabel ?? Title;
    /// <summary>효과와 부작용 설명입니다.</summary>
    public string Impact => plan.Preview.Impact;
    /// <summary>안전 조건이 제공되지 않았으면 확인 불가라고 표시합니다.</summary>
    public string Safety => plan.Preview.Details?.SafetyNote ?? "실행 직전에 대상과 현재 상태를 다시 확인합니다.";
    /// <summary>코드 등록의 안전 수준과 개별 확인 정책입니다.</summary>
    public string SafetyLabel => plan.Definition.Safety == PcOptimizer.Core.Models.SafetyLevel.Irreversible ? "되돌릴 수 없는 정리 · 이 항목만 실행" : "주의가 필요한 설정 변경 · 이 항목만 실행";
    /// <summary>예상 논리 크기이며 실제 확보 공간이 아닙니다.</summary>
    public string Estimate => plan.Preview.Details?.EstimatedLogicalBytes is >= 0 and var bytes
        ? $"대상 파일의 논리 크기: {ActionText.Bytes(bytes)} (예상치)" : "대상 파일의 논리 크기: 제공되지 않음";
    /// <summary>공간 관련 조치 또는 실제 추정값이 있는 경우에만 공간 항목을 보여 줍니다.</summary>
    public bool HasSpaceEstimate => ActionText.IsSpaceAction(ActionId) || plan.Preview.Details?.EstimatedLogicalBytes is not null;
    /// <summary>재부팅 필요 여부를 추정하지 않습니다.</summary>
    public string Restart => plan.Preview.Details?.RequiresRestart switch { true => "재부팅 필요", false => "재부팅 불필요", _ => "재부팅 필요 여부: 확인되지 않음" };
    /// <summary>지원되는 복구와 파일 삭제의 차이를 설명합니다.</summary>
    public string Restore => IsRestore ? "현재 값이 앱의 적용 값과 같을 때만 되돌립니다."
        : ActionId == PcOptimizer.Core.Actions.ActionId.WindowsUpdateCache ? "삭제한 파일은 되돌릴 수 없습니다. 서비스는 작업 후 원래 상태로 복구하며, 실패하면 조치 기록에서 서비스만 복구할 수 있습니다."
        : ActionId is PcOptimizer.Core.Actions.ActionId.Startup or PcOptimizer.Core.Actions.ActionId.MachineStartup ? "해제한 등록 원문은 복원 전까지 이 앱의 기록에 보관합니다. 복구 기록을 직접 지우면 되돌릴 수 없습니다."
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
    /// <summary>공식 도구 전후 논리 크기를 디스크 여유 공간과 구분합니다.</summary>
    public string CacheObservation => result.Effect?.BeforeLogicalBytes is { } before
        ? $"실행 직전 캐시: {ActionText.Bytes(before)} → 남은 캐시: {(result.Effect.AfterLogicalBytes is { } after ? ActionText.Bytes(after) : "확인 불가")}" : "";
    /// <summary>관측된 볼륨 여유 변화는 음수/미확인을 그대로 표시합니다.</summary>
    public string Actual => result.Effect?.FreeSpaceDeltaBytes switch
    {
        null => "실제 여유 공간 변화: 확인되지 않음",
        0 => "실제 여유 공간 변화: 변화 없음 (0 B)",
        < 0 and var bytes => $"실제 여유 공간 변화: {ActionText.Bytes(-(decimal)bytes)} 감소",
        var bytes => $"실제 여유 공간 변화: {ActionText.Bytes(bytes.Value)} 증가",
    };
    /// <summary>오류 원문 대신 알려진 사유와 필요한 다음 행동만 보여 줍니다.</summary>
    public string Detail => OutcomeDetail + (!IsDraining && result.ServiceRecoveryCompleted == true
        ? " 이번 조치에 기록한 업데이트 서비스는 원래 상태로 돌아온 것을 확인했습니다." : "");
    private string OutcomeDetail => IsDraining ? "중단 요청 뒤에도 실제 작업이 남아 있을 수 있습니다. 종료가 확인될 때까지 다른 조치는 시작하지 않습니다."
        : result.Succeeded ? HasSpaceEffect ? "완료 후 상태를 다시 검사합니다. 여유 공간 변화에는 다른 프로그램의 작업도 영향을 줍니다." : "선택한 설정의 변경을 확인했습니다. 재검사 결과는 메인 화면에서 확인해 주세요."
        : result.Code switch
        {
            "AlreadyOriginal" => "다시 쓸 필요가 없어 설정을 변경하지 않았습니다.",
            "AlreadyApplied" => "현재 값이 선택한 설정과 같아 변경하지 않았습니다.",
            "ToolUnavailable" => "이 도구를 실행할 수 있는 설치를 찾지 못했습니다. Windows 저장소 설정에서도 정리할 수 있습니다.",
            "ToolNotInProtectedLocation" => "사용자 쓰기 가능 위치의 도구는 관리자 권한으로 실행하지 않습니다. 해당 도구를 일반 터미널에서 직접 사용해 주세요.",
            "UserScopeExcluded" or "OutsideUserProfile" => "현재 사용자 범위 밖의 캐시여서 실행하지 않았습니다.",
            "ToolChanged" => "공식 도구나 캐시 경로가 바뀌었습니다. 미리보기를 다시 확인해 주세요.",
            "StartFailed" or "PreflightFailed" => "공식 도구 실행을 시작하지 못했습니다. 캐시 정리는 수행하지 않았습니다.",
            "ObservationFailed" => "도구 실행 뒤 남은 캐시를 확인하지 못했습니다. 정리가 일부 진행됐을 수 있으므로 다시 검사해 주세요.",
            "NoEligibleFiles" => "최근 파일과 보호 대상을 제외하면 정리할 파일이 없습니다. 파일을 변경하지 않았습니다.",
            "Partial" when ActionText.IsSpaceAction(actionId) => "일부 파일만 정리했습니다. 아래 삭제·건너뜀 개수와 재검사 결과를 확인해 주세요. 사용 중인 파일은 강제로 지우지 않았습니다.",
            "FilesUnavailable" => "사용 중이거나 접근할 수 없는 파일은 그대로 두었습니다. 관련 앱을 닫은 뒤 다시 확인할 수 있습니다.",
            "AdobeAppRunning" => (result.Started ? "일부 파일을 정리한 뒤 Adobe 관련 앱 실행을 확인해 나머지는 남겼습니다. " : "Adobe 관련 앱이 실행 중이어서 정리를 시작하지 않았습니다. ") + "Premiere·After Effects·Media Encoder·Audition과 백그라운드 렌더 작업을 종료한 뒤 다시 확인하세요.",
            "AdobeStateUnavailable" => (result.Started ? "일부 파일을 정리한 뒤 앱 실행 상태를 확인하지 못해 중단했습니다. " : "앱 실행 상태를 확인하지 못해 정리를 시작하지 않았습니다. ") + "삭제·건너뜀 개수를 확인하고 잠시 후 다시 시도하세요.",
            "AdobeCacheMissing" => "선택한 Adobe 캐시 폴더를 찾거나 읽을 수 없습니다. Adobe 환경설정에서 위치를 확인해 다시 선택하세요. 다른 위치를 대신 지우지 않습니다.",
            "AdobeLocationUnsupported" => "지원하는 로컬 캐시 위치가 아닙니다. 네트워크·이동식 드라이브·별칭 경로 대신 고정 드라이브의 실제 캐시 폴더를 선택하세요.",
            "AdobeCacheFolderRequired" => "캐시 상위 경로나 프로젝트 폴더 대신 Media Cache Files 또는 Peak Files 폴더 자체를 선택하세요.",
            "AdobeLocationsFull" => "이번 실행에서 선택할 수 있는 캐시 폴더 8곳을 모두 등록했습니다. 다른 폴더가 필요하면 진행 중인 작업을 마친 뒤 앱을 다시 실행하세요.",
            "SteamLocationUnsupported" => "로컬 고정 드라이브의 steamapps\\shadercache 폴더 자체를 선택하세요. 네트워크·별칭·라이브러리 상위 폴더는 지원하지 않습니다.",
            "SteamProtectedLocation" => "Program Files·개인 문서·다른 사용자 등 보호 위치는 직접 정리하지 않습니다. 선택한 라이브러리의 보호 조건을 확인하세요.",
            "SteamLibrariesUnavailable" => "현재 Steam 라이브러리 설정을 완전히 읽지 못했습니다. 선택 경로만으로 삭제를 허용하지 않습니다.",
            "SteamLibraryChanged" => "선택한 캐시가 현재 Steam 라이브러리 목록에 없습니다. 검사 결과와 Steam 저장소 설정에서 위치를 다시 확인하세요.",
            "SteamCacheMissing" => "선택한 셰이더 캐시 폴더가 없거나 접근할 수 없습니다. 다른 캐시를 대신 지우지 않습니다.",
            "SteamLocationsFull" => "이번 실행에서 선택할 수 있는 라이브러리 16곳을 모두 등록했습니다. 작업을 마친 뒤 앱을 다시 실행하세요.",
            "SteamAppRunning" or "SteamGameRunning" => (result.Started ? "일부 파일 처리 후 Steam 또는 게임 실행을 확인해 나머지는 남겼습니다. " : "Steam 또는 라이브러리의 게임이 실행 중이어서 정리를 시작하지 않았습니다. ") + "Steam·게임·관련 백그라운드 작업을 직접 종료한 뒤 다시 확인하세요.",
            "SteamStateUnavailable" => (result.Started ? "일부 파일 처리 후 실행 상태를 확인하지 못해 중단했습니다. " : "실행 중인 프로세스 정보를 완전히 확인하지 못해 정리를 시작하지 않았습니다. ") + "확인 불가를 유휴 상태로 간주하지 않습니다.",
            "TargetChanged" => result.Started ? "일부 처리 후 나머지 대상의 상태가 달라져 중단했습니다. 처리 개수를 확인하고 다시 검사해 주세요." : "미리보기 이후 파일 또는 위치가 달라져 실행하지 않았습니다. 대상을 다시 확인해 주세요.",
            "CurrentValueChanged" => "확인 이후 설정이 달라졌습니다. 사용자가 바꾼 값을 덮어쓰지 않습니다. 다시 검사해 주세요.",
            "PowerSchemeMissing" => "선택한 계획이나 되돌릴 계획이 이 PC에 없습니다. 새 전원 계획을 만들지는 않습니다.",
            "PowerNeedsAc" => "고성능 또는 사용자 지정 계획은 AC 전원이 확인될 때만 적용합니다. 전원 연결 후 다시 확인해 주세요.",
            "PowerStateChanged" => "미리보기 이후 전원 연결 상태가 달라졌습니다. 현재 상태에서 다시 확인해 주세요.",
            "PowerReadFailed" => "현재 전원 계획을 확인하지 못했습니다. Windows 전원 설정에서 상태를 확인해 주세요.",
            "DeliveryBusy" => (result.Started ? "일부 처리 후 새 다운로드나 일시 중지 항목을 확인해 나머지는 남겼습니다. " : "다운로드 또는 일시 중지 항목이 있어 정리를 시작하지 않았습니다. ") + "Windows·Store 다운로드가 끝난 뒤 다시 확인하세요.",
            "DeliveryStateUnavailable" => (result.Started ? "정리 후 상태를 모두 확인하지 못했습니다. 처리 개수와 재검사 결과를 확인하세요." : "Windows 배달 최적화 캐시 상태를 확인하지 못해 정리를 시작하지 않았습니다."),
            "DeliveryContractUnsupported" => result.Started ? "일부 처리 후 Windows 제공자 형식을 확인하지 못해 추가 삭제 요청을 멈췄습니다. 처리 개수를 확인하세요." : "이 Windows 제공자의 정리 기능 형식을 확인하지 못해 삭제를 요청하지 않았습니다.",
            "DeliveryDeleteFailed" or "DeliveryVerificationFailed" => result.Started ? "Windows가 캐시 정리를 완료했는지 확인하지 못했습니다. 이미 처리한 항목은 되돌릴 수 없습니다. 재검사 후 남은 항목을 확인하세요." : "Windows 정리 요청을 준비하지 못해 삭제를 시작하지 않았습니다. 권한과 Windows 상태를 확인해 주세요.",
            "StartupUnsupported" => "선택한 Run 출처의 문자열 등록만 해제할 수 있습니다. 없거나 지원하지 않는 형식이면 Windows 시작 앱 설정을 사용하세요.",
            "StartupReadFailed" => "자동 실행 등록을 읽지 못했습니다. 항목이 이동·삭제됐거나 접근 권한이 달라졌을 수 있습니다. 다시 검사해 주세요.",
            "StartupWriteFailed" => "Windows가 등록 변경을 완료하지 못했습니다. 다른 앱의 변경·정책을 확인해 주세요. 원래 등록의 복구 기록은 보존했습니다.",
            "PowerWriteFailed" => "Windows가 전원 계획 변경을 완료하지 못했습니다. 정책과 현재 계획을 확인해 주세요. 복구 기록은 보존했습니다.",
            "UpdateRecoveryRequired" => "이전 업데이트 정리의 서비스 복구가 남아 있습니다. 조치 기록에서 복구한 뒤 다시 확인하세요.",
            "UpdateRecoveryFailed" => "원래 실행 중이던 서비스를 모두 복구하지 못했습니다. 파일 정리가 일부 진행됐을 수 있습니다. 조치 기록의 서비스 복구 항목을 확인하세요.",
            "UpdateServiceUnavailable" => "업데이트 관련 서비스 상태나 접근 권한을 확인하지 못했습니다. Windows 서비스 정책을 확인하세요.",
            "UpdateServiceTransitionFailed" => "서비스가 전환 중이거나 제한 시간 안에 요청한 상태가 되지 않았습니다. 중단된 작업의 복구 기록이 있으면 먼저 확인하세요.",
            "UpdateServiceChangeFailed" => "Windows가 서비스 변경 요청을 거절했습니다. 종속 서비스나 시작 유형은 추가로 변경하지 않았습니다. 복구 기록을 확인하세요.",
            "UpdateInstallBusy" => "업데이트 설치 또는 제거가 진행 중입니다. 작업 완료 후 다시 확인하세요.",
            "UpdateRebootRequired" => "업데이트를 마치려면 재시작이 필요합니다. 작업을 저장하고 Windows에서 재시작한 뒤 확인하세요.",
            "UpdateStateUnavailable" => "업데이트 활동 상태를 모두 확인하지 못해 추가 파일 처리를 하지 않습니다. 실행 결과와 복구 기록을 확인하세요.",
            "UpdateServicesNotReady" => "Windows Update·BITS가 모두 실행 중이고 중지 가능한 상태에서만 이 정리를 준비합니다. 정지된 서비스를 정리 목적으로 켜지 않습니다. Windows 저장소 정리를 이용하거나 업데이트 작업 완료 후 다시 확인하세요.",
            "UpdateBitsJobsPresent" => "BITS에 다운로드 작업이 남아 있습니다. 일시 중지·오류 상태의 작업도 자동 취소하지 않습니다. 해당 앱이나 Windows의 다운로드를 마친 뒤 다시 확인하세요.",
            "UpdateBitsUnavailable" => "모든 사용자의 BITS 전송 작업을 확인하지 못해 정리를 시작하지 않았습니다.",
            "UpdateWorkerBusy" => "업데이트 또는 Windows 구성 요소 작업이 실행 중입니다. 작업을 강제 종료하지 않으며 완료 후 다시 확인해야 합니다.",
            "UpdateDownloadBusy" => "Windows·Store 다운로드가 진행 중이거나 일시 중지돼 있습니다. 다운로드를 마친 뒤 다시 확인하세요.",
            "UpdateServiceRestarted" => "파일 처리 중 업데이트 서비스가 다시 시작되어 추가 삭제를 멈췄습니다. 처리 개수와 재검사 결과를 확인하세요.",
            "UpdateDownloadMissing" => "고정 Windows 업데이트 Download 폴더를 찾거나 읽을 수 없습니다. 다른 위치를 대신 지우지 않습니다.",
            "UpdateNoFilesChanged" => "파일은 삭제하지 않았습니다. 잠시 중지했던 업데이트 서비스는 원래 상태로 복구했습니다.",
            "UpdateMaintenanceFailed" => "업데이트 캐시 정리 중 오류가 발생했습니다. 이미 처리한 파일과 서비스 복구 기록을 확인하세요.",
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
    internal static bool IsSpaceAction(ActionId id) => id is ActionId.UserFiles or ActionId.SystemFiles or ActionId.AppFiles or ActionId.OfficialCache or ActionId.DeliveryOptimization or ActionId.WindowsUpdateCache or ActionId.SteamShaderCache;
    internal static string Name(ActionId id) => id switch
    {
        ActionId.UserFiles => "사용자 임시 파일 정리", ActionId.SystemFiles => "Windows 캐시 정리", ActionId.AppFiles => "앱 캐시 정리",
        ActionId.Startup => "자동 실행 등록 해제", ActionId.MachineStartup => "모든 사용자 자동 실행 등록 해제", ActionId.Power => "전원 계획 변경", ActionId.Display => "화면 주사율 변경",
        ActionId.OfficialCache => "공식 도구 캐시 정리", ActionId.DeliveryOptimization => "배달 최적화 캐시 정리", ActionId.UpdateServices => "업데이트 서비스 원상복구", ActionId.WindowsUpdateCache => "Windows 업데이트 다운로드 캐시 정리", ActionId.SteamShaderCache => "Steam 셰이더 캐시 정리", _ => "지원하지 않는 조치",
    };
    internal static string Bytes(decimal bytes)
    {
        var units = new[] { "B", "KiB", "MiB", "GiB", "TiB" };
        var index = 0;
        while (bytes >= 1024 && index < units.Length - 1) { bytes /= 1024; index++; }
        return bytes.ToString("0.##", CultureInfo.CurrentCulture) + " " + units[index];
    }
}
