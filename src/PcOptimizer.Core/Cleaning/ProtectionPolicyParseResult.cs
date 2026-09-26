/**
 * @file    : ProtectionPolicyParseResult.cs
 * @author  : rudals252
 * @brief   : 보호 정책 파싱 결과(유효 정책 또는 실패 코드와 문제 항목 번호) 레코드
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 보호 정책 파싱 결과입니다. 유효하면 <see cref="Policy"/>가 있고, 무효이면 <see cref="Error"/>와(항목 오류일 때) <see cref="EntryIndex"/>가 있습니다.
/// </summary>
/// <param name="Policy">유효한 정책(무효이면 null).</param>
/// <param name="Error">실패 코드(유효하면 None).</param>
/// <param name="EntryIndex">문제가 된 protectedRoots 항목 번호(0부터, 항목 오류가 아니면 null).</param>
public sealed record ProtectionPolicyParseResult(ProtectionPolicy? Policy, ProtectionPolicyError Error, int? EntryIndex)
{
    /// <summary>유효한 정책인지 여부.</summary>
    public bool IsValid => Policy is not null && Error == ProtectionPolicyError.None;

    /// <summary>
    /// 유효 결과를 만듭니다.
    /// </summary>
    /// <param name="policy">정책.</param>
    /// <returns>결과.</returns>
    public static ProtectionPolicyParseResult Valid(ProtectionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return new ProtectionPolicyParseResult(policy, ProtectionPolicyError.None, null);
    }

    /// <summary>
    /// 무효 결과를 만듭니다.
    /// </summary>
    /// <param name="error">실패 코드.</param>
    /// <param name="entryIndex">문제 항목 번호.</param>
    /// <returns>결과.</returns>
    public static ProtectionPolicyParseResult Invalid(ProtectionPolicyError error, int? entryIndex = null)
    {
        return new ProtectionPolicyParseResult(null, error, entryIndex);
    }
}
