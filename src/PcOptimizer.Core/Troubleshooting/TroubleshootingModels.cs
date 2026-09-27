/**
 * @file    : TroubleshootingModels.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함(스펙 §5A)의 도구·증상·카탈로그 모델. 실행 방식은 직접 실행·내장 도구 열기·외부 도구 안내 세 가지로 닫혀 있다
 */
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Troubleshooting;

/// <summary>도구 실행 방식입니다. 앱은 이 세 가지 밖의 실행을 하지 않습니다.</summary>
public enum ToolMode
{
    /// <summary>Windows 내장 명령을 고정 인자로 직접 실행하고 출력을 보여 줍니다.</summary>
    DirectCommand,
    /// <summary>Windows 내장 GUI 도구나 설정 화면을 엽니다.</summary>
    BuiltInTool,
    /// <summary>외부 도구는 공식 배포처 링크와 한국어 절차만 제공합니다.</summary>
    ExternalGuide,
}

/// <summary>
/// 도구 한 항목입니다. <see cref="Command"/>·<see cref="OpenTarget"/>·<see cref="LinkId"/>는 실행 방식에 맞는 하나만 채워집니다.
/// </summary>
/// <param name="Id">고유 ID.</param>
/// <param name="Category">분류(표시용).</param>
/// <param name="Name">표시 이름.</param>
/// <param name="When">"언제 쓰나" 한 줄.</param>
/// <param name="What">"무엇을 하나" 한 줄.</param>
/// <param name="Caution">"주의" 한 줄.</param>
/// <param name="Mode">실행 방식.</param>
/// <param name="Safety">안전 수준.</param>
/// <param name="Steps">사용 절차(번호 목록).</param>
/// <param name="Warning">절차 첫 줄에 두는 경고(선택).</param>
/// <param name="Command">직접 실행 명령 ID(<see cref="RepairCommandCatalog"/>).</param>
/// <param name="OpenTarget">내장 도구 ID(<see cref="BuiltInToolCatalog"/>).</param>
/// <param name="LinkId">외부 도구의 공식 링크 항목 ID(vendor-links.json).</param>
/// <param name="RebootRequired">완료 후 다시 시작이 필요한지.</param>
/// <param name="ExtraLinks">보조 링크(예: 한국어 안내 블로그). 공식 링크 표의 항목 ID와 버튼 문구. 공식 배포처가 아니라는 뜻으로 별도 버튼에 둔다.</param>
public sealed record TroubleshootingTool(
    string Id,
    string Category,
    string Name,
    string When,
    string What,
    string Caution,
    ToolMode Mode,
    SafetyLevel Safety,
    IReadOnlyList<string> Steps,
    string? Warning,
    string? Command,
    string? OpenTarget,
    string? LinkId,
    bool RebootRequired,
    IReadOnlyList<ExtraLink> ExtraLinks);

/// <summary>도구 카드의 보조 링크입니다.</summary>
/// <param name="LinkId">공식 링크 표 항목 ID.</param>
/// <param name="Label">버튼 문구.</param>
public sealed record ExtraLink(string LinkId, string Label);

/// <summary>증상 절차의 한 단계입니다.</summary>
/// <param name="ToolId">도구 ID.</param>
/// <param name="Note">이 증상에서 이 도구를 쓰는 이유 한 줄.</param>
public sealed record SymptomStep(string ToolId, string Note);

/// <summary>증상 진입 항목입니다.</summary>
/// <param name="Id">고유 ID.</param>
/// <param name="Title">증상 제목(사용자 말투).</param>
/// <param name="Summary">권장 순서 요약 한 줄.</param>
/// <param name="Steps">권장 순서의 도구 단계.</param>
public sealed record Symptom(string Id, string Title, string Summary, IReadOnlyList<SymptomStep> Steps);

/// <summary>검증을 통과한 카탈로그입니다. 파서만 만듭니다.</summary>
public sealed class TroubleshootingCatalog
{
    private readonly Dictionary<string, TroubleshootingTool> _tools;

    internal TroubleshootingCatalog(IReadOnlyList<TroubleshootingTool> tools, IReadOnlyList<Symptom> symptoms)
    {
        Tools = [.. tools];
        Symptoms = [.. symptoms];
        _tools = Tools.ToDictionary(t => t.Id, StringComparer.Ordinal);
    }

    /// <summary>도구(파일 순서).</summary>
    public IReadOnlyList<TroubleshootingTool> Tools { get; }

    /// <summary>증상(파일 순서).</summary>
    public IReadOnlyList<Symptom> Symptoms { get; }

    /// <summary>ID로 도구를 찾습니다. 없으면 null.</summary>
    public TroubleshootingTool? FindTool(string? id) => id is not null && _tools.TryGetValue(id, out var tool) ? tool : null;
}

/// <summary>해석 결과입니다. 오류가 하나라도 있으면 카탈로그는 null입니다.</summary>
/// <param name="Catalog">카탈로그(실패하면 null).</param>
/// <param name="Errors">오류 코드 목록(성공하면 빈 목록).</param>
public sealed record TroubleshootingParseResult(TroubleshootingCatalog? Catalog, IReadOnlyList<string> Errors);
