/**
 * @file    : ActionModels.cs
 * @author  : rudals252
 * @brief   : 코드에서 등록하는 조치와 닫힌 대상 형식·세션·일회성 계획·실행 결과 계약
 */
namespace PcOptimizer.Core.Actions;

/// <summary>자동 조치의 코드 허용 목록입니다. 문자열 명령을 받지 않습니다.</summary>
public enum ActionId { UserFiles, SystemFiles, AppFiles, Startup, Power, Display, OfficialCache, DeliveryOptimization, MachineStartup, UpdateServices, WindowsUpdateCache, SteamShaderCache }
/// <summary>현재 사용자 확인이 필요한지 구분합니다.</summary>
public enum ActionScope { CurrentUser, System }
/// <summary>실행 인스턴스의 사용자 범위입니다.</summary>
public enum ActionUserScope { Full, SystemOnly, Unknown }
/// <summary>승인 허용 목록의 공식 캐시 도구입니다.</summary>
public enum OfficialCacheTool { Npm, Pip, NuGetHttp }
/// <summary>계획이 귀속된 실제 SID·로그온 세션·사용자 범위입니다. 리포트 익명 ID를 사용하지 않습니다.</summary>
public sealed record ActionSession(string Sid, int SessionId, ActionUserScope Scope)
{
    /// <summary>확인 불가 세션에서 계획을 발급하지 않습니다.</summary>
    public bool IsKnown => !string.IsNullOrWhiteSpace(Sid) && SessionId >= 0 && Scope is ActionUserScope.Full or ActionUserScope.SystemOnly;
}
/// <summary>조치 대상의 닫힌 합집합입니다. 다른 어셈블리는 대상 형식을 추가할 수 없습니다.</summary>
public abstract record ActionTarget
{
    private protected ActionTarget() { }
    /// <summary>허용된 파일 집합을 식별하는 키입니다. 파일 스냅샷은 어댑터가 계획 준비 시 고정합니다.</summary>
    public sealed record Files(string CatalogKey) : ActionTarget;
    /// <summary>명령 본문이 아닌 시작 항목의 출처와 이름입니다.</summary>
    public sealed record Startup(string SourceKey, string ValueName) : ActionTarget;
    /// <summary>현재 설치된 전원 계획의 식별자입니다.</summary>
    public sealed record Power(Guid SchemeId) : ActionTarget;
    /// <summary>현재 연결 장치의 모드 식별자입니다. 원문 명령으로 해석하지 않습니다.</summary>
    public sealed record Display(string DeviceKey, string ModeKey) : ActionTarget;
    /// <summary>등록된 공식 도구 캐시입니다.</summary>
    public sealed record OfficialTool(OfficialCacheTool Tool) : ActionTarget;
    /// <summary>공식 배달 최적화 어댑터 대상입니다.</summary>
    public sealed record DeliveryCache : ActionTarget;
    /// <summary>검증된 저장소 기록 ID입니다. 본문/명령/설정 경로를 UI에서 받지 않습니다.</summary>
    public sealed record Restore(Guid RecordId) : ActionTarget;
}
/// <summary>신뢰된 코드에서 등록하는 조치 정의입니다.</summary>
public sealed record ActionDefinition(ActionId Id, ActionScope Scope, bool SupportsRestore = false)
{
    /// <summary>현재 버전은 항목별 확인만 허용하며 일괄 자동 실행은 제공하지 않습니다.</summary>
    public bool BatchEligible => false;
    /// <summary>영구 파일 삭제/공식 purge는 되돌릴 수 없음, 설정 변경은 주의로 표시합니다.</summary>
    public PcOptimizer.Core.Models.SafetyLevel Safety => Id is ActionId.UserFiles or ActionId.SystemFiles or ActionId.AppFiles or ActionId.OfficialCache or ActionId.DeliveryOptimization or ActionId.WindowsUpdateCache or ActionId.SteamShaderCache
        ? PcOptimizer.Core.Models.SafetyLevel.Irreversible : PcOptimizer.Core.Models.SafetyLevel.Caution;
}
/// <summary>어댑터가 검증한 대상과 미리보기 설명입니다.</summary>
public sealed record ActionPreview(ActionTarget Target, string Summary, string Impact, ActionPreviewDetails? Details = null);
/// <summary>미리보기의 표시 근거입니다. 논리 크기를 실제 확보 공간으로 표현하지 않습니다.</summary>
public sealed record ActionPreviewDetails(string TargetLabel, string SafetyNote, long? EstimatedLogicalBytes = null, bool? RequiresRestart = null);
/// <summary>실행기는 ID로만 이 서비스의 원본 계획을 조회합니다. 외부에서 만든 계획 객체를 받지 않습니다.</summary>
public sealed record ActionPlan(Guid Id, ActionDefinition Definition, ActionPreview Preview, ActionSession Session, DateTimeOffset ExpiresAt, bool IsRestore);
/// <summary>계획 준비 성공 또는 실행 전 거절 사유입니다.</summary>
public sealed record ActionPreparation(ActionPlan? Plan, string? Code);
/// <summary>관측 결과와 실제 변경 시작 여부입니다. 성공·실패·거절을 코드로 구분합니다.</summary>
public sealed record ActionResult(Guid PlanId, bool Started, bool Succeeded, string Code, ActionEffect? Effect = null)
{
    /// <summary>기록한 서비스들의 원상태 재관측 결과입니다. null은 서비스 복구 작업 없음입니다.</summary>
    public bool? ServiceRecoveryCompleted { get; init; }
}
/// <summary>관측한 여유 공간 변화입니다. null은 확인 불가, 음수는 여유 공간 감소입니다.</summary>
public sealed record ActionEffect(long? FreeSpaceDeltaBytes, int? ChangedFiles = null, int? SkippedFiles = null, int? FailedFiles = null,
    long? BeforeLogicalBytes = null, long? AfterLogicalBytes = null);

/// <summary>어댑터의 변경 직전 재검증·시작 기록과 실제 자식 작업 추적 계약입니다.</summary>
public interface IActionExecution
{
    /// <summary>변경 직전에 한 번 호출합니다. 취소·만료·세션 변경이면 예외로 거절합니다.</summary>
    void MarkStarted();
    /// <summary>프로세스 시작 직전 검증하고 실제 Start 성공 후에만 시작을 기록합니다.</summary>
    bool TryStartProcess(Func<bool> start) => throw new NotSupportedException("ProcessStartBoundaryUnavailable");
    /// <summary>반환 후에도 살아 있을 수 있는 실제 자식 작업을 등록합니다.</summary>
    void Track(Task task);
}
/// <summary>코드로 등록한 어댑터만 실행합니다. 신규 실제 변경 어댑터는 후속 Task에서 구현합니다.</summary>
public interface IActionAdapter
{
    /// <summary>지원하는 조치와 범위입니다.</summary>
    ActionDefinition Definition { get; }
    /// <summary>대상 재식별과 보호 정책을 검증한 미리보기입니다. null이면 거절합니다.</summary>
    Task<ActionPreview?> PrepareAsync(ActionTarget target, bool restore, CancellationToken ct);
    /// <summary>대상을 재검증하고 변경 직전 MarkStarted를 호출한 뒤 적용·재관측합니다.</summary>
    Task<ActionResult> ExecuteAsync(ActionPlan plan, IActionExecution execution, CancellationToken ct);
    /// <summary>반환 뒤에도 살아 있는 네이티브 작업의 실제 종료를 기다립니다.</summary>
    Task WaitForDrainAsync() => Task.CompletedTask;
}
