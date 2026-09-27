/**
 * @file    : FindingCardViewModel.cs
 * @author  : rudals252
 * @brief   : Finding 카드 표시 모델(제목·설명 3줄·안전/판정 배지·측정값·근거·권장/조건·영향·확인 불가 사유)과 자세히 보기·설정 열기·허용된 공식 링크 열기·유지·비활성 적용 동작
 */

// 서드파티 패키지
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.App.Services;
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// Finding 카드 하나의 표시 모델입니다.
/// <list type="bullet">
/// <item>판정은 색이 아니라 배지 텍스트(<see cref="VerdictText"/>)로도 읽을 수 있습니다.</item>
/// <item>안전 수준도 색과 함께 항상 배지 텍스트(<see cref="SafetyText"/>)로 보여 줍니다.</item>
/// <item>원시 근거·부작용 문장은 카드 본문이 아니라 자세히 보기 패널에서만 보여 줍니다.</item>
/// <item>[설정 열기]는 허용 목록에 있는 설정 URI가 있을 때만 제공하고, 없으면 수동 경로 안내를 보여 줍니다.</item>
/// <item>공식 링크 버튼은 링크 정책(<see cref="LinkPolicy"/>)을 통과한 OpenLink만 보여 주며, 누를 때 다시 확인한 뒤 엽니다(자동으로 열지 않음).</item>
/// <item>[유지]는 현재 검사에서 카드를 접는 UI 동작일 뿐이며 설정 변경·영구 제외가 아닙니다(다음 검사에서 새 카드가 만들어짐).</item>
/// <item>연결된 주사율 실행기가 있으면 해당 모니터의 시험 준비 버튼을 제공하며, 실제 변경은 별도 확인 뒤에만 시작합니다.</item>
/// </list>
/// </summary>
public sealed partial class FindingCardViewModel : ObservableObject
{
    private readonly SettingsUriPolicy _settingsPolicy;
    private readonly LinkPolicy _linkPolicy;

    /// <summary>자세히 보기 패널을 펼쳤는지 여부.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailsButtonText))]
    private bool _isDetailsVisible;

    /// <summary>현재 검사에서 권고를 접었는지 여부.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBodyVisible))]
    private bool _isKept;

    /// <summary>링크를 열지 못했을 때의 안내(없으면 null).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLinkStatus))]
    private string? _linkStatusText;

    /// <summary>
    /// Finding으로 카드 모델을 만듭니다.
    /// </summary>
    /// <param name="finding">표시할 Finding.</param>
    /// <param name="settingsPolicy">설정 URI 허용 정책.</param>
    /// <param name="linkPolicy">외부 링크 허용 정책.</param>
    public FindingCardViewModel(Finding finding, SettingsUriPolicy settingsPolicy, LinkPolicy linkPolicy, bool displayTrialsAvailable = false, bool adobeCleanupAvailable = false, PcOptimizer.Core.Actions.ActionTarget.Startup? startupTarget = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ArgumentNullException.ThrowIfNull(settingsPolicy);
        ArgumentNullException.ThrowIfNull(linkPolicy);

        Finding = finding;
        StartupTarget = finding.Category == FindingCategory.Startup && startupTarget is not null
            && finding.Id == PcOptimizer.Core.Rules.StartupItemsRule.FINDING_ID_PREFIX + startupTarget.SourceKey + ":" + startupTarget.ValueName ? startupTarget : null;
        _settingsPolicy = settingsPolicy;
        _linkPolicy = linkPolicy;
        var displayTarget = displayTrialsAvailable ? DisplayFindingTarget.Read(finding) : null;
        CanTrialDisplay = displayTarget is not null;
        DisplayTrialButtonText = displayTarget is null ? "주사율 시험 적용" : $"{displayTarget.Hz}Hz 시험 적용";
        CanPrepareAdobe = adobeCleanupAvailable && finding.Category == FindingCategory.AppCache
            && finding.Verdict is Verdict.Info or Verdict.Candidate
            && finding.Id == PcOptimizer.Core.Rules.AppCacheRule.FINDING_ID_PREFIX + "Adobe 미디어 캐시";
        Measurements = [.. finding.Measured.Select(m => new MeasurementItemViewModel(m))];
        VerdictText = DisplayText.Verdict(finding.Verdict);
        CategoryText = DisplayText.Category(finding.Category);
        ReasonText = finding.CannotVerifyReason is { } reason ? DisplayText.Reason(reason) : null;
        SettingsUri = finding.Actions.OfType<OpenSettingsAction>()
            .Select(action => action.Uri)
            .FirstOrDefault(SettingsUriPolicy.IsAllowed);
        OpenLinkAction[] linkActions = [.. finding.Actions.OfType<OpenLinkAction>()];
        Links = [.. linkActions
            .Where(action => linkPolicy.IsAllowed(action.Url))
            .Select(action => new LinkButtonViewModel(
                string.IsNullOrWhiteSpace(action.Label) ? Strings.Button_OpenLinkDefault : action.Label, action.Url, OpenLink))];
        HiddenLinkCount = linkActions.Length - Links.Count;
        HasKeepAction = finding.Actions.Any(action => action is KeepAction);
        HasApplyAction = finding.Actions.Any(action => action is ApplyAction);
        CanShowDetails = HasEvidence || HasDetail || HasImpact || Measurements.Count > 0 || finding.Actions.Any(action => action is ShowDetailsAction);
    }

