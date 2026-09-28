/**
 * @file    : ActionFeedbackView.xaml.cs
 * @author  : rudals252
 * @brief   : 부모 조치 모델의 대상별 상태를 카드 안에 연결하며 별도 실행기를 만들지 않음
 */
using System.Windows;
using System.Windows.Controls;
using PcOptimizer.App.ViewModels;

namespace PcOptimizer.App.Views;

/// <summary>상태는 부모 VM에 보존하므로 뷰가 재생성되어도 결과가 사라지지 않습니다.</summary>
public partial class ActionFeedbackView : UserControl
{
    /// <summary>공통 조치 모델 바인딩입니다.</summary>
    public static readonly DependencyProperty OwnerProperty = DependencyProperty.Register(nameof(Owner), typeof(ActionCenterViewModel), typeof(ActionFeedbackView), new PropertyMetadata(null, Changed));
    /// <summary>조치·복구 기록 또는 고정 선택 영역을 식별합니다.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(ActionFeedbackView), new PropertyMetadata(null, Changed));
    /// <summary>동일 앱 수명의 실행 모델입니다.</summary>
    public ActionCenterViewModel? Owner { get => (ActionCenterViewModel?)GetValue(OwnerProperty); set => SetValue(OwnerProperty, value); }
    /// <summary>이 카드의 상태 키입니다.</summary>
    public object? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    /// <summary>별도의 스크롤 없이 카드 본문을 생성합니다.</summary>
    public ActionFeedbackView() { InitializeComponent(); UpdateFeedback(); }
    private static void Changed(DependencyObject value, DependencyPropertyChangedEventArgs args) => ((ActionFeedbackView)value).UpdateFeedback();
    private void UpdateFeedback()
    { if (FeedbackRoot is not null) { FeedbackRoot.DataContext = Owner is { } owner && Source is { } source ? owner.FeedbackFor(source) : null; } }
}
