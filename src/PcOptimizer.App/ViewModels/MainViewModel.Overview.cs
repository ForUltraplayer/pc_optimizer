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

    public bool IsOverview => !ShowAllResults;

    // Community observations and cache sizes are not verified opportunities for improvement.
    public IReadOnlyList<FindingCardViewModel> RecommendedCards => _allCards
        .Where(c => c.Verdict == Verdict.Candidate && !IsCommunityCard(c))
        .OrderByDescending(c => c.CanOpenSettings || c.HasLinks)
        .ToArray();

    public IEnumerable<FindingCardViewModel> VisibleCards => ShowAllResults ? Cards : RecommendedCards;
    public bool IsResultListEmpty => !VisibleCards.Any();
    public string ResultsHeading => ShowAllResults ? Strings.Overview_All : Strings.Overview_Recommendations;
    public string ResultsDescription => ShowAllResults ? Strings.Overview_AllHelp : Strings.Overview_RecommendationsHelp;
    public string RecommendationsTab => DisplayText.Format(Strings.Overview_RecommendationsCount, RecommendedCards.Count);
    public string AllResultsTab => DisplayText.Format(Strings.Overview_AllCount, _allCards.Count);
    public string SettingsCount => LastResult is null ? Strings.Overview_NotScanned
        : DisplayText.Format(Strings.Overview_CandidateCount, RecommendedCards.Count(c => c.Finding.Category != FindingCategory.Driver));
    public string DriverCount => LastResult is null ? Strings.Overview_NotScanned
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
        : ShowAllResults ? Strings.Overview_EmptyFilter : Strings.Overview_EmptyAfter;

    [RelayCommand]
    private void ShowRecommendations() => ShowAllResults = false;

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