    /// <summary>원본 Finding.</summary>
    public Finding Finding { get; }
    /// <summary>실행기와 정확한 대상 측정값이 있을 때만 카드에서 시험 준비를 제공합니다.</summary>
    public bool CanTrialDisplay { get; }
    /// <summary>선택할 주사율을 버튼에서 명시합니다. 누르면 실제 사전 검사와 확인 화면으로 이동합니다.</summary>
    public string DisplayTrialButtonText { get; }
    /// <summary>관측 크기를 삭제량으로 간주하지 않고 기본 미디어 캐시를 새로 확인합니다.</summary>
    public bool CanPrepareAdobe { get; }
    /// <summary>현재 검사의 허용 목록과 일치하는 개별 Run 등록 대상입니다.</summary>
    public PcOptimizer.Core.Actions.ActionTarget.Startup? StartupTarget { get; }
    /// <summary>선택한 등록의 확인 화면으로 바로 연결할 수 있는지 여부입니다.</summary>
    public bool CanPrepareStartup => StartupTarget is not null;

    /// <summary>한 문장 제목.</summary>
    public string Title => Finding.Title;

    /// <summary>판정.</summary>
    public Verdict Verdict => Finding.Verdict;

    /// <summary>개선 후보 카드인지 여부.</summary>
    public bool IsCandidate => Verdict == Verdict.Candidate;

    /// <summary>판정 배지 텍스트.</summary>
    public string VerdictText { get; }

    /// <summary>분류 이름.</summary>
    public string CategoryText { get; }

    /// <summary>설명 3줄이 있는지 여부.</summary>
    public bool HasExplanation => Finding.Explanation is not null;

    /// <summary>"이게 뭔가요" 한 줄.</summary>
    public string? ExplainWhat => Finding.Explanation?.What;

    /// <summary>효과 한 줄.</summary>
    public string? ExplainEffect => Finding.Explanation?.Effect;

    /// <summary>주의 한 줄.</summary>
    public string? ExplainCaution => Finding.Explanation?.Caution;

    /// <summary>안전 수준(없으면 null).</summary>
    public SafetyLevel? Safety => Finding.Safety;

    /// <summary>안전 배지가 있는지 여부.</summary>
    public bool HasSafety => Finding.Safety is not null;

    /// <summary>안전 배지 텍스트(색과 무관하게 항상 표시).</summary>
    public string? SafetyText => Finding.Safety switch
    {
        SafetyLevel.Safe => Strings.Safety_Safe,
        SafetyLevel.Caution => Strings.Safety_Caution,
        SafetyLevel.Irreversible => Strings.Safety_Irreversible,
        _ => null,
    };

