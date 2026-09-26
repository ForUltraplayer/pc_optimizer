/**
 * @file    : MainViewModel.Overview.cs
 * @author  : rudals252
 * @brief   : 추천 조치·목적별 결과·온라인 비교 완료 상태 표시
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

    /// <summary>설정 후보만 표시할지 여부.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCards), nameof(ResultsDescription), nameof(IsResultListEmpty), nameof(EmptyResultsText))]
    private bool _showSettingsOnly;

    /// <summary>최근 실제 정리 실행 결과.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanupOutcome))]
    private CleanupOutcomeViewModel? _lastCleanupOutcome;

    /// <summary>최근 실제 정리 실행 결과가 있는지 여부.</summary>
    public bool HasCleanupOutcome => LastCleanupOutcome is not null;
    private bool _lastScanIncludedOnline;

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
        : RecommendedCards.Where(c => !ShowSettingsOnly || c.Finding.Category != FindingCategory.Driver);
    /// <summary>현재 결과 목록이 비었는지 여부.</summary>
    public bool IsResultListEmpty => !VisibleCards.Any();
    /// <summary>결과 목록의 제목.</summary>
    public string ResultsHeading => ShowAllResults ? Strings.Overview_All : Strings.Overview_Recommendations;
    /// <summary>선택한 결과 범위의 설명.</summary>
    public string ResultsDescription => ShowAllResults ? Strings.Overview_AllHelp
        : ShowSettingsOnly ? Strings.Overview_SettingsFocusHelp : Strings.Overview_RecommendationsHelp;
    /// <summary>추천 조치 건수를 포함한 탭 문구.</summary>
    public string RecommendationsTab => DisplayText.Format(Strings.Overview_RecommendationsCount, RecommendedCards.Count);
    /// <summary>전체 결과 건수를 포함한 탭 문구.</summary>
    public string AllResultsTab => DisplayText.Format(Strings.Overview_AllCount, _allCards.Count);
    /// <summary>드라이버 이외의 설정 개선 후보 수.</summary>
    public string SettingsCount => LastResult is null ? Strings.Overview_NotScanned
        : DisplayText.Format(Strings.Overview_CandidateCount, RecommendedCards.Count(c => c.Finding.Category != FindingCategory.Driver));
    /// <summary>요청 여부와 실제 온라인 비교 완료를 구분한 드라이버 안내입니다.</summary>
    public string DriverCount
    {
        get
        {
            if (LastResult is null) { return Strings.Overview_NotScanned; }
            var count = RecommendedCards.Count(c => c.Finding.Category == FindingCategory.Driver);
            if (OnlineComparisonComplete(LastResult.Report)) { return DisplayText.Format(Strings.Overview_CandidateCount, count); }
            if (count > 0) { return DisplayText.Format(Strings.Overview_OnlinePartialCount, count); }
            return _lastScanIncludedOnline ? Strings.Overview_OnlineUnavailable : Strings.Overview_OnlineNotChecked;
        }
    }

    /// <summary>두 온라인 공급자가 모두 완료되고 판정 불가가 없어야 후보 0개로 표시합니다.</summary>
    private static bool OnlineComparisonComplete(ScanReport report) =>
        new[] { NvidiaLookupProbeContract.PROBE_ID, WindowsUpdateProbeContract.PROBE_ID }
            .All(id => report.ProbeSummaries.Any(p => p.ProbeId == id && p.Status == ProbeStatus.Success && p.IssueCount == 0 && !p.IsStillRunning))
        && !report.Findings.Any(f => f.Category == FindingCategory.Driver && f.Verdict == Verdict.CannotVerify);

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
        : ShowSettingsOnly ? Strings.Overview_EmptySettings : Strings.Overview_EmptyAfter;

    /// <summary>추천 조치 전체로 돌아갑니다.</summary>
    [RelayCommand]
    private void ShowRecommendations()
    {
        ShowSettingsOnly = false;
        ShowAllResults = false;
    }

    /// <summary>설정 개선 후보만 표시합니다.</summary>
    [RelayCommand]
    private void ShowSettings()
    {
        ShowSettingsOnly = true;
        ShowAllResults = false;
    }

    /// <summary>드라이버 분류의 모든 결과를 엽니다.</summary>
    [RelayCommand]
    private void ShowDrivers()
    {
        ShowAllResults = true;
        var category = Categories.FirstOrDefault(c => c.Category == FindingCategory.Driver);
        if (category is null)
        {
            category = new CategoryItemViewModel(FindingCategory.Driver, 0);
            Categories.Add(category);
        }
        SelectedCategory = category;
    }

    /// <summary>모든 분류의 결과를 표시합니다.</summary>
    [RelayCommand]
    private void ShowAll()
    {
        ShowAllResults = true;
        SelectedCategory = Categories.FirstOrDefault();
    }

    partial void OnStateChanged(ScanState value) => NotifyOverview();

    private void NotifyOverview()
    {
        foreach (var property in new[] { nameof(VisibleCards), nameof(IsResultListEmpty), nameof(RecommendationsTab),
            nameof(AllResultsTab), nameof(SettingsCount), nameof(DriverCount), nameof(OverviewTitle),
            nameof(OverviewDescription), nameof(EmptyResultsText) })
        {
            OnPropertyChanged(property);
        }
    }
}
