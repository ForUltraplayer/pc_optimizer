/**
 * @file    : ProtectionPolicy.cs
 * @author  : rudals252
 * @brief   : 검증을 통과한 보호 정책(스키마 버전, 보호 루트 항목 목록) 불변 모델
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 검증을 통과한 보호 정책입니다. <see cref="ProtectionPolicyParser"/>가 만들며, 보호 루트의 내용은 1차에서 순회하지 않습니다(스펙 §5.2).
/// </summary>
public sealed class ProtectionPolicy
{
    /// <summary>
    /// 보호 정책을 만듭니다.
    /// </summary>
    /// <param name="schemaVersion">스키마 버전.</param>
    /// <param name="roots">보호 루트 항목(복사해 보관).</param>
    public ProtectionPolicy(int schemaVersion, IEnumerable<ProtectedRootSpec> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        SchemaVersion = schemaVersion;
        Roots = Array.AsReadOnly(roots.ToArray());
    }

    /// <summary>스키마 버전.</summary>
    public int SchemaVersion { get; }

    /// <summary>보호 루트 항목(정책 파일 순서, 읽기 전용).</summary>
    public IReadOnlyList<ProtectedRootSpec> Roots { get; }
}