    /// <summary>측정값 목록.</summary>
    public IReadOnlyList<MeasurementItemViewModel> Measurements { get; }

    /// <summary>측정값이 있는지 여부.</summary>
    public bool HasMeasurements => Measurements.Count > 0;

    /// <summary>측정값이 없는지 여부(상세 패널의 빈 안내 표시용).</summary>
    public bool HasNoMeasurements => Measurements.Count == 0;

    /// <summary>근거.</summary>
    public string Evidence => Finding.Evidence;

    /// <summary>근거가 있는지 여부.</summary>
    public bool HasEvidence => !string.IsNullOrWhiteSpace(Finding.Evidence);

    /// <summary>확인 불가 사유 문자열(CannotVerify가 아니면 null).</summary>
    public string? ReasonText { get; }

    /// <summary>확인 불가 사유가 있는지 여부.</summary>
    public bool HasReason => ReasonText is not null;

    /// <summary>사용자용 상세 사유.</summary>
    public string? Detail => Finding.Detail;

    /// <summary>상세 사유가 있는지 여부.</summary>
    public bool HasDetail => !string.IsNullOrWhiteSpace(Finding.Detail);

    /// <summary>권장 문장.</summary>
    public string? RecommendationText => Finding.Recommendation?.Text;

    /// <summary>권장 조건 문장("조건: …").</summary>
    public string? ConditionText =>
        Finding.Recommendation is { } recommendation ? DisplayText.Format(Strings.Card_ConditionFormat, recommendation.Condition) : null;

    /// <summary>권장이 있는지 여부.</summary>
    public bool HasRecommendation => Finding.Recommendation is not null;

    /// <summary>기대 효과 문장.</summary>
    public string? BenefitText => Finding.Impact is { } impact ? DisplayText.Format(Strings.Card_BenefitFormat, impact.Benefit) : null;

    /// <summary>부작용 문장(자세히 보기 패널에서만 표시).</summary>
    public string? SideEffectText => Finding.Impact is { } impact ? DisplayText.Format(Strings.Card_SideEffectFormat, impact.SideEffect) : null;

    /// <summary>영향이 있는지 여부.</summary>
    public bool HasImpact => Finding.Impact is not null;

    /// <summary>자세히 보기를 제공하는지 여부.</summary>
    public bool CanShowDetails { get; }

    /// <summary>자세히 보기 버튼 문구.</summary>
    public string DetailsButtonText => IsDetailsVisible ? Strings.Button_HideDetails
        : Finding.Id.StartsWith("video-editor-cache:", StringComparison.Ordinal) ? "캐시 위치·정리 방법 보기"
        : Finding.Id.StartsWith("shader-cache:", StringComparison.Ordinal) ? "캐시별 용량·주의사항 보기" : Strings.Button_ShowDetails;

    /// <summary>허용 목록에 있는 설정 URI(없으면 null).</summary>
    public string? SettingsUri { get; }

    /// <summary>[설정 열기]를 제공하는지 여부.</summary>
    public bool CanOpenSettings => SettingsUri is not null;

    /// <summary>설정 URI가 없어 수동 경로 안내를 보여야 하는지 여부(권장이 있을 때만).</summary>
    public bool ShowManualPathHint => HasRecommendation && !CanOpenSettings && !HasLinks;

    /// <summary>효과를 검증하지 않은 정보 카드에는 효과 배너를 붙이지 않습니다.</summary>
    public bool HasCandidateBenefit => Verdict == Verdict.Candidate && !string.IsNullOrWhiteSpace(Finding.Impact?.Benefit);
    /// <summary>후보에 표시할 기대 효과 원문.</summary>
    public string? CandidateBenefit => Finding.Impact?.Benefit;
    /// <summary>설정 변경·공식 안내·확인의 동작 종류.</summary>
    public string ActionModeText => CanTrialDisplay ? "앱에서 사전 확인 후 시험 적용할 수 있습니다."
        : CanPrepareAdobe ? "기본 캐시 중 90일 이상 된 파일만 다시 확인합니다. 카드의 전체 관측 크기와 정리량은 다릅니다."
        : CanOpenSettings ? Strings.Overview_ManualSetting
        : HasLinks ? Strings.Overview_OfficialGuide : Strings.Overview_ReviewFirst;
    /// <summary>분류에 맞는 설정 버튼 문구.</summary>
    public string SettingsButtonText => Finding.Category switch
    {
        FindingCategory.Display => Strings.Overview_DisplaySettings,
        FindingCategory.Power => Strings.Overview_PowerSettings,
        FindingCategory.Driver => Strings.Overview_UpdateSettings,
        FindingCategory.Storage or FindingCategory.AppCache => Strings.Cleanup_OpenWindows,
        _ => Strings.Button_OpenSettings,
    };

