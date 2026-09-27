/**
 * @file    : MainViewModel.Overview.cs
 * @author  : rudals252
 * @brief   : 추천 조치·목적별 결과·바로 할 수 있는 것/직접 해야 하는 것 요약·온라인 비교 완료 판정(온라인 공급자와 온라인 규칙 결과만 사용)
 */
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.ViewModels;

/// <summary>목적별 추천·전체 결과와 정리 이력을 연결합니다.</summary>
public sealed partial class MainViewModel
{
    /// <summary>전체 결과 표시 여부.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCards), nameof(ResultsHeading), nameof(ResultsDescription), nameof(IsOverview), nameof(IsResultListEmpty), nameof(EmptyResultsText))]
    private bool _showAllResults;

    /// <summary>최근 실제 정리 실행 결과.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanupOutcome))]
    private CleanupOutcomeViewModel? _lastCleanupOutcome;

    /// <summary>최근 실제 정리 실행 결과가 있는지 여부.</summary>
    public bool HasCleanupOutcome => LastCleanupOutcome is not null;

    /// <summary>추천 조치 화면 표시 여부.</summary>
    public bool IsOverview => !ShowAllResults;

    // 커뮤니티 관측과 캐시 크기만으로 개선 효과가 입증된 것은 아니다.
    /// <summary>커뮤니티 관측을 제외한 개선 후보 카드.</summary>
    public IReadOnlyList<FindingCardViewModel> RecommendedCards => _allCards
        .Where(c => c.Verdict == Verdict.Candidate && !IsCommunityCard(c))
        .OrderByDescending(c => c.CanOpenSettings || c.HasLinks)
        .ToArray();

    /// <summary>선택한 목적과 표시 범위에 맞는 카드.</summary>
    public IEnumerable<FindingCardViewModel> VisibleCards => ShowAllResults ? Cards
        : RecommendedCards;
    /// <summary>현재 결과 목록이 비었는지 여부.</summary>
    public bool IsResultListEmpty => !VisibleCards.Any();
    /// <summary>결과 목록의 제목.</summary>
    public string ResultsHeading => ShowAllResults ? Strings.Overview_All : Strings.Overview_Recommendations;
    /// <summary>선택한 결과 범위의 설명.</summary>
    public string ResultsDescription => ShowAllResults ? Strings.Overview_AllHelp
        : Strings.Overview_RecommendationsHelp;
    /// <summary>추천 조치 건수를 포함한 탭 문구.</summary>
    public string RecommendationsTab => DisplayText.Format(Strings.Overview_RecommendationsCount, RecommendedCards.Count);
    /// <summary>전체 결과 건수를 포함한 탭 문구.</summary>
    public string AllResultsTab => DisplayText.Format(Strings.Overview_AllCount, _allCards.Count);
    /// <summary>앱 안에서 바로 실행할 수 있는 후보 수.</summary>
    public int DoNowCount => RecommendedCards.Count(card => card.CanTrialDisplay || _actionAvailability.CanExecuteInApp(card.Finding));

    /// <summary>사용자가 다른 곳에서 직접 해야 하는 후보 수.</summary>
    public int DoManuallyCount => RecommendedCards.Count - DoNowCount;

    /// <summary>바로 할 수 있는 것이 하나라도 있는지(0이면 타일을 숨김).</summary>
    public bool HasDoNow => DoNowCount > 0;

    /// <summary>바로 할 수 있는 것 타일 문구.</summary>
    public string DoNowText => DisplayText.Format(Strings.Overview_DoNowCount, DoNowCount);

    /// <summary>직접 해야 하는 것 타일 문구.</summary>
    public string DoManuallyText => DisplayText.Format(Strings.Overview_DoManuallyCount, DoManuallyCount);

    /// <summary>
    /// 두 온라인 공급자가 모두 성공하고 온라인 규칙(NVIDIA 비교)의 판정 불가가 없어야 비교 완료로 봅니다.
    /// 로컬 드라이버 규칙(AMD/Intel 링크·OEM·설치 드라이버 등)의 확인 불가는 온라인 비교 여부와 무관하므로 섞지 않습니다(REV-014).
    /// </summary>
    private static bool OnlineComparisonComplete(ScanReport report) =>
        new[] { NvidiaLookupProbeContract.PROBE_ID, WindowsUpdateProbeContract.PROBE_ID }
            .All(id => report.ProbeSummaries.Any(p => p.ProbeId == id && p.Status == ProbeStatus.Success && p.IssueCount == 0 && !p.IsStillRunning))
        && !report.Findings.Any(f => f.Verdict == Verdict.CannotVerify && f.Id.StartsWith(DriverUpdateRule.FINDING_ID_PREFIX, StringComparison.Ordinal));

    /// <summary>검사 진행 상태와 개선 후보 제목.</summary>
    public string OverviewTitle => IsScanning ? Strings.Overview_Scanning
        : LastResult is null ? Strings.Overview_Welcome
        : RecommendedCards.Count > 0 ? DisplayText.Format(Strings.Overview_Found, RecommendedCards.Count)
        : Strings.Overview_NoCandidates;
    /// <summary>확인 불가·정상·정보 건수 설명.</summary>
    public string OverviewDescription => IsScanning ? Strings.Overview_ScanningHelp
        : LastResult is null ? Strings.Overview_WelcomeHelp
        : DisplayText.Format(Strings.Overview_ResultHelp, CannotVerifyCount, OkCount, InfoCount);
    /// <summary>결과 목록이 비었을 때의 안내.</summary>
    public string EmptyResultsText => IsScanning ? Strings.Overview_ScanningHelp
        : LastResult is null ? Strings.Overview_EmptyBefore
        : ShowAllResults ? Strings.Overview_EmptyFilter
        : Strings.Overview_EmptyAfter;

    /// <summary>추천 조치 전체로 돌아갑니다.</summary>
    [RelayCommand]
    private void ShowRecommendations()
    {
        ShowAllResults = false;
        CurrentPage = MainPage.Recommended;
    }

    /// <summary>모든 분류의 결과를 표시합니다.</summary>
    [RelayCommand]
    private void ShowAll()
    {
        ShowAllResults = true;
        SelectedCategory = Categories.FirstOrDefault();
        CurrentPage = MainPage.AllResults;
    }

    /// <summary>검사에서 발견한 앱 캐시 위치·크기를 바로 표시합니다. 삭제나 재검사를 실행하지 않습니다.</summary>
    [RelayCommand]
    private void ShowAppCaches()
    {
        ShowAllResults = true;
        SelectedCategory = Categories.FirstOrDefault(c => c.Category == FindingCategory.AppCache);
        CurrentPage = MainPage.AllResults;
    }

    partial void OnStateChanged(ScanState value) => NotifyOverview();

    private void NotifyOverview()
    {
        foreach (var property in new[] { nameof(VisibleCards), nameof(IsResultListEmpty), nameof(RecommendationsTab),
            nameof(AllResultsTab), nameof(DoNowCount), nameof(DoManuallyCount), nameof(HasDoNow), nameof(DoNowText),
            nameof(DoManuallyText), nameof(OverviewTitle),
            nameof(OverviewDescription), nameof(EmptyResultsText) })
        {
            OnPropertyChanged(property);
        }
    }
}
