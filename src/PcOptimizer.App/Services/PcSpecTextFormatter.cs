/**
 * @file    : PcSpecTextFormatter.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 스냅샷을 공유용 텍스트로 만든다(fastfetch식 한 열: 머리글 줄, "[섹션]" 줄, "라벨: 값" 줄, 확인 불가 섹션은 안내 한 줄). 기본 익명화이며 식별 정보 포함 토글이 켜졌을 때만 PC 이름·사용자명을 머리글에 넣는다. 화면도 같은 줄 도우미를 쓴다
 */

// 기본 패키지
using System.Globalization;
using System.Text;

// 사용자 패키지
using PcOptimizer.App.Models;
using PcOptimizer.App.Resources;

namespace PcOptimizer.App.Services;

/// <summary>
/// 사양 스냅샷의 텍스트 형식기입니다. 화면(<see cref="ViewModels.PcSpecViewModel"/>)도 같은 줄 도우미로 줄을 만들어,
/// 화면의 줄 구성과 텍스트 복사·TXT 저장 결과가 같습니다(빈 구분 줄 제외).
/// </summary>
public sealed class PcSpecTextFormatter
{
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm";
    private const string ITEM_FORMAT = "{0} {1}";
    private const string LABEL_FORMAT = "{0}:";
    private const string SECTION_FORMAT = "[{0}]";

    /// <summary>
    /// 텍스트를 만듭니다. 머리글 줄 뒤에 섹션마다 빈 줄 하나를 두고 섹션 줄을 씁니다.
    /// </summary>
    /// <param name="snapshot">사양 스냅샷.</param>
    /// <param name="includeIdentity">PC 이름·사용자명·식별 항목을 포함할지(기본 false).</param>
    /// <param name="machineName">PC 이름(포함 시).</param>
    /// <param name="userName">사용자명(포함 시).</param>
    /// <returns>공유용 텍스트(줄 끝은 <see cref="Environment.NewLine"/>).</returns>
    public string Format(PcSpecSnapshot snapshot, bool includeIdentity, string? machineName, string? userName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var builder = new StringBuilder();
        foreach (var line in HeaderLines(snapshot, includeIdentity, machineName, userName))
        {
            builder.AppendLine(line);
        }

        foreach (var section in snapshot.Sections)
        {
            builder.AppendLine();
            builder.AppendLine(SectionHeader(section.Title));
            if (IsUnavailable(snapshot, section))
            {
                builder.AppendLine(Strings.Spec_SectionUnavailable);
                continue;
            }

            foreach (var item in VisibleItems(section, includeIdentity))
            {
                builder.AppendLine(ItemLine(item.Label, item.Value));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 머리글 줄("PC 사양 · 시각 · 익명화 표기", 식별 정보 포함 시 PC 이름·사용자명 줄)을 만듭니다.
    /// </summary>
    /// <param name="snapshot">사양 스냅샷.</param>
    /// <param name="includeIdentity">PC 이름·사용자명을 포함할지.</param>
    /// <param name="machineName">PC 이름(포함 시).</param>
    /// <param name="userName">사용자명(포함 시).</param>
    /// <returns>머리글 줄 목록.</returns>
    public IReadOnlyList<string> HeaderLines(PcSpecSnapshot snapshot, bool includeIdentity, string? machineName, string? userName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var marker = includeIdentity ? Strings.Spec_Identified : Strings.Spec_Anonymized;
        var capturedAt = snapshot.CapturedAtUtc.ToLocalTime().ToString(DATE_FORMAT, CultureInfo.CurrentCulture);
        var lines = new List<string> { string.Format(CultureInfo.CurrentCulture, Strings.Spec_TextHeader, capturedAt, marker) };
        if (includeIdentity)
        {
            lines.Add(ItemLine(Strings.Spec_MachineName, machineName));
            lines.Add(ItemLine(Strings.Spec_UserName, userName));
        }

        return lines;
    }

    /// <summary>
    /// 섹션이 확인 불가(모든 항목 값 없음)인지 여부입니다. 확인 불가 섹션은 항목 대신 안내 한 줄만 씁니다.
    /// </summary>
    /// <param name="snapshot">사양 스냅샷.</param>
    /// <param name="section">섹션.</param>
    public static bool IsUnavailable(PcSpecSnapshot snapshot, PcSpecSection section)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(section);
        return snapshot.UnavailableSections.Contains(section.Title, StringComparer.Ordinal);
    }

    /// <summary>
    /// 표시할 항목(식별 정보 포함이 꺼져 있으면 <see cref="PcSpecItem.IsIdentifying"/> 항목을 뺌)입니다.
    /// </summary>
    /// <param name="section">섹션.</param>
    /// <param name="includeIdentity">식별 항목을 포함할지.</param>
    public static IEnumerable<PcSpecItem> VisibleItems(PcSpecSection section, bool includeIdentity)
    {
        ArgumentNullException.ThrowIfNull(section);
        return section.Items.Where(item => includeIdentity || !item.IsIdentifying)
            .Select(item => includeIdentity && item.IdentifyingValue is not null ? item with { Value = item.IdentifyingValue } : item);
    }

    /// <summary>섹션 제목 줄("[운영체제]")입니다.</summary>
    /// <param name="title">섹션 제목.</param>
    public static string SectionHeader(string title) => string.Format(CultureInfo.CurrentCulture, SECTION_FORMAT, title);

    /// <summary>항목 라벨 표기("모델:")입니다.</summary>
    /// <param name="label">항목 라벨.</param>
    public static string LabelText(string label) => string.Format(CultureInfo.CurrentCulture, LABEL_FORMAT, label);

    /// <summary>항목 값 표기(null이면 "확인 불가")입니다.</summary>
    /// <param name="value">항목 값.</param>
    public static string ValueText(string? value) => value ?? Strings.Spec_ValueUnknown;

    /// <summary>항목 한 줄("모델: 값", 라벨 표기와 값 표기를 공백 하나로 이음)입니다.</summary>
    /// <param name="label">항목 라벨.</param>
    /// <param name="value">항목 값(null이면 "확인 불가").</param>
    public static string ItemLine(string label, string? value)
    {
        return string.Format(CultureInfo.CurrentCulture, ITEM_FORMAT, LabelText(label), ValueText(value));
    }
}
