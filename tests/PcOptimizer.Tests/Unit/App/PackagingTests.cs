/**
 * @file    : PackagingTests.cs
 * @author  : rudals252
 * @brief   : 포터블 배포에 필요한 App 프로젝트 속성(단일 파일·self-contained·아이콘·어셈블리 이름·버전·시작 훅 끄기)이 설정돼 있는지 검증
 */

// 기본 패키지
using System.IO;
using System.Xml.Linq;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>배포 속성을 검증합니다.</summary>
public sealed class PackagingTests
{
    private static readonly string ROOT = FindRoot();
    private static readonly string CSPROJ = Path.Combine(ROOT, "src", "PcOptimizer.App", "PcOptimizer.App.csproj");
    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        { if (File.Exists(Path.Combine(dir.FullName, "PcOptimizer.sln"))) { return dir.FullName; } }
        throw new InvalidOperationException("테스트 저장소를 찾지 못했습니다.");
    }

    /// <summary>단일 파일 self-contained 배포·아이콘·이름·버전 속성과 시작 훅 끄기(관리자 권한 앱에 사용자 환경 변수 DOTNET_STARTUP_HOOKS 주입 차단)가 있다.</summary>
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
    [InlineData("StartupHookSupport", "false")]
    public void PublishPropertiesArePresent(string name, string expected)
    {
        var doc = XDocument.Load(name is "AssemblyName" or "ApplicationIcon" or "StartupHookSupport" ? CSPROJ
            : Path.Combine(ROOT, "src", "PcOptimizer.App", "Properties", "PublishProfiles", "win-x64.pubxml"));
        var value = doc.Descendants(name).Select(e => e.Value.Trim()).FirstOrDefault();
        Assert.Equal(expected, value);
    }

    /// <summary>버전이 유의적 버전 형식이다.</summary>
    [Fact]
    public void VersionIsSemantic()
    {
        var version = XDocument.Load(CSPROJ).Descendants("Version").Select(e => e.Value.Trim()).FirstOrDefault();
        Assert.Matches(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$", version);
    }
}
