/**
 * @file    : CategoryItemViewModel.cs
 * @author  : rudals252
 * @brief   : 왼쪽 분류 목록 항목(분류·이름·Finding 건수) 표시 모델
 */

// 사용자 패키지
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.ViewModels;

/// <summary>
/// 분류 목록의 항목입니다. <see cref="Category"/>가 null이면 "전체"입니다.
/// </summary>
public sealed class CategoryItemViewModel
{
    /// <summary>
    /// 분류 항목을 만듭니다.
    /// </summary>
    /// <param name="category">분류(null이면 전체).</param>
    /// <param name="count">이 분류의 Finding 건수.</param>
    public CategoryItemViewModel(FindingCategory? category, int count)
    {
        Category = category;
        Count = count;
        DisplayName = category is { } value ? DisplayText.Category(value) : Strings.Category_All;
        Label = DisplayText.Format(Strings.Category_ItemFormat, DisplayName, count);
    }

    /// <summary>분류(null이면 전체).</summary>
    public FindingCategory? Category { get; }

    /// <summary>Finding 건수.</summary>
    public int Count { get; }

    /// <summary>분류 이름.</summary>
    public string DisplayName { get; }

    /// <summary>목록 표시 문자열("이름 (건수)").</summary>
    public string Label { get; }

    /// <summary>
    /// Finding이 이 분류에 속하는지 확인합니다.
    /// </summary>
    /// <param name="category">Finding 분류.</param>
    /// <returns>속하면 true.</returns>
    public bool Includes(FindingCategory category)
    {
        return Category is null || Category == category;
    }
}
