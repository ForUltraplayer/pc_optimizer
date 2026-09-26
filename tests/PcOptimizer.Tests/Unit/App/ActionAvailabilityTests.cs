/**
 * @file    : ActionAvailabilityTests.cs
 * @author  : rudals252
 * @brief   : 앱 안에서 바로 실행할 수 있는 후보 판정(도구 캐시 카드·보호 위치 도구 조건)과 보호 위치 도구 존재 확인(실행 규칙과 같은 후보·판정, 프로세스 실행 없음)을 검증
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
    private const string PYTHON_DIRECTORY = @"C:\PF\Python312";
    private const string PROGRAM_FILES_X86 = @"C:\PF86";
    private const string X86_NODE_DIRECTORY = @"C:\PF86\nodejs";

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

    /// <summary>노출 판정은 실행 규칙과 같다: 표준 위치 npm은 진입 파일까지 있어야 하고, 보호 위치를 모르면 어떤 도구도 노출하지 않는다.</summary>
    [Fact]
    public void ProtectedLocationCheckMatchesExecutionRule()
    {
        var npmOnly = Tree(NODE_DIRECTORY + @"\node.exe", NODE_DIRECTORY + @"\node_modules\npm\bin\npm-cli.js");
        Assert.True(IsExposed(CacheTool.Npm, null, npmOnly));
        Assert.False(IsExposed(CacheTool.NuGetHttp, null, npmOnly));
        Assert.False(IsExposed(CacheTool.Pip, null, npmOnly));

        // node.exe만 있고 npm 진입 파일이 없으면 npm 정리를 실행할 수 없다.
        Assert.False(IsExposed(CacheTool.Npm, null, Tree(NODE_DIRECTORY + @"\node.exe")));

        var dotnet = Tree(DOTNET_DIRECTORY + @"\dotnet.exe");
        Assert.True(IsExposed(CacheTool.NuGetHttp, null, dotnet));

        // 보호 위치를 알 수 없으면 어떤 도구도 보호 위치에 있다고 보지 않는다.
        Assert.False(SystemCacheToolBackend.AnyToolInProtectedLocation(new FakePathEnvironment(), _ => null, DOTNET_DIRECTORY, dotnet));
    }

    /// <summary>Program Files의 Python만 PATH에 있어도 pip 정리를 실행할 수 있으므로 정리 창을 노출한다.</summary>
    [Fact]
    public void PythonOnlyInProgramFilesIsExposed()
    {
        var python = Tree(PYTHON_DIRECTORY + @"\python.exe");
        Assert.True(IsExposed(CacheTool.Pip, PYTHON_DIRECTORY, python));
        Assert.True(SystemCacheToolBackend.AnyToolInProtectedLocation(new FakePathEnvironment(), ProgramRoot, PYTHON_DIRECTORY, python));
    }

    /// <summary>Program Files (x86)에만 있는 도구도 보호 위치이므로 노출한다(PATH 없이 표준 위치).</summary>
    [Fact]
    public void X86OnlyToolIsExposed()
    {
        var node = Tree(X86_NODE_DIRECTORY + @"\node.exe", X86_NODE_DIRECTORY + @"\node_modules\npm\bin\npm-cli.js");
        Assert.True(SystemCacheToolBackend.AnyToolInProtectedLocation(new FakePathEnvironment(), ProgramRoot, null, node));
    }

    /// <summary>사용자 폴더 도구만 있으면(PATH에 있어도) 관리자 권한 앱이 실행하지 않으므로 노출하지 않는다. 빈 PATH·도구 없음도 같다.</summary>
    [Fact]
    public void UserFolderToolsOnlyAreNotExposed()
    {
        const string USER_PYTHON = @"C:\Users\kim\AppData\Local\Programs\Python\Python312";
        const string USER_NODE = @"C:\Users\kim\AppData\Roaming\nvm\v20";
        var user = Tree(USER_PYTHON + @"\python.exe", USER_NODE + @"\node.exe", USER_NODE + @"\node_modules\npm\bin\npm-cli.js");
        Assert.False(SystemCacheToolBackend.AnyToolInProtectedLocation(new FakePathEnvironment(), ProgramRoot, USER_PYTHON + ";" + USER_NODE, user));
        Assert.False(SystemCacheToolBackend.AnyToolInProtectedLocation(new FakePathEnvironment(), ProgramRoot, string.Empty, Tree()));
    }

    /// <summary>도구 하나의 노출 판정(테스트 보호 위치 사용).</summary>
    private static bool IsExposed(CacheTool tool, string? path, FakeDirectoryEntrySource tree)
        => SystemCacheToolBackend.IsToolInProtectedLocation(tool, new FakePathEnvironment(), ProgramRoot, path, tree);

    /// <summary>테스트 보호 위치: %ProgramFiles%·%ProgramW6432% = C:\PF, %ProgramFiles(x86)% = C:\PF86.</summary>
    private static string? ProgramRoot(string name) => name switch
    {
        "ProgramFiles" or "ProgramW6432" => PROGRAM_FILES,
        "ProgramFiles(x86)" => PROGRAM_FILES_X86,
        _ => null,
    };

    /// <summary>주어진 파일과 모든 상위 폴더를 일반 항목으로 등록한 가짜 디렉터리 트리.</summary>
    private static FakeDirectoryEntrySource Tree(params string[] files)
    {
        var directories = new Dictionary<string, List<PcOptimizer.Probes.Storage.DirectoryEntry>>(StringComparer.OrdinalIgnoreCase);
        void AddDirectory(string path)
        {
            if (directories.ContainsKey(path)) { return; }
            directories[path] = [];
            if (System.IO.Path.GetDirectoryName(path) is not { } parent) { return; }
            AddDirectory(parent);
            directories[parent].Add(FakeDirectoryEntrySource.Folder(System.IO.Path.GetFileName(path)));
        }
        AddDirectory(@"C:\");
        foreach (var file in files)
        {
            var parent = System.IO.Path.GetDirectoryName(file)!;
            AddDirectory(parent);
            directories[parent].Add(FakeDirectoryEntrySource.File(System.IO.Path.GetFileName(file), 1));
        }
        var tree = new FakeDirectoryEntrySource();
        foreach (var (directory, entries) in directories) { tree.Dir(directory, [.. entries]); }
        return tree;
    }
}
