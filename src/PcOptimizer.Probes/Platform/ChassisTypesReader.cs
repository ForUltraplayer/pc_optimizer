/**
 * @file    : ChassisTypesReader.cs
 * @author  : rudals252
 * @brief   : Win32_SystemEnclosure.ChassisTypes 코드를 모든 인클로저에서 모아 읽는 프로브 공용 도우미(전원·시스템 정보 프로브가 공유)
 */

// 기본 패키지
using System.Diagnostics.CodeAnalysis;

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 섀시 종류 코드 조회 도우미입니다. 조회 실패·빈 결과는 Issue로 돌려주며 빈 목록으로 숨기지 않습니다.
/// 코드 해석(노트북형/데스크톱형)은 Core의 ChassisClassifier가 맡습니다.
/// </summary>
internal static class ChassisTypesReader
{
    /// <summary>섀시 정보 WMI 클래스.</summary>
    public const string SYSTEM_ENCLOSURE_CLASS = "Win32_SystemEnclosure";

    /// <summary>섀시 코드 속성.</summary>
    public const string PROPERTY_CHASSIS_TYPES = "ChassisTypes";

    /// <summary>측정값 출처 문자열.</summary>
    public const string SOURCE = "WMI " + SYSTEM_ENCLOSURE_CLASS + "." + PROPERTY_CHASSIS_TYPES;

    private static readonly string[] ENCLOSURE_PROPERTIES = [PROPERTY_CHASSIS_TYPES];

    /// <summary>
    /// 섀시 코드를 읽습니다. 여러 인클로저가 있으면 모든 코드를 모읍니다.
    /// </summary>
    /// <param name="wmi">WMI 클라이언트.</param>
    /// <param name="timeout">제공자 타임아웃.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <param name="codes">숫자 문자열 코드 목록(실패하면 null).</param>
    /// <param name="issue">실패·빈 결과의 Issue(성공하면 null).</param>
    /// <returns>코드를 읽었으면 true.</returns>
    public static bool TryRead(
        IWmiClient wmi,
        TimeSpan timeout,
        CancellationToken ct,
        [NotNullWhen(true)] out IReadOnlyList<string>? codes,
        [NotNullWhen(false)] out Issue? issue)
    {
        var result = wmi.Query(SYSTEM_ENCLOSURE_CLASS, ENCLOSURE_PROPERTIES, timeout, ct);
        if (result.Status != WmiQueryStatus.Success)
        {
            codes = null;
            issue = WmiResultInterpreter.ToIssue(SYSTEM_ENCLOSURE_CLASS, result);
            return false;
        }

        string[] collected = [.. result.Rows.SelectMany(row => WmiResultInterpreter.GetUInt16ArrayAsText(row, PROPERTY_CHASSIS_TYPES) ?? [])];
        if (collected.Length == 0)
        {
            codes = null;
            issue = WmiResultInterpreter.EmptyIssue(SYSTEM_ENCLOSURE_CLASS);
            return false;
        }

        codes = collected;
        issue = null;
        return true;
    }
}
