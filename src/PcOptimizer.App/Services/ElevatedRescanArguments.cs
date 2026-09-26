/**
 * @file    : ElevatedRescanArguments.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 재검사 인스턴스에 넘기는 고정 인자(--elevated-rescan --origin-sid SID --context GUID)의 생성과 엄격한 해석(그 밖의 인자는 무시, 경로·명령으로 쓰지 않음)
 */

// 기본 패키지
using System.Globalization;
using System.Text.RegularExpressions;

namespace PcOptimizer.App.Services;

/// <summary>
/// 관리자 권한 재검사 인자입니다. 원래 창 사용자의 SID와 새 컨텍스트 ID만 담습니다.
/// </summary>
/// <remarks>
/// 승격된 인스턴스는 이 세 플래그만 해석합니다. 알 수 없는 인자는 무시하고 경로·명령·규칙 파일 등으로 쓰지 않습니다.
/// 플래그 누락·중복, SID·GUID 형식 오류는 재검사가 아닌 일반 시작(null)으로 처리합니다.
/// </remarks>
/// <param name="OriginSid">원래(일반 권한) 창을 실행한 사용자의 SID 문자열.</param>
/// <param name="ContextId">재검사 요청 컨텍스트 ID.</param>
public sealed partial record ElevatedRescanArguments(string OriginSid, Guid ContextId)
{
    /// <summary>재검사 표시 플래그.</summary>
    public const string FLAG_RESCAN = "--elevated-rescan";

    /// <summary>원래 사용자 SID 플래그.</summary>
    public const string FLAG_ORIGIN_SID = "--origin-sid";

    /// <summary>컨텍스트 ID 플래그.</summary>
    public const string FLAG_CONTEXT = "--context";

    /// <summary>컨텍스트 GUID 형식(하이픈 구분, 중괄호 없음).</summary>
    public const string CONTEXT_FORMAT = "D";

    /// <summary>SID 문자열 최대 길이(S-1-권한-하위 권한 15개).</summary>
    private const int MAX_SID_LENGTH = 184;
    private const int SID_REGEX_TIMEOUT_MS = 100;

    /// <summary>
    /// 명령줄 인자를 해석합니다.
    /// </summary>
    /// <param name="args">프로세스 인자(실행 파일 경로 제외).</param>
    /// <returns>재검사 인자. 형식이 맞지 않으면 null(일반 시작).</returns>
    public static ElevatedRescanArguments? Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var rescan = false;
        string? sid = null;
        Guid? context = null;
        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case FLAG_RESCAN:
                    if (rescan)
                    {
                        return null;
                    }

                    rescan = true;
                    break;
                case FLAG_ORIGIN_SID:
                    if (sid is not null || index + 1 >= args.Count || !IsValidSid(args[index + 1]))
                    {
                        return null;
                    }

                    sid = args[++index];
                    break;
                case FLAG_CONTEXT:
                    if (context is not null
                        || index + 1 >= args.Count
                        || !Guid.TryParseExact(args[index + 1], CONTEXT_FORMAT, out var parsed))
                    {
                        return null;
                    }

                    context = parsed;
                    index++;
                    break;
                default:
                    // 알 수 없는 인자는 무시한다(경로·명령·옵션으로 해석하지 않음).
                    break;
            }
        }

        return rescan && sid is not null && context is { } contextId
            ? new ElevatedRescanArguments(sid, contextId)
            : null;
    }

    /// <summary>
    /// 문자열이 SID 형식(S-1-권한-하위 권한…, 숫자만)인지 확인합니다.
    /// </summary>
    /// <param name="value">확인할 문자열.</param>
    /// <returns>SID 형식이면 true.</returns>
    public static bool IsValidSid(string? value)
    {
        return value is not null && value.Length <= MAX_SID_LENGTH && SidPattern().IsMatch(value);
    }

    /// <summary>
    /// 재검사 인스턴스에 넘길 명령줄을 만듭니다. SID·GUID만 넣으므로 공백·따옴표가 생기지 않습니다.
    /// </summary>
    /// <returns>명령줄 문자열.</returns>
    /// <exception cref="InvalidOperationException">SID 형식이 아닌 경우(인자 주입 방지).</exception>
    public string ToCommandLine()
    {
        if (!IsValidSid(OriginSid))
        {
            throw new InvalidOperationException("원래 사용자 SID 형식이 올바르지 않아 명령줄을 만들지 않습니다.");
        }

        return string.Join(
            ' ',
            FLAG_RESCAN,
            FLAG_ORIGIN_SID,
            OriginSid,
            FLAG_CONTEXT,
            ContextId.ToString(CONTEXT_FORMAT, CultureInfo.InvariantCulture));
    }

    /// <summary>SID 문자열 형식(S-1-식별자 권한-하위 권한 1~15개).</summary>
    [GeneratedRegex(@"^S-1-[0-9]{1,15}(-[0-9]{1,10}){1,15}\z", RegexOptions.CultureInvariant, SID_REGEX_TIMEOUT_MS)]
    private static partial Regex SidPattern();
}
