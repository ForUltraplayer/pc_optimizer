/**
 * @file    : ActionFeedback.cs
 * @author  : rudals252
 * @brief   : 누른 카드에 남기는 진행·확인·결과. 실행 권한은 기존 조율기만 보유
 */
using CommunityToolkit.Mvvm.ComponentModel;
using PcOptimizer.Core.Actions;

namespace PcOptimizer.App.ViewModels;

/// <summary>카드 재생성·필터 전환 뒤에도 동일 대상의 반응을 보존합니다.</summary>
public sealed partial class ActionFeedback(ActionCenterViewModel owner) : ObservableObject
{
    /// <summary>실행·취소·확인 명령을 제공하는 기존 모델입니다.</summary>
    public ActionCenterViewModel Owner { get; } = owner;
    /// <summary>선택 위치의 상태 안내입니다.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasContent))] private string _status = "";
    /// <summary>실제 작업이 진행 중인지 표시합니다.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasContent))] private bool _isWorking;
    /// <summary>활성 항목 하나만 실행 확인을 가질 수 있습니다.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPreview)), NotifyPropertyChangedFor(nameof(HasContent))] private ActionPreviewViewModel? _preview;
    /// <summary>이 항목의 마지막 결과입니다.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasResult)), NotifyPropertyChangedFor(nameof(HasContent))] private ActionResultViewModel? _result;
    /// <summary>조치 완료와 별도인 재검사 상태입니다.</summary>
    [ObservableProperty] private string _rescanStatus = "";
    /// <summary>복구 가능한 설정만 바로 되돌리기 연결을 제공합니다.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanUndo))] private RollbackItemViewModel? _undo;
    /// <summary>준비한 확인 화면이 있는지 여부입니다.</summary>
    public bool HasPreview => Preview is not null;
    /// <summary>실행 결과가 있는지 여부입니다.</summary>
    public bool HasResult => Result is not null;
    /// <summary>내용 없는 카드에는 빈 패널을 표시하지 않습니다.</summary>
    public bool HasContent => IsWorking || HasPreview || HasResult || Status.Length > 0;
    /// <summary>삭제 등 복원 불가능한 조치에는 버튼을 표시하지 않습니다.</summary>
    public bool CanUndo => Undo is not null;
}

public sealed partial class ActionCenterViewModel
{
    private readonly Dictionary<object, ActionFeedback> _feedback = [];
    private ActionFeedback? _activeFeedback;
    private ActionChoice? _activeChoice;
    private bool _readingHistory;
    /// <summary>다른 페이지에서 진입한 조치용 확인 영역입니다.</summary>
    public ActionFeedback StandaloneFeedback => FeedbackFor("external");
    /// <summary>카드 내부 조치에서 바깥 화면을 상단으로 이동하지 않도록 구분합니다.</summary>
    public bool UsesStandaloneFeedback => _activeFeedback is null || ReferenceEquals(_activeFeedback, StandaloneFeedback);
    private ActionFeedback CurrentFeedback => _activeFeedback ?? StandaloneFeedback;
    /// <summary>동일 조치 대상·복구 ID에 같은 표시 모델을 돌려줍니다.</summary>
    public ActionFeedback FeedbackFor(object source)
    {
        if (source is ActionFeedback feedback && ReferenceEquals(feedback.Owner, this)) { return feedback; }
        object key = source switch { ActionChoice c => (c.Id, c.Target), RollbackItemViewModel r => r.Id, _ => source };
        if (!_feedback.TryGetValue(key, out var value)) { _feedback.Add(key, value = new(this)); }
        return value;
    }
    private void ActivateFeedback(object source)
    {
        var previous = CurrentFeedback;
        if (previous.Preview is not null) { previous.Status = "다른 항목을 선택했습니다. 실행하려면 이 항목을 다시 확인하세요."; }
        previous.Preview = null;
        Preview = null;
        _activeFeedback = FeedbackFor(source);
        _activeChoice = source is ActionFeedback selected ? Choices.FirstOrDefault(c => ReferenceEquals(FeedbackFor(c), selected)) : source as ActionChoice;
        _activeFeedback.Status = ""; _activeFeedback.Result = null; _activeFeedback.RescanStatus = ""; _activeFeedback.Undo = null;
        Status = "";
        OnPropertyChanged(nameof(UsesStandaloneFeedback));
    }
    partial void OnPreviewChanged(ActionPreviewViewModel? value) => CurrentFeedback.Preview = value;
    partial void OnStatusChanged(string value) { if (!_readingHistory) { CurrentFeedback.Status = value; } }
    partial void OnRescanStatusChanged(string value) => CurrentFeedback.RescanStatus = value;
    private void SetRescanFeedback(ActionFeedback feedback, string value)
    {
        feedback.RescanStatus = value;
        if (ReferenceEquals(feedback, CurrentFeedback)) { RescanStatus = value; }
    }
    private void UpdateFeedbackResult(ActionResultViewModel? value)
    {
        CurrentFeedback.Result = value;
        if (value is { Succeeded: true, IsRestore: false } && _executing is { } plan && _workflow.Supports(plan.ActionId, true))
        { CurrentFeedback.Undo = new(plan.Id, plan.ActionId, value.ActionName, "", false, true); }
        else { CurrentFeedback.Undo = null; }
    }
    private IEnumerable<ActionChoice> DisplayChoices => _activeChoice is { } active && !Choices.Contains(active) && FeedbackFor(active).HasContent
        ? Choices.Append(active) : Choices;
    private async Task PrepareInlineAsync(ActionId id, ActionTarget target, object source, bool restore = false)
    {
        if (!CanPrepare()) { return; }
        ActivateFeedback(source); await PrepareCoreAsync(id, target, restore);
    }
    /// <summary>현재 카드 안에서 원본 복구를 준비하며 별도 실행 확인을 유지합니다.</summary>
    [CommunityToolkit.Mvvm.Input.RelayCommand(CanExecute = nameof(CanUndoFeedback))]
    private Task UndoFeedbackAsync(ActionFeedback? feedback) => CanUndoFeedback(feedback) && feedback?.Undo is { } undo
        ? PrepareInlineAsync(undo.ActionId, new ActionTarget.Restore(undo.Id), feedback, true) : Task.CompletedTask;
    private bool CanUndoFeedback(ActionFeedback? feedback) => CanPrepare() && feedback?.Undo is { CanRestore: true } && _feedback.Values.Contains(feedback);
}
