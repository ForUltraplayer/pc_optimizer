/**
 * @file    : DeviceIdTokenizer.cs
 * @author  : rudals252
 * @brief   : 기본(익명화) 내보내기 한 번 안에서 장치 내부 ID(모니터 장치 경로·PnP ID·장치 인터페이스 경로·볼륨 경로·중괄호 GUID)를 처음 본 순서의 토큰(display-1, pnp-1, volume-1, guid-1 …)으로 바꾸는 치환기
 */

// 기본 패키지
using System.Globalization;
using System.Text.RegularExpressions;

namespace PcOptimizer.App.Services;

/// <summary>
/// 장치 내부 ID를 내보내기 단위 토큰으로 바꿉니다(스펙 §3 "장치·경로 내부 ID는 내보내기 시 익명화").
/// 해시를 토큰으로 쓰지 않고, 한 인스턴스 안에서 처음 본 순서대로 종류별 번호를 붙입니다.
/// 같은 원문 ID(대소문자 무시)는 어느 필드에 있든 같은 토큰이 되므로 Finding ID·측정값·상세 문장 사이의 연결이 유지됩니다.
/// 내보내기마다 새 인스턴스를 만들어 쓰며, 다른 내보내기와 토큰 번호를 공유하지 않습니다.
/// </summary>
/// <remarks>
/// 대상(앞의 규칙이 먼저 적용됨):
/// <list type="number">
/// <item>모니터 장치 경로 <c>\\?\DISPLAY#…#…#{GUID}</c> → <c>display-N</c></item>
/// <item>볼륨 경로 <c>\\?\Volume{GUID}\</c> → <c>volume-N</c></item>
/// <item>그 밖의 장치 인터페이스 경로 <c>\\?\PCI#…#…#{GUID}</c> 등 → <c>pnp-N</c></item>
/// <item>PnP 인스턴스/하드웨어 ID: 알려진 열거자(PCI, ROOT, DISPLAY, USB, HID, ACPI, SWD 등)로 시작하는 <c>열거자\ID[\인스턴스]</c>,
/// 또는 대문자 열거자 + '&amp;'가 든 인스턴스를 가진 <c>열거자\ID\인스턴스</c> 모양(HK로 시작하는 레지스트리 하이브 제외) → <c>pnp-N</c></item>
/// <item>중괄호 GUID <c>{xxxxxxxx-…}</c>(저장소 제공자 디스크 ID 등) → <c>guid-N</c></item>
/// </list>
/// 레지스트리 경로(HKLM\SYSTEM\…)·파일 경로·중괄호 없는 GUID(전원 계획 등 Windows 공용 값)는 바꾸지 않습니다.
/// 어댑터 LUID는 측정값으로 내보내지 않으므로 대상이 아닙니다.
/// </remarks>
public sealed partial class DeviceIdTokenizer
{
    /// <summary>모니터 장치 경로 토큰 종류.</summary>
    public const string KIND_DISPLAY = "display";

    /// <summary>PnP ID·장치 인터페이스 경로 토큰 종류.</summary>
    public const string KIND_PNP = "pnp";

    /// <summary>볼륨 경로 토큰 종류.</summary>
    public const string KIND_VOLUME = "volume";

    /// <summary>중괄호 GUID 토큰 종류.</summary>
    public const string KIND_GUID = "guid";

    private const string TOKEN_FORMAT = "{0}-{1}";
    private const int FIRST_TOKEN_NUMBER = 1;
    private const int REGEX_TIMEOUT_MILLISECONDS = 1000;
    private const string GROUP_DISPLAY = "display";
    private const string GROUP_VOLUME = "volume";
    private const string GROUP_INTERFACE = "iface";
    private const string GROUP_PNP = "pnp";
    private const string GROUP_GUID = "guid";

    private readonly Dictionary<string, string> _tokens = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);

    /// <summary>
    /// 문자열 안의 장치 ID를 토큰으로 바꿉니다.
    /// </summary>
    /// <param name="text">원문.</param>
    /// <returns>치환한 문자열.</returns>
    public string Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return DeviceIdPattern().Replace(text, match => TokenFor(match.Value, KindOf(match)));
    }

    /// <summary>
    /// 일치한 그룹으로 토큰 종류를 정한다.
    /// </summary>
    private static string KindOf(Match match)
    {
        if (match.Groups[GROUP_DISPLAY].Success)
        {
            return KIND_DISPLAY;
        }

        if (match.Groups[GROUP_VOLUME].Success)
        {
            return KIND_VOLUME;
        }

        return match.Groups[GROUP_INTERFACE].Success || match.Groups[GROUP_PNP].Success ? KIND_PNP : KIND_GUID;
    }

    /// <summary>
    /// 원문 ID의 토큰을 돌려준다. 처음 보면 종류별 다음 번호를 붙인다.
    /// </summary>
    private string TokenFor(string raw, string kind)
    {
        if (_tokens.TryGetValue(raw, out var existing))
        {
            return existing;
        }

        var number = _counters.TryGetValue(kind, out var last) ? last + 1 : FIRST_TOKEN_NUMBER;
        _counters[kind] = number;
        var token = string.Format(CultureInfo.InvariantCulture, TOKEN_FORMAT, kind, number);
        _tokens[raw] = token;
        return token;
    }

    /// <summary>
    /// 장치 ID 모양을 찾는 정규식(대안 순서가 우선순위).
    /// </summary>
    [GeneratedRegex(
        @"(?<display>\\\\\?\\(?i:DISPLAY)#[^#\s""|]+#[^#\s""|]+#\{[0-9A-Fa-f-]{36}\})"
        + @"|(?<volume>\\\\\?\\(?i:Volume)\{[0-9A-Fa-f-]{36}\}\\?)"
        + @"|(?<iface>\\\\\?\\[A-Za-z0-9_]+#[^#\s""|]+#[^#\s""|]+#\{[0-9A-Fa-f-]{36}\})"
        + @"|(?<pnp>(?<![A-Za-z0-9_\\])(?i:PCI|ROOT|DISPLAY|MONITOR|USB|USBSTOR|HID|ACPI|SWD|HDAUDIO|SCSI|STORAGE|NVME|BTHENUM|BTH|UMB|SW|UEFI)\\[^\s\\""|,]+(?:\\[^\s\\""|,]+)?"
        + @"|(?<![A-Za-z0-9_\\])(?!HK)[A-Z][A-Z0-9_]{1,15}\\[^\s\\""|,]+\\[^\s\\""|,]*&[^\s\\""|,]*)"
        + @"|(?<guid>\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\})",
        RegexOptions.CultureInvariant,
        REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex DeviceIdPattern();
}
