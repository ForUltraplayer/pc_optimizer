/**
 * @file    : NvidiaProductListParser.cs
 * @author  : rudals252
 * @brief   : NVIDIA 제품 목록 XML(lookupValueSearch TypeID=3)을 DTD·외부 참조 없이 해석해 이름·psid(ParentID)·pfid(Value)를 검증하고, 어댑터 이름과 정확히 같은 항목만 찾는 순수 파서
 */

// 기본 패키지
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// NVIDIA 제품 목록 파서입니다. XML이 아니면(HTML 오류 페이지 등) NetworkFailed, 구조·값이 맞지 않으면 ProbeError이며 예외를 던지지 않습니다.
/// </summary>
public static partial class NvidiaProductListParser
{
    private const string ROOT_ELEMENT = "LookupValueSearch";
    private const string LIST_ELEMENT = "LookupValues";
    private const string ITEM_ELEMENT = "LookupValue";
    private const string NAME_ELEMENT = "Name";
    private const string VALUE_ELEMENT = "Value";
    private const string PARENT_ATTRIBUTE = "ParentID";
    private const string SPACE = " ";
    private const int REGEX_TIMEOUT_MILLISECONDS = 100;
    private const long MAX_DOCUMENT_CHARACTERS = 4 * 1024 * 1024;

    /// <summary>
    /// 제품 목록 XML을 해석합니다.
    /// </summary>
    /// <param name="body">응답 본문.</param>
    /// <returns>제품 목록 또는 실패.</returns>
    public static NvidiaLookupResult<IReadOnlyList<NvidiaProduct>> Parse(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MAX_DOCUMENT_CHARACTERS,
            };
            using var reader = XmlReader.Create(new StringReader(body), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>.Fail(CannotVerifyReason.NetworkFailed, "productList.notXml");
        }

        var list = document.Root is { Name.LocalName: ROOT_ELEMENT } root ? root.Element(LIST_ELEMENT) : null;
        if (list is null)
        {
            return NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>.Fail(CannotVerifyReason.ProbeError, "productList.structure");
        }

        var products = new List<NvidiaProduct>();
        foreach (var item in list.Elements(ITEM_ELEMENT))
        {
            var name = item.Element(NAME_ELEMENT)?.Value.Trim();
            if (string.IsNullOrEmpty(name)
                || !TryParseId(item.Attribute(PARENT_ATTRIBUTE)?.Value, out var psid)
                || !TryParseId(item.Element(VALUE_ELEMENT)?.Value, out var pfid))
            {
                return NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>.Fail(CannotVerifyReason.ProbeError, "productList.item");
            }

            products.Add(new NvidiaProduct(name, psid, pfid));
        }

        return products.Count == 0
            ? NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>.Fail(CannotVerifyReason.ProbeError, "productList.empty")
            : NvidiaLookupResult<IReadOnlyList<NvidiaProduct>>.Success(products);
    }

    /// <summary>
    /// 어댑터 이름과 정확히 같은 제품만 찾습니다(앞뒤·연속 공백과 대소문자만 무시, 접두사·부분 일치로 고르지 않음).
    /// </summary>
    /// <param name="products">제품 목록.</param>
    /// <param name="adapterName">어댑터 이름.</param>
    /// <returns>일치 항목(0개·여러 개면 호출자가 모호로 처리).</returns>
    public static IReadOnlyList<NvidiaProduct> FindExactMatches(IReadOnlyList<NvidiaProduct> products, string adapterName)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(adapterName);

        var target = NormalizeName(adapterName);
        return target.Length == 0
            ? []
            : [.. products.Where(product => string.Equals(NormalizeName(product.Name), target, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// 이름의 앞뒤 공백을 없애고 연속 공백을 하나로 줄인다.
    /// </summary>
    internal static string NormalizeName(string name)
    {
        return WhitespacePattern().Replace(name.Trim(), SPACE);
    }

    /// <summary>
    /// 양의 정수 ID를 해석한다.
    /// </summary>
    private static bool TryParseId(string? text, out int value)
    {
        return int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
    }

    /// <summary>
    /// 연속 공백 정규식.
    /// </summary>
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex WhitespacePattern();
}
