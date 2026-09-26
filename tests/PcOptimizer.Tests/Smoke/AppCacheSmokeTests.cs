/**
 * @file    : AppCacheSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 이 PC에서 실제 앱 캐시 프로브(포함 규칙·공유 파일 스캔·레지스트리·앱 설정)를 실행해 Success 또는 Partial인지 확인하고 규칙 해석 시간·지원/미지원 요약·탐지 규칙 수·규칙별 관측을 출력(파일 삭제·앱 실행 없음)
 */

// 기본 패키지
using System.Diagnostics;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Applications;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Engine;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>
/// 앱 캐시 실제 PC 스모크 테스트입니다. 조회만 합니다.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class AppCacheSmokeTests(ITestOutputHelper output)
{
    /// <summary>실제 앱 캐시 프로브가 Success 또는 Partial로 끝나고, 규칙 목록 무결성을 확인했으며 요약·규칙별 결과를 낸다.</summary>
    [Fact]
    public async Task 실제_앱_캐시_프로브를_실행한다()
    {
        var parseWatch = Stopwatch.StartNew();
        var catalog = RuleCatalogLoader.CreateEmbedded().Load();
        parseWatch.Stop();
        output.WriteLine($"catalog state={catalog.State} load+parse={parseWatch.ElapsedMilliseconds}ms (parse {catalog.ParseElapsed.TotalMilliseconds:0}ms)");

        var probe = AppCacheProbe.CreateDefault(FileScanService.CreateDefault());
        var context = new ScanContext(Guid.NewGuid(), EngineTestData.USER with { IsElevated = PcOptimizer.App.Services.ScanService.IsCurrentProcessElevated() }, false, DateTimeOffset.UtcNow);
        var watch = Stopwatch.StartNew();
        var result = await probe.RunAsync(context, CancellationToken.None);
        watch.Stop();
        var snapshot = new ScanSnapshot(context.ScanId, [result]);

        output.WriteLine($"status={result.Status} elapsed={watch.ElapsedMilliseconds}ms elevated={context.IsElevated}");
        foreach (var issue in result.Issues)
        {
            output.WriteLine($"issue {issue.Reason}: {issue.Summary}");
        }

        foreach (var name in new[]
        {
            AppCacheProbeContract.WINAPP2_VERSION, AppCacheProbeContract.COMMUNITY_TOTAL, AppCacheProbeContract.COMMUNITY_SUPPORTED,
            AppCacheProbeContract.SUPPLEMENT_TOTAL, AppCacheProbeContract.SUPPLEMENT_SUPPORTED, AppCacheProbeContract.UNSUPPORTED_BY_REASON,
            AppCacheProbeContract.PARSE_MS, AppCacheProbeContract.DETECTED_COUNT, AppCacheProbeContract.NOT_DETECTED_COUNT,
            AppCacheProbeContract.DETECTION_UNKNOWN_COUNT, AppCacheProbeContract.RUNTIME_UNSUPPORTED_BY_REASON, AppCacheProbeContract.ELAPSED_MS,
            AppCacheProbeContract.RULE_COUNT, AppCacheProbeContract.SQUIRREL_COUNT, AppCacheProbeContract.CONFIG_COUNT,
        })
        {
            output.WriteLine($"{name} = {Describe(snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, name)?.Value)}");
        }

        var findings = new RuleCatalogSummaryRule().Evaluate(snapshot)
            .Concat(new AppCacheRule().Evaluate(snapshot))
            .Concat(new SquirrelVersionFoldersRule().Evaluate(snapshot))
            .ToList();
        foreach (var finding in findings)
        {
            output.WriteLine($"{finding.Id} | {finding.Verdict} {finding.CannotVerifyReason?.ToString() ?? string.Empty} | {finding.Title}");
        }

        Assert.True(catalog.IsVerified);
        Assert.Contains(result.Status, new[] { ProbeStatus.Success, ProbeStatus.Partial });
        Assert.Equal(AppCacheProbeContract.CATALOG_VERIFIED, SnapshotText(snapshot, AppCacheProbeContract.CATALOG_STATE));
        Assert.Contains(findings, f => f.Id == RuleCatalogSummaryRule.FINDING_ID && f.Verdict == Verdict.Info);
        Assert.DoesNotContain(findings, f => f.Verdict == Verdict.Candidate);
    }

    /// <summary>
    /// 일반 권한 검사처럼(컨텍스트의 IsElevated=false) 실행해 이 PC의 실제 앱 설정 리더(npm·pip·NuGet·Steam·Adobe) 상태를 확인한다.
    /// 설정 경로 값은 출력하지 않고 상태·값 출처만 출력한다.
    /// </summary>
    [Fact]
    public async Task 일반_권한_컨텍스트로_앱_설정_리더를_실행한다()
    {
        var probe = AppCacheProbe.CreateDefault(FileScanService.CreateDefault());
        var context = new ScanContext(Guid.NewGuid(), EngineTestData.USER with { IsElevated = false }, false, DateTimeOffset.UtcNow);

        var result = await probe.RunAsync(context, CancellationToken.None);
        var snapshot = new ScanSnapshot(context.ScanId, [result]);

        output.WriteLine($"status={result.Status}");
        var count = snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, AppCacheProbeContract.CONFIG_COUNT)?.Value is IntegerValue value ? value.Value : 0;
        for (var index = 0; index < count; index++)
        {
            string Field(string field) => SnapshotText(snapshot, AppCacheProbeContract.Name(AppCacheProbeContract.CONFIG_PREFIX, index, field)) ?? string.Empty;
            output.WriteLine($"config {Field(AppCacheProbeContract.FIELD_APP)}: state={Field(AppCacheProbeContract.FIELD_STATE)} origin={Field(AppCacheProbeContract.FIELD_CONFIG_ORIGIN)}");
        }

        foreach (var finding in new AppCacheRule().Evaluate(snapshot).Where(f => f.Id.StartsWith(AppCacheRule.FINDING_ID_PREFIX + "supplement:", StringComparison.Ordinal)
            || f.Id.StartsWith(AppCacheRule.CONFIG_FINDING_ID_PREFIX, StringComparison.Ordinal)))
        {
            output.WriteLine($"{finding.Id} | {finding.Verdict} | {finding.Title}");
        }

        Assert.Contains(result.Status, new[] { ProbeStatus.Success, ProbeStatus.Partial });
        Assert.Equal(new BooleanValue(false), snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, AppCacheProbeContract.ELEVATED_DEFAULTS_ONLY)!.Value);
    }

    /// <summary>
    /// 측정값을 한 줄로 쓴다.
    /// </summary>
    private static string Describe(MeasurementValue? value)
    {
        return value switch
        {
            IntegerValue integer => integer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            TextValue text => text.Value,
            TextListValue list => "[" + string.Join(", ", list.Values) + "]",
            BooleanValue boolean => boolean.Value.ToString(),
            null => "(없음)",
            _ => value.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    /// 문자열 측정값.
    /// </summary>
    private static string? SnapshotText(ScanSnapshot snapshot, string name)
    {
        return snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, name)?.Value is TextValue text ? text.Value : null;
    }
}
