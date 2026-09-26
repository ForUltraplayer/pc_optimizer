/**
 * @file    : NvidiaDriverListParser.cs
 * @author  : rudals252
 * @brief   : NVIDIA DriverManualLookup JSON 응답을 해석해 요청 매핑(psid·pfid·osID)·항목별 버전(NNN.NN)·UTC 배포일·URL 디코딩 이름·HTTPS NVIDIA URL·계열 표시(IsCRD)·Windows 11 대상·GPU 제품 포함을 검증하는 순수 파서
 */

// 기본 패키지
using System.Globalization;
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// NVIDIA 드라이버 목록 파서입니다. JSON이 아니면(HTML 오류 페이지·잘린 응답) NetworkFailed, 필수 항목 누락·값 오류·매핑 불일치는
/// ProbeError이며 예외를 던지지 않고 빈 성공으로 바꾸지 않습니다. 단, 서버가 명시한 '해당 드라이버 없음' 응답은 빈 목록 성공입니다.
/// </summary>
public static class NvidiaDriverListParser
{
    /// <summary>'해당 드라이버 없음' 메시지 코드.</summary>
    public const string NOT_FOUND_MESSAGE_CODE = "DriverDownloadIDNotFound";

    /// <summary>대상 OS 이름(URL 디코딩 후 OSList에 있어야 함).</summary>
    public const string REQUIRED_OS_NAME = "Windows 11";

    private const string FLAG_TRUE = "1";
    private const string FLAG_FALSE = "0";

    /// <summary>배포일 형식(예: "Tue Sep 22, 2026"). 요일이 날짜와 맞지 않으면 거부된다.</summary>
    private static readonly string[] RELEASE_DATE_FORMATS = ["ddd MMM dd, yyyy", "ddd MMM d, yyyy"];

