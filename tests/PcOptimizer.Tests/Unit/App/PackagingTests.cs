/**
 * @file    : PackagingTests.cs
 * @author  : rudals252
 * @brief   : 포터블 배포에 필요한 App 프로젝트 속성(단일 파일·self-contained·아이콘·어셈블리 이름·버전)이 설정돼 있는지 검증
 */

// 기본 패키지
using System.IO;
using System.Xml.Linq;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>배포 속성을 검증합니다.</summary>
public sealed class PackagingTests
{
    private static readonly string CSPROJ = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PcOptimizer.App", "PcOptimizer.App.csproj"));

    /// <summary>단일 파일 self-contained 배포·아이콘·이름·버전 속성이 있다.</summary>
    [Theory]
    [InlineData("AssemblyName", "PcOptimizer")]
    [InlineData("ApplicationIcon", @"Assets\app.ico")]
    [InlineData("PublishSingleFile", "true")]
    [InlineData("SelfContained", "true")]
    [InlineData("RuntimeIdentifier", "win-x64")]
    [InlineData("IncludeNativeLibrariesForSelfExtract", "true")]
    [InlineData("EnableCompressionInSingleFile", "true")]
    [InlineData("SatelliteResourceLanguages", "ko")]
    [InlineData("DebugType", "none")]
    public void PublishPropertiesArePresent(string name, string expected)
    {
        var doc = XDocument.Load(CSPROJ);
        var value = doc.Descendants(name).Select(e => e.Value.Trim()).FirstOrDefault();
        Assert.Equal(expected, value);
    }

    /// <summary>버전이 유의적 버전 형식이다.</summary>
    [Fact]
    public void VersionIsSemantic()
    {
        var version = XDocument.Load(CSPROJ).Descendants("Version").Select(e => e.Value.Trim()).FirstOrDefault();
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }
}
