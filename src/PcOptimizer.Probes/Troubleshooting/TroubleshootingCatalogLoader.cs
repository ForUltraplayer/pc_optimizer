/**
 * @file    : TroubleshootingCatalogLoader.cs
 * @author  : rudals252
 * @brief   : Probes 어셈블리에 포함된 문제 해결 카탈로그(PcOptimizer.Rules.troubleshooting-tools.json)를 읽어 공식 링크 표와 대조·검증하는 로더(파일 시스템·네트워크에서 읽지 않음)
 */
using System.Text;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Troubleshooting;

namespace PcOptimizer.Probes.Troubleshooting;

/// <summary>원본은 저장소 <c>rules/troubleshooting-tools.json</c>이며 빌드 때 리소스로 들어갑니다. 외부 도구 링크 ID는 공식 링크 표에 있어야 합니다.</summary>
public static class TroubleshootingCatalogLoader
{
    /// <summary>포함 리소스 이름(Probes 프로젝트의 LogicalName과 같음).</summary>
    public const string RESOURCE_NAME = "PcOptimizer.Rules.troubleshooting-tools.json";
    /// <summary>받아들이는 최대 크기(바이트).</summary>
    public const int MAX_BYTES = 1024 * 1024;
    /// <summary>리소스 없음 오류 코드.</summary>
    public const string ERROR_MISSING = "resourceMissing";
    /// <summary>리소스 초과 오류 코드.</summary>
    public const string ERROR_TOO_LARGE = "resourceTooLarge";

    /// <summary>포함 리소스를 읽어 검증합니다. 예외를 던지지 않습니다.</summary>
    /// <param name="links">공식 링크 표(없으면 외부 도구 참조가 모두 오류가 되어 카탈로그를 쓰지 않음).</param>
    public static TroubleshootingParseResult LoadEmbedded(VendorLinkCatalog? links)
    {
        using var stream = typeof(TroubleshootingCatalogLoader).Assembly.GetManifestResourceStream(RESOURCE_NAME);
        if (stream is null) { return new(null, [ERROR_MISSING]); }
        if (stream.Length > MAX_BYTES) { return new(null, [ERROR_TOO_LARGE]); }
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
        var ids = links?.Entries.Select(e => e.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        return TroubleshootingCatalogParser.Parse(reader.ReadToEnd(), ids.Contains);
    }
}
