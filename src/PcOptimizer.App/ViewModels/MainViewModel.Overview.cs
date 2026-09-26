using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCards), nameof(ResultsHeading), nameof(ResultsDescription), nameof(IsOverview), nameof(IsResultListEmpty), nameof(EmptyResultsText))]
    private bool _showAllResults;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleCards), nameof(ResultsDescription), nameof(IsResultListEmpty), nameof(EmptyResultsText))]
    private bool _showSettingsOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanupOutcome))]
    private CleanupOutcomeViewModel? _lastCleanupOutcome;

    public bool HasCleanupOutcome => LastCleanupOutcome is not null;
    private bool _lastScanIncludedOnline;

    public bool IsOverview => !ShowAllResults;

    // Community observations and cache sizes are not verified opportunities for improvement.
    public IReadOnlyList<FindingCardViewModel> RecommendedCards => _allCards
        .Where(c => c.Verdict == Verdict.Candidate && !IsCommunityCard(c))
        .OrderByDescending(c => c.CanOpenSettings || c.HasLinks)
        .ToArray();

    public IEnumerable<FindingCardViewModel> VisibleCards => ShowAllResults ? Cards
        : RecommendedCards.Where(c => !ShowSettingsOnly || c.Finding.Category != FindingCategory.Driver);
    public bool IsResultListEmpty => !VisibleCards.Any();
    public string ResultsHeading => ShowAllResults ? Strings.Overview_All : Strings.Overview_Recommendations;
    public string ResultsDescription => ShowAllResults ? Strings.Overview_AllHelp
        : ShowSettingsOnly ? Strings.Overview_SettingsFocusHelp : Strings.Overview_RecommendationsHelp;
    public string RecommendationsTab => DisplayText.Format(Strings.Overview_RecommendationsCount, RecommendedCards.Count);
    public string AllResultsTab => DisplayText.Format(Strings.Overview_AllCount, _allCards.Count);
    public string SettingsCount => LastResult is null ? Strings.Overview_NotScanned
        : DisplayText.Format(Strings.Overview_CandidateCount, RecommendedCards.Count(c => c.Finding.Category != FindingCategory.Driver));
    public string DriverCount => LastResult is null ? Strings.Overview_NotScanned
        : !_lastScanIncludedOnline && !RecommendedCards.Any(c => c.Finding.Category == FindingCategory.Driver)
            ? Strings.Overview_OnlineNotChecked
            : DisplayText.Format(Strings.Overview_CandidateCount, RecommendedCards.Count(c => c.Finding.Category == FindingCategory.Driver));
    public string OverviewTitle => IsScanning ? Strings.Overview_Scanning
        : LastResult is null ? Strings.Overview_Welcome
        : RecommendedCards.Count > 0 ? DisplayText.Format(Strings.Overview_Found, RecommendedCards.Count)
        : Strings.Overview_NoCandidates;
    public string OverviewDescription => IsScanning ? Strings.Overview_ScanningHelp
        : LastResult is null ? Strings.Overview_WelcomeHelp
        : DisplayText.Format(Strings.Overview_ResultHelp, CannotVerifyCount, OkCount, InfoCount);
    public string EmptyResultsText => IsScanning ? Strings.Overview_ScanningHelp
        : LastResult is null ? Strings.Overview_EmptyBefore
        : ShowAllResults ? Strings.Overview_EmptyFilter
        : ShowSettingsOnly ? Strings.Overview_EmptySettings : Strings.Overview_EmptyAfter;

    [RelayCommand]
    private void ShowRecommendations()
    {
        ShowSettingsOnly = false;
        ShowAllResults = false;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        ShowSettingsOnly = true;
        ShowAllResults = false;
    }

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
