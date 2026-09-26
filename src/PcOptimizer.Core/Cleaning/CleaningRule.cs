/**
 * @file    : CleaningRule.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 INI에서 해석한 정리 규칙 하나(탐지 키·파일 탐지·FileKey·ExcludeKey·RegKey 개수·경고 여부·미지원 사유)와 구성 요소 레코드. 조회 범위 계산용이며 삭제 기능과 연결하지 않는다
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 레지스트리 탐지 키 하나입니다(키 존재만 확인).
/// </summary>
/// <param name="Hive">루트.</param>
/// <param name="SubKey">하위 키 경로.</param>
public sealed record RegistryDetectSpec(RuleRegistryHive Hive, string SubKey);

/// <summary>
/// FileKey 하나입니다. 경로는 환경 변수·와일드카드가 든 템플릿 그대로이며, 해석은 Probes가 합니다.
/// </summary>
/// <param name="PathTemplate">폴더 경로 템플릿(끝 구분자 제거).</param>
/// <param name="Patterns">파일 이름 패턴(세미콜론 구분을 나눈 목록, 대소문자 무시).</param>
/// <param name="Recurse">하위 폴더까지 보는지 여부(RECURSE 또는 REMOVESELF).</param>
/// <param name="RemoveSelf">원본에 REMOVESELF가 있었는지 여부. 폴더 자체 삭제 의미는 조회에 쓰지 않고 기록만 합니다.</param>
public sealed record FileKeySpec(string PathTemplate, IReadOnlyList<string> Patterns, bool Recurse, bool RemoveSelf);

/// <summary>
/// ExcludeKey 하나(FILE/PATH)입니다.
/// </summary>
/// <param name="Kind">제외 종류.</param>
/// <param name="PathTemplate">폴더 경로 템플릿(와일드카드 가능, 끝 구분자 제거).</param>
/// <param name="Patterns">파일 이름 패턴.</param>
public sealed record ExcludeKeySpec(ExcludeKind Kind, string PathTemplate, IReadOnlyList<string> Patterns);

/// <summary>
/// 정리 규칙 하나입니다. 1차에서는 파일 관측 범위를 계산하는 데만 쓰며, RegKey·REMOVESELF 같은 삭제·레지스트리 효과는 실행하지 않습니다.
/// </summary>
/// <param name="Id">규칙 ID(출처 접두사 + 섹션 이름, 예: "winapp2:Discord").</param>
/// <param name="Name">표시 이름(섹션 이름에서 " *" 제거).</param>
/// <param name="Origin">출처.</param>
/// <param name="Section">분류(Section 값, 없으면 LangSecRef 값, 둘 다 없으면 null). 앱 카드 묶음에는 <see cref="AppGroup"/>를 씁니다.</param>
/// <param name="DetectKeys">레지스트리 탐지 키(여러 개면 OR).</param>
/// <param name="DetectFiles">파일·폴더 탐지 경로 템플릿(여러 개면 OR, 와일드카드 가능).</param>
/// <param name="FileKeys">관측 대상.</param>
/// <param name="ExcludeKeys">제외 대상.</param>
/// <param name="RegKeyCount">RegKey 개수(지원하지 않는 효과로 기록만 함).</param>
/// <param name="HasWarning">원본에 Warning 문구가 있는지 여부.</param>
/// <param name="UnsupportedReason">미지원 사유(지원하면 null).</param>
/// <param name="UnsupportedDetail">미지원 사유의 보조 정보(예: 알 수 없는 키 이름, 변수 이름). 사용자 경로를 담지 않습니다.</param>
public sealed record CleaningRule(
    string Id,
    string Name,
    RuleOrigin Origin,
    string? Section,
    IReadOnlyList<RegistryDetectSpec> DetectKeys,
    IReadOnlyList<string> DetectFiles,
    IReadOnlyList<FileKeySpec> FileKeys,
    IReadOnlyList<ExcludeKeySpec> ExcludeKeys,
    int RegKeyCount,
    bool HasWarning,
    UnsupportedRuleReason? UnsupportedReason,
    string? UnsupportedDetail)
{
    /// <summary>규칙 ID의 커뮤니티 접두사.</summary>
    public const string COMMUNITY_ID_PREFIX = "winapp2:";

    /// <summary>규칙 ID의 보충 규칙 접두사.</summary>
    public const string SUPPLEMENT_ID_PREFIX = "supplement:";

    /// <summary>지원하는 규칙인지 여부.</summary>
    public bool IsSupported => UnsupportedReason is null;

    /// <summary>
    /// 앱 카드 묶음 기준입니다. winapp2 <c>Section=</c> 텍스트 값(예: "Google Chrome Web Browser")이 있고 숫자가 아니면 그 값,
    /// 아니면 규칙 이름(끝의 " *" 제거)입니다. 숫자 <c>LangSecRef</c>·숫자 Section 값은 앱 이름이 아닌 분류 번호라 묶음 기준으로 쓰지 않습니다.
    /// </summary>
    public string AppGroup => Section is { } section && !string.IsNullOrWhiteSpace(section) && !section.Trim().All(char.IsAsciiDigit) ? section.Trim() : Name;

    /// <summary>
    /// 출처와 섹션 이름으로 규칙 ID를 만듭니다.
    /// </summary>
    /// <param name="origin">출처.</param>
    /// <param name="name">섹션 이름(" *" 제거).</param>
    /// <returns>규칙 ID.</returns>
    public static string CreateId(RuleOrigin origin, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return (origin == RuleOrigin.Supplement ? SUPPLEMENT_ID_PREFIX : COMMUNITY_ID_PREFIX) + name;
    }
}
