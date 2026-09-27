/**
 * @file    : RollbackRecord.cs
 * @author  : rudals252
 * @brief   : 명령을 포함하지 않는 복구 기록·값·저장소·코드 어댑터 계약
 */
namespace PcOptimizer.Core.Actions;

/// <summary>Pending/Restoring은 프로세스 중단 뒤 관측이 필요한 상태입니다.</summary>
public enum RollbackState { Pending, Applied, Restoring, Restored, Unchanged }
/// <summary>사용자 되돌리기와 반드시 수습해야 하는 내부 임시 변경을 구분합니다.</summary>
public enum RollbackPurpose { UserUndo, ServiceRecovery, TemporaryDisplay }
/// <summary>원래 값 없음·네이티브 형식·전체 바이트를 손실 없이 보존합니다.</summary>
public sealed record RollbackValue(bool Exists, int NativeType, byte[] Data)
{
    /// <summary>배열 참조가 아닌 실제 값으로 비교합니다.</summary>
    public bool SameAs(RollbackValue other) => Exists == other.Exists && NativeType == other.NativeType && Data.AsSpan().SequenceEqual(other.Data);
}
/// <summary>대상 키는 코드 어댑터가 재식별해야 하며 경로나 명령으로 직접 실행하지 않습니다.</summary>
public sealed record RollbackRecord(int Version, Guid Id, string Sid, ActionId ActionId, ActionScope Scope,
    string TargetKey, RollbackPurpose Purpose, RollbackValue Before, RollbackValue Applied,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, RollbackState State, int Revision)
{
    /// <summary>중단 또는 아직 끝나지 않은 내부 임시 변경입니다.</summary>
    public bool NeedsRecovery => State is RollbackState.Pending or RollbackState.Restoring
        || (State == RollbackState.Applied && Purpose != RollbackPurpose.UserUndo);
    /// <summary>완료 후 보관 기한이 지난 기록입니다. 해제된 시작 등록의 유일한 원본은 복원 전 자동 삭제하지 않습니다.</summary>
    public bool CanExpire => !NeedsRecovery && !(ActionId == ActionId.Startup && State == RollbackState.Applied);
}
/// <summary>손상 기록도 묵살하지 않고 ID/일반 사유로 알립니다. 본문과 개인 경로는 노출하지 않습니다.</summary>
public sealed record RollbackIssue(string FileName, string Code);
/// <summary>읽을 수 있는 기록과 확인 불가 기록입니다.</summary>
public sealed record RollbackCatalog(IReadOnlyList<RollbackRecord> Records, IReadOnlyList<RollbackIssue> Issues);
/// <summary>열린 동안 다른 앱 인스턴스의 복구/변경도 직렬화하는 저장소입니다.</summary>
public interface IRollbackStore
{
    /// <summary>현재 프로필·소유권·세션을 검사하고 독점 저장소 세션을 엽니다.</summary>
    IRollbackTransaction Open(ActionSession session);
}
/// <summary>실제 작업이 끝날 때까지 보존해야 하는 저장소 잠금입니다.</summary>
public interface IRollbackTransaction : IDisposable
{
    /// <summary>정확한 ID의 검증된 기록만 읽습니다.</summary>
    RollbackRecord? Read(Guid id);
    /// <summary>손상/다른 사용자/부분 파일을 별도 이슈로 반환합니다.</summary>
    RollbackCatalog ReadAll();
    /// <summary>신규 Pending 또는 직전 리비전에서 허용된 전이만 내구성 있게 저장합니다.</summary>
    void Save(RollbackRecord record);
    /// <summary>완료된 기록만 기준 시각 이전 것을 삭제합니다. 미완료 기록은 남깁니다.</summary>
    int PruneCompleted(DateTimeOffset olderThan);
}
/// <summary>신뢰된 코드가 현재 대상에서 수집한 변경 내용입니다.</summary>
public sealed record RollbackChange(string TargetKey, RollbackPurpose Purpose, RollbackValue Before, RollbackValue Applied);
/// <summary>복구 가능한 네이티브 조치의 코드 계약입니다. 사용자 JSON으로 등록하지 않습니다.</summary>
public interface IReversibleActionAdapter
{
    /// <summary>허용 조치 ID·사용자 범위입니다.</summary>
    ActionDefinition Definition { get; }
    /// <summary>적용 미리보기의 실제 대상을 확인합니다.</summary>
    Task<ActionPreview?> PrepareAsync(ActionTarget target, CancellationToken ct);
    /// <summary>현재 값과 적용할 값을 실제 대상에서 수집합니다.</summary>
    Task<RollbackChange> CaptureAsync(ActionPlan plan, CancellationToken ct);
    /// <summary>키/값 형식/정책을 코드 허용 목록에 대조한 뒤 대상을 다시 식별합니다.</summary>
    Task<bool> ValidateAsync(RollbackRecord record, CancellationToken ct);
    /// <summary>검증한 대상의 현재 원문 값을 다시 읽습니다.</summary>
    Task<RollbackValue> ReadCurrentAsync(RollbackRecord record, CancellationToken ct);
    /// <summary>변경 직전에 기대값을 재확인하고 일치할 때만 씁니다. 외부 앱과의 원자성은 네이티브 API 계약을 따릅니다.</summary>
    Task<bool> CompareExchangeAsync(RollbackRecord record, RollbackValue expected, RollbackValue desired, CancellationToken ct);
    /// <summary>취소/예외 뒤에도 실제 네이티브 변경이 끝날 때까지 기다립니다.</summary>
    Task WaitForDrainAsync() => Task.CompletedTask;
}
