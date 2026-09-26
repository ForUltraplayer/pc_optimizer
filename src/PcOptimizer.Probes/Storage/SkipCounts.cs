/**
 * @file    : SkipCounts.cs
 * @author  : rudals252
 * @brief   : 파일 순회에서 건너뛴 항목의 사유(보호 제외·접근 거부·사용 중/변경 중·reparse·클라우드 placeholder·시간 초과) 열거형과 사유별 개수 값
 */

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 순회에서 항목을 건너뛴 사유입니다.
/// </summary>
public enum ScanSkipReason
{
    /// <summary>보호 루트라서 들어가지 않음(내용 크기는 모름, 0바이트가 아님).</summary>
    ProtectedExcluded,

    /// <summary>접근 거부.</summary>
    AccessDenied,

    /// <summary>사용 중·열거 중 사라짐 등 변경 중이라 읽지 못함.</summary>
    InUse,

    /// <summary>reparse point(정션·심볼릭 링크 등)라서 따라가지 않음.</summary>
    Reparse,

    /// <summary>클라우드 placeholder(Offline·RecallOnOpen·RecallOnDataAccess)라서 건드리지 않음.</summary>
    Placeholder,

    /// <summary>볼륨 시간 예산을 넘겨 열거하지 못함.</summary>
    Timeout,
}

/// <summary>
/// 사유별 건너뛴 항목 수입니다.
/// </summary>
/// <param name="ProtectedExcluded">보호 제외.</param>
/// <param name="AccessDenied">접근 거부.</param>
/// <param name="InUse">사용 중·변경 중.</param>
/// <param name="Reparse">reparse point.</param>
/// <param name="Placeholder">클라우드 placeholder.</param>
/// <param name="Timeout">시간 초과로 열거하지 못한 디렉터리.</param>
public readonly record struct SkipCounts(
    long ProtectedExcluded,
    long AccessDenied,
    long InUse,
    long Reparse,
    long Placeholder,
    long Timeout)
{
    /// <summary>모든 사유의 합.</summary>
    public long Total => ProtectedExcluded + AccessDenied + InUse + Reparse + Placeholder + Timeout;

    /// <summary>읽으려 했지만 읽지 못한 항목 수(접근 거부 + 사용 중 + 시간 초과). 0보다 크면 부분 집계입니다.</summary>
    public long Incomplete => AccessDenied + InUse + Timeout;

    /// <summary>
    /// 사유 하나의 개수를 돌려줍니다.
    /// </summary>
    /// <param name="reason">사유.</param>
    /// <returns>개수.</returns>
    public long Of(ScanSkipReason reason)
    {
        return reason switch
        {
            ScanSkipReason.ProtectedExcluded => ProtectedExcluded,
            ScanSkipReason.AccessDenied => AccessDenied,
            ScanSkipReason.InUse => InUse,
            ScanSkipReason.Reparse => Reparse,
            ScanSkipReason.Placeholder => Placeholder,
            ScanSkipReason.Timeout => Timeout,
            _ => 0,
        };
    }

    /// <summary>
    /// 두 개수를 더합니다.
    /// </summary>
    /// <param name="other">더할 개수.</param>
    /// <returns>합.</returns>
    public SkipCounts Add(SkipCounts other)
    {
        return new SkipCounts(
            ProtectedExcluded + other.ProtectedExcluded,
            AccessDenied + other.AccessDenied,
            InUse + other.InUse,
            Reparse + other.Reparse,
            Placeholder + other.Placeholder,
            Timeout + other.Timeout);
    }

    /// <summary>
    /// 사유 하나를 1 늘린 값을 돌려줍니다.
    /// </summary>
    /// <param name="reason">사유.</param>
    /// <returns>늘린 값.</returns>
    public SkipCounts Increment(ScanSkipReason reason)
    {
        return reason switch
        {
            ScanSkipReason.ProtectedExcluded => this with { ProtectedExcluded = ProtectedExcluded + 1 },
            ScanSkipReason.AccessDenied => this with { AccessDenied = AccessDenied + 1 },
            ScanSkipReason.InUse => this with { InUse = InUse + 1 },
            ScanSkipReason.Reparse => this with { Reparse = Reparse + 1 },
            ScanSkipReason.Placeholder => this with { Placeholder = Placeholder + 1 },
            ScanSkipReason.Timeout => this with { Timeout = Timeout + 1 },
            _ => this,
        };
    }
}
