/**
 * @file    : CacheToolEnvironmentTests.cs
 * @author  : rudals252
 * @brief   : npm·pip·NuGet 자식 프로세스 시작 정보에서 코드 주입이 가능한 환경 변수(시작 훅·추가 deps·공유 저장소·COMPlus_·NODE_OPTIONS·PYTHON*)가 빠지고, 사용자 캐시 위치를 고르는 NUGET_·NPM_CONFIG_·PIP_와 무관한 변수는 남는지 검증
 */

// 기본 패키지
using System.Diagnostics;

// 사용자 패키지
using PcOptimizer.Probes.Actions;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// 현재 프로세스 환경 변수를 바꾸는 테스트를 다른 테스트와 동시에 돌리지 않기 위한 컬렉션 정의입니다.
/// </summary>
[CollectionDefinition(nameof(ProcessEnvironmentCollection), DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection
{
}

/// <summary>
/// <see cref="CacheToolProcess.CreateStartInfo"/>의 환경 변수 정리를 검증합니다. 프로세스는 시작하지 않습니다.
/// </summary>
[Collection(nameof(ProcessEnvironmentCollection))]
public sealed class CacheToolEnvironmentTests
{
    /// <summary>심는 값(존재하지 않는 경로라 실행에 쓰이지 않음).</summary>
    private const string FAKE_VALUE = @"C:\pcopt-test-not-a-real-path\fixture";

    /// <summary>테스트가 현재 프로세스에 심는 주입 가능 변수.</summary>
    private static readonly string[] INJECTION_KEYS =
    [
        "DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "DOTNET_SHARED_STORE", "COMPlus_EnableDiagnostics", "COMPlus_DbgJITDebugLaunchSetting",
        "NODE_OPTIONS", "PYTHONSTARTUP", "PYTHONPATH", "PYTHONHOME",
    ];

    /// <summary>남아야 하는 변수(사용자 캐시 위치 선택 변수와 무관한 변수).</summary>
    private static readonly string[] KEPT_KEYS = ["NUGET_PACKAGES", "NPM_CONFIG_CACHE", "PIP_CACHE_DIR", "PCOPT_TEST_UNRELATED"];

    /// <summary>
    /// 현재 프로세스에 주입 가능 변수를 심어도 도구 시작 정보의 환경에는 없고, 캐시 위치 변수와 무관한 변수는 그대로 남는다(최종 리뷰 이월 5b).
    /// </summary>
    [Theory]
    [InlineData(CacheTool.Npm, false)]
    [InlineData(CacheTool.Npm, true)]
    [InlineData(CacheTool.Pip, false)]
    [InlineData(CacheTool.Pip, true)]
    [InlineData(CacheTool.NuGetHttp, false)]
    [InlineData(CacheTool.NuGetHttp, true)]
    public void InjectionCapableVariablesAreRemovedAndOthersKept(CacheTool tool, bool clear)
    {
        var location = new CacheToolLocation(tool, @"C:\Program Files\nodejs\node.exe", @"C:\Users\tester\cache", "hash", @"C:\Program Files\nodejs\npm-cli.js");
        var previous = INJECTION_KEYS.Concat(KEPT_KEYS).ToDictionary(key => key, Environment.GetEnvironmentVariable);
        try
        {
            foreach (var key in previous.Keys)
            {
                Environment.SetEnvironmentVariable(key, FAKE_VALUE);
            }

            ProcessStartInfo start = CacheToolProcess.CreateStartInfo(location, clear);

            foreach (var key in INJECTION_KEYS)
            {
                Assert.False(start.Environment.ContainsKey(key), $"{key}가 남아 있다");
            }

            Assert.DoesNotContain(start.Environment.Keys, key => key.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase));
            foreach (var key in KEPT_KEYS)
            {
                Assert.True(start.Environment.TryGetValue(key, out var value), $"{key}가 지워졌다");
                Assert.Equal(FAKE_VALUE, value);
            }
        }
        finally
        {
            foreach (var (key, value) in previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