    /// <summary>허용 목록을 통과한 공식 링크 버튼(OpenLink 순서).</summary>
    public IReadOnlyList<LinkButtonViewModel> Links { get; }

    /// <summary>공식 링크 버튼이 있는지 여부.</summary>
    public bool HasLinks => Links.Count > 0;

    /// <summary>허용 목록에 없어 표시하지 않은 링크 수.</summary>
    public int HiddenLinkCount { get; }

    /// <summary>표시하지 않은 링크 안내(없으면 null).</summary>
    public string? HiddenLinksNote => HiddenLinkCount > 0 ? DisplayText.Format(Strings.Card_LinksHiddenFormat, HiddenLinkCount) : null;

    /// <summary>표시하지 않은 링크가 있는지 여부.</summary>
    public bool HasHiddenLinks => HiddenLinkCount > 0;

    /// <summary>링크 열기 안내가 있는지 여부.</summary>
    public bool HasLinkStatus => LinkStatusText is not null;

    /// <summary>[유지]를 제공하는지 여부.</summary>
    public bool HasKeepAction { get; }

    /// <summary>[적용](비활성)을 보여 주는지 여부.</summary>
    public bool HasApplyAction { get; }

    /// <summary>[적용] 설명(자동 조치는 다음 버전 예정).</summary>
    public string ApplyTooltip => Strings.Tooltip_Apply;

    /// <summary>[적용]은 1차에서 항상 비활성입니다.</summary>
    public bool IsApplyEnabled => false;

    /// <summary>카드 본문(측정값·근거·권장·영향·버튼)을 보여 주는지 여부.</summary>
    public bool IsBodyVisible => !IsKept;

    /// <summary>
    /// 자세히 보기 패널을 펼치거나 접습니다.
    /// </summary>
    [RelayCommand]
    private void ToggleDetails()
    {
        IsDetailsVisible = !IsDetailsVisible;
    }

    /// <summary>
    /// 허용된 설정 화면을 엽니다.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenSettings))]
    private void OpenSettings()
    {
        _settingsPolicy.TryOpen(SettingsUri);
    }

    /// <summary>
    /// 링크 정책으로 다시 확인한 뒤 공식 링크를 엽니다. 거부하면 안내를, 열지 못하면(비승격 셸 시작 실패) 주소를 복사해 붙여 넣으라는 안내와 주소를 보여 줍니다.
    /// </summary>
    /// <param name="url">링크 URL(허용 목록을 통과한 링크만 버튼으로 만들어짐).</param>
    private void OpenLink(string url)
    {
        LinkStatusText = _linkPolicy.TryOpen(url) switch
        {
            LinkOpenResult.Opened => null,
            LinkOpenResult.Refused => Strings.Link_Refused,
            _ => DisplayText.Format(Strings.Link_OpenFailedCopyFormat, new Uri(url, UriKind.Absolute).AbsoluteUri),
        };
    }

    /// <summary>
    /// 현재 검사에서 권고를 접습니다(설정 변경 없음).
    /// </summary>
    [RelayCommand]
    private void Keep()
    {
        IsKept = true;
        IsDetailsVisible = false;
    }

    /// <summary>
    /// 접은 카드를 다시 펼칩니다.
    /// </summary>
    [RelayCommand]
    private void Unkeep()
    {
        IsKept = false;
    }
}