    /// <summary>
    /// 드라이버 목록 JSON을 해석합니다.
    /// </summary>
    /// <param name="body">응답 본문.</param>
    /// <param name="product">요청한 제품(매핑 확인용).</param>
    /// <param name="kind">요청한 목록 종류(IsCRD 확인용).</param>
    /// <param name="osId">요청한 OS ID.</param>
    /// <returns>검증한 항목 목록(응답 순서) 또는 실패.</returns>
    public static NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>> Parse(string body, NvidiaProduct product, NvidiaDriverListKind kind, int osId)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(product);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return Fail(CannotVerifyReason.NetworkFailed, "driverList.notJson");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Fail(CannotVerifyReason.ProbeError, "driverList.root");
            }

            if (!MatchesRequest(root, product, osId))
            {
                return Fail(CannotVerifyReason.ProbeError, "driverList.requestMismatch");
            }

            if (!int.TryParse(Text(root, "Success"), NumberStyles.None, CultureInfo.InvariantCulture, out var success)
                || !root.TryGetProperty("IDS", out var ids)
                || ids.ValueKind != JsonValueKind.Array)
            {
                return Fail(CannotVerifyReason.ProbeError, "driverList.envelope");
            }

            if (success == 0)
            {
                return IsNotFound(ids)
                    ? NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>>.Success(Array.Empty<NvidiaDriverEntry>())
                    : Fail(CannotVerifyReason.ProbeError, "driverList.unsuccessful");
            }

            if (ids.GetArrayLength() == 0)
            {
                return Fail(CannotVerifyReason.ProbeError, "driverList.noItems");
            }

            var entries = new List<NvidiaDriverEntry>();
            foreach (var item in ids.EnumerateArray())
            {
                var entry = ParseEntry(item, product, kind, out var code);
                if (entry is null)
                {
                    return Fail(CannotVerifyReason.ProbeError, code);
                }

                entries.Add(entry);
            }

            return NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>>.Success(entries);
        }
    }

    /// <summary>
    /// 항목 하나를 검증한다. 실패하면 null과 오류 코드.
    /// </summary>
    private static NvidiaDriverEntry? ParseEntry(JsonElement item, NvidiaProduct product, NvidiaDriverListKind kind, out string code)
    {
        code = "driverList.item";
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("downloadInfo", out var info)
            || info.ValueKind != JsonValueKind.Object
            || Text(info, "Success") != FLAG_TRUE)
        {
            return null;
        }

        var version = Text(info, "Version");
        if (!DriverVersionNumber.IsNvidiaResponseFormat(version))
        {
            code = "driverList.version";
            return null;
        }

        if (!DateTime.TryParseExact(Text(info, "ReleaseDateTime"), RELEASE_DATE_FORMATS, CultureInfo.InvariantCulture, DateTimeStyles.None, out var released))
        {
            code = "driverList.releaseDate";
            return null;
        }

        var name = Decode(Text(info, "Name"));
        if (string.IsNullOrWhiteSpace(name))
        {
            code = "driverList.name";
            return null;
        }

        var details = Text(info, "DetailsURL");
        var download = Text(info, "DownloadURL");
        if (!NvidiaUrlAllowlist.IsAllowed(details) || !NvidiaUrlAllowlist.IsAllowed(download))
        {
            code = "driverList.url";
            return null;
        }

        var isCrd = Text(info, "IsCRD");
        var isBeta = Text(info, "IsBeta");
        var expectedCrd = kind == NvidiaDriverListKind.Studio ? FLAG_TRUE : FLAG_FALSE;
        if (isCrd != expectedCrd || isBeta is not (FLAG_TRUE or FLAG_FALSE))
        {
            code = "driverList.branchFlag";
            return null;
        }

        if (!ListsOs(info) || !ListsProduct(info, product))
        {
            code = "driverList.mapping";
            return null;
        }

        return new NvidiaDriverEntry(version, DateOnly.FromDateTime(released), name.Trim(), details!, download!, isBeta == FLAG_TRUE);
    }

    /// <summary>
    /// 응답의 요청 기록(Request[0])이 보낸 psid·pfid·osID와 같은지 확인한다(다른 GPU·OS 응답 거부).
    /// </summary>
    private static bool MatchesRequest(JsonElement root, NvidiaProduct product, int osId)
    {
        if (!root.TryGetProperty("Request", out var request) || request.ValueKind != JsonValueKind.Array || request.GetArrayLength() == 0)
        {
            return false;
        }

        var first = request[0];
        return first.ValueKind == JsonValueKind.Object
            && Text(first, "psid") == product.Psid.ToString(CultureInfo.InvariantCulture)
            && Text(first, "pfid") == product.Pfid.ToString(CultureInfo.InvariantCulture)
            && Text(first, "osID") == osId.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 서버가 명시한 '해당 드라이버 없음' 응답인지 확인한다.
    /// </summary>
    private static bool IsNotFound(JsonElement ids)
    {
        foreach (var item in ids.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("downloadInfo", out var info)
                && info.ValueKind == JsonValueKind.Object
                && info.TryGetProperty("Messaging", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.EnumerateArray().Any(message => message.ValueKind == JsonValueKind.Object && Text(message, "MessageCode") == NOT_FOUND_MESSAGE_CODE))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 항목의 대상 OS 목록에 Windows 11이 있는지 확인한다.
    /// </summary>
    private static bool ListsOs(JsonElement info)
    {
        return info.TryGetProperty("OSList", out var list)
            && list.ValueKind == JsonValueKind.Array
            && list.EnumerateArray().Any(os => os.ValueKind == JsonValueKind.Object
                && string.Equals(Decode(Text(os, "OSName"))?.Trim(), REQUIRED_OS_NAME, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 항목의 지원 제품 목록(series[].products[].productName)에 요청한 제품 이름이 정확히 있는지 확인한다.
    /// </summary>
    private static bool ListsProduct(JsonElement info, NvidiaProduct product)
    {
        if (!info.TryGetProperty("series", out var series) || series.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var target = NvidiaProductListParser.NormalizeName(product.Name);
        foreach (var group in series.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Object || !group.TryGetProperty("products", out var products) || products.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            if (products.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object
                && Decode(Text(item, "productName")) is { } name
                && string.Equals(NvidiaProductListParser.NormalizeName(name), target, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 문자열 속성을 읽는다(없거나 문자열이 아니면 null).
    /// </summary>
    private static string? Text(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    /// <summary>
    /// 퍼센트 인코딩을 푼다(없으면 null).
    /// </summary>
    private static string? Decode(string? text)
    {
        return text is null ? null : Uri.UnescapeDataString(text);
    }

    /// <summary>
    /// 실패 결과를 만든다.
    /// </summary>
    private static NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>> Fail(CannotVerifyReason reason, string code)
    {
        return NvidiaLookupResult<IReadOnlyList<NvidiaDriverEntry>>.Fail(reason, code);
    }
}
