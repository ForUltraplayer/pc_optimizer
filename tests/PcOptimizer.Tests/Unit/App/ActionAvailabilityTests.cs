/**
 * @file    : ActionAvailabilityTests.cs
 * @author  : rudals252
 * @brief   : 앱 안에서 바로 실행할 수 있는 후보 판정(도구 캐시 카드·보호 위치 도구 조건)과 보호 위치 도구 존재 확인(프로세스 실행·PATH 탐색 없음)을 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Models;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>앱 내 실행 가능 판정을 검증합니다.</summary>
public sealed class ActionAvailabilityTests
{
    private const string PROGRAM_FILES = @"C:\PF";
    private const string NODE_DIRECTORY = @"C:\PF\nodejs";
    private const string DOTNET_DIRECTORY = @"C:\PF\dotnet";

    private static Finding Card(string id, Verdict verdict) => new(id, FindingCategory.AppCache, "제목", [], "근거", verdict, null, null,
        verdict == Verdict.Candidate ? new Recommendation("권고", "조건") : null, null, [],
        verdict == Verdict.Candidate ? new Explanation("무엇", "효과", "주의") : null, verdict == Verdict.Candidate ? SafetyLevel.Safe : null);

    /// <summary>도구가 보호 위치에 있고 검토 규칙 앱 카드일 때만 바로 실행 가능하다.</summary>
    [Theory]
    [InlineData("appCache.app:npm", true, true)]
    [InlineData("appCache.app:npm", false, false)]
    [InlineData("display.refresh:display-1", true, false)]
    public void OnlyReviewedCacheCardsWithProtectedToolsAreExecutable(string id, bool toolInstalled, bool expected)
    {
        var availability = new CacheToolActionAvailability(() => toolInstalled, reviewedAppIds: ["npm", "pip", "nuget"]);
        Assert.Equal(expected, availability.CanExecuteInApp(Card(id, Verdict.Candidate)));
    }

    /// <summary>앱 식별자는 대소문자를 무시하고, 검토하지 않은 앱·후보가 아닌 카드는 바로 실행 대상이 아니다.</summary>
    [Theory]
    [InlineData("appCache.app:NuGet", Verdict.Candidate, true)]
    [InlineData("appCache.app:PIP", Verdict.Candidate, true)]
    [InlineData("appCache.app:Steam 셰이더 캐시", Verdict.Candidate, false)]
    [InlineData("appCache.app:npm", Verdict.Info, false)]
    [InlineData("appCache.config:npm", Verdict.Candidate, false)]
    public void ReviewedAppIdsAreCaseInsensitiveAndOnlyCandidatesCount(string id, Verdict verdict, bool expected)
    {
        var availability = new CacheToolActionAvailability(() => true, CacheToolActionAvailability.DEFAULT_REVIEWED_APP_IDS);
        Assert.Equal(expected, availability.CanExecuteInApp(Card(id, verdict)));
    }

    /// <summary>정리 창 노출 조건은 도구 위치 확인 함수를 그대로 따른다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CacheToolsAvailableFollowsProtectedLocationCheck(bool installed)
    {
        var availability = new CacheToolActionAvailability(() => installed, CacheToolActionAvailability.DEFAULT_REVIEWED_APP_IDS);
        Assert.Equal(installed, availability.CacheToolsAvailable);
    }

    /// <summary>보호 위치(Program Files) 도구 확인은 실행 파일과 고정 진입 파일의 존재만 본다. pip는 기존 탐색에 보호 위치 표준 경로가 없다.</summary>
    [Fact]
    public void ProtectedLocationCheckUsesStandardProgramFilesPathsOnly()
    {
        var npmOnly = BaseTree()
            .Dir(NODE_DIRECTORY, FakeDirectoryEntrySource.File("node.exe", 1), FakeDirectoryEntrySource.Folder("node_modules"))
            .Dir(NODE_DIRECTORY + @"\node_modules", FakeDirectoryEntrySource.Folder("npm"))
            .Dir(NODE_DIRECTORY + @"\node_modules\npm", FakeDirectoryEntrySource.Folder("bin"))
            .Dir(NODE_DIRECTORY + @"\node_modules\npm\bin", FakeDirectoryEntrySource.File("npm-cli.js", 1));
        Assert.True(SystemCacheToolBackend.IsInProtectedLocation(CacheTool.Npm, PROGRAM_FILES, npmOnly));
        Assert.False(SystemCacheToolBackend.IsInProtectedLocation(CacheTool.NuGetHttp, PROGRAM_FILES, npmOnly));
        Assert.False(SystemCacheToolBackend.IsInProtectedLocation(CacheTool.Pip, PROGRAM_FILES, npmOnly));

        // node.exe만 있고 npm 진입 파일이 없으면 npm 정리를 실행할 수 없다.
        var nodeWithoutNpm = BaseTree().Dir(NODE_DIRECTORY, FakeDirectoryEntrySource.File("node.exe", 1));
        Assert.False(SystemCacheToolBackend.IsInProtectedLocation(CacheTool.Npm, PROGRAM_FILES, nodeWithoutNpm));

        var dotnet = BaseTree().Dir(DOTNET_DIRECTORY, FakeDirectoryEntrySource.File("dotnet.exe", 1));
        Assert.True(SystemCacheToolBackend.IsInProtectedLocation(CacheTool.NuGetHttp, PROGRAM_FILES, dotnet));

        // Program Files 위치를 알 수 없으면(빈 문자열) 어떤 도구도 보호 위치에 있다고 보지 않는다.
        Assert.False(SystemCacheToolBackend.IsInProtectedLocation(CacheTool.NuGetHttp, string.Empty, dotnet));
    }

    /// <summary>Program Files까지 등록한 가짜 디렉터리 트리.</summary>
    private static FakeDirectoryEntrySource BaseTree() => new FakeDirectoryEntrySource()
        .Dir(@"C:\", FakeDirectoryEntrySource.Folder("PF"))
        .Dir(PROGRAM_FILES, FakeDirectoryEntrySource.Folder("nodejs"), FakeDirectoryEntrySource.Folder("dotnet"));
}
