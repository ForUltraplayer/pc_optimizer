/**
 * @file    : VendorLinkCatalogLoader.cs
 * @author  : rudals252
 * @brief   : Probes 어셈블리에 포함된 공식 링크 표(PcOptimizer.Rules.vendor-links.json)를 읽어 검증하는 로더(파일 시스템·네트워크에서 읽지 않음)
 */

// 기본 패키지
using System.IO;
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.Probes.Drivers;

/// <summary>
/// 포함 리소스의 공식 링크 표를 읽습니다. 원본은 저장소 <c>rules/vendor-links.json</c>이며 빌드 때 리소스로 들어갑니다.
/// 출력 폴더나 사용자 쓰기 가능한 위치의 파일은 읽지 않으므로 링크 허용 목록을 파일 교체로 바꿀 수 없습니다.
/// </summary>
public static class VendorLinkCatalogLoader
{
    /// <summary>포함 리소스 이름(Probes 프로젝트의 LogicalName과 같음).</summary>
    public const string RESOURCE_NAME = "PcOptimizer.Rules.vendor-links.json";

    /// <summary>받아들이는 최대 크기(바이트).</summary>
    public const int MAX_BYTES = 1024 * 1024;

    /// <summary>리소스가 없음 오류 코드.</summary>
    public const string ERROR_MISSING = "resourceMissing";

    /// <summary>리소스가 너무 큼 오류 코드.</summary>
    public const string ERROR_TOO_LARGE = "resourceTooLarge";

    /// <summary>
    /// 포함 리소스를 읽어 검증합니다. 예외를 던지지 않습니다.
    /// </summary>
    /// <returns>해석 결과(실패하면 표는 null).</returns>
    public static VendorLinkParseResult LoadEmbedded()
    {
        using var stream = typeof(VendorLinkCatalogLoader).Assembly.GetManifestResourceStream(RESOURCE_NAME);
        if (stream is null)
        {
            return new VendorLinkParseResult(null, [ERROR_MISSING]);
        }

        if (stream.Length > MAX_BYTES)
        {
            return new VendorLinkParseResult(null, [ERROR_TOO_LARGE]);
        }

        using var reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: true);
        return VendorLinkCatalogParser.Parse(reader.ReadToEnd());
    }
}
