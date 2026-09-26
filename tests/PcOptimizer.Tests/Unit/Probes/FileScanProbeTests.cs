/**
 * @file    : FileScanProbeTests.cs
 * @author  : rudals252
 * @brief   : 파일 스캔 프로브의 정책 무효 시 Failed·ProbeError(순회 없음), 접근 거부가 있을 때 Partial과 사유별 Issue(경로 없음), 루트·임시 위치·미분류 후보 측정값(가짜 대용량 메타데이터), 보호 루트 크기 비보고, 범위·권한·기본 타임아웃을 가짜 서비스로 검증
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Engine.Fakes;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="FileScanProbe"/>를 가짜 환경·열거를 쓰는 실제 <see cref="FileScanService"/>로 검증합니다. 큰 크기는 메타데이터 값일 뿐입니다.
/// </summary>
public sealed class FileScanProbeTests
{
    private const string PROFILE = FileScanServiceTests.PROFILE;
    private const long GB = 1_000_000_000;

    /// <summary>
    /// 측정값을 이름으로 찾는다.
    /// </summary>
    private static Measurement? Find(ProbeResult result, string name)
    {
        return result.Measurements.FirstOrDefault(m => m.Name == name);
    }

    /// <summary>범위·권한·네트워크와 기본 타임아웃(볼륨 예산 × 볼륨 수 + 여유, 상한)을 명시한다.</summary>
    [Fact]
    public void 범위와_기본_타임아웃을_명시한다()
    {
        var probe = new FileScanProbe(FileScanServiceTests.Service(FileScanServiceTests.Tree()), new FakeClock());

        Assert.Equal(FileScanProbeContract.PROBE_ID, probe.Id);
        Assert.Equal(ProbeScope.User, probe.Scope);
        Assert.False(probe.RequiresElevation);
        Assert.False(probe.RequiresNetwork);
        Assert.Equal(FindingCategory.Storage, probe.Category);
        Assert.Equal(ScanOptions.DEFAULT_FILE_SCAN_TIMEOUT_PER_VOLUME + FileScanProbe.FILE_SCAN_TIMEOUT_MARGIN, probe.DefaultTimeout);
        Assert.True(probe.DefaultTimeout <= FileScanProbe.FILE_SCAN_TIMEOUT_CAP);
    }

    /// <summary>보호 정책이 무효이면 순회 없이 Failed + ProbeError이고 요약은 리소스 문장(정책 오류 코드 포함)이다.</summary>
    [Fact]
    public async Task 정책이_무효이면_실패로_알린다()
    {
        var source = FileScanServiceTests.Tree();
        var probe = new FileScanProbe(FileScanServiceTests.Service(source, policy: "{ broken"), new FakeClock());

        var result = await probe.RunAsync(FileScanServiceTests.Context(), CancellationToken.None);

        Assert.Equal(ProbeStatus.Failed, result.Status);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(CannotVerifyReason.ProbeError, issue.Reason);
        Assert.Contains("MalformedJson", issue.Summary, StringComparison.Ordinal);
        Assert.Contains("protect.json", issue.Summary, StringComparison.Ordinal);
        Assert.Equal(FileScanProbeContract.POLICY_INVALID, (Find(result, FileScanProbeContract.POLICY_STATE)?.Value as TextValue)?.Value);
        Assert.Empty(source.Enumerated);
    }

    /// <summary>
    /// 접근 거부가 있으면 Partial과 사유별 개수 Issue(경로 없음)를 내고, 루트·임시 위치 측정값을 낸다. 보호 폴더 크기 측정값은 없다.
    /// </summary>
    [Fact]
    public async Task 부분_관측과_측정값을_낸다()
    {
        var probe = new FileScanProbe(FileScanServiceTests.Service(FileScanServiceTests.Tree()), new FakeClock());

        var result = await probe.RunAsync(FileScanServiceTests.Context(), CancellationToken.None);

        Assert.Equal(ProbeStatus.Partial, result.Status);
        var denied = Assert.Single(result.Issues, issue => issue.Reason == CannotVerifyReason.AccessDenied);
        Assert.DoesNotContain(PROFILE, denied.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"C:\", string.Concat(result.Issues.Select(i => i.Summary)), StringComparison.OrdinalIgnoreCase);

        var profileIndex = Enumerable.Range(0, 7).Single(index =>
            (Find(result, FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, index, FileScanProbeContract.FIELD_ID))?.Value as TextValue)?.Value == FileScanProbeContract.ROOT_PROFILE);
        var profileBytes = Find(result, FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, profileIndex, FileScanProbeContract.FIELD_BYTES))!;
        Assert.Equal(new IntegerValue(415), profileBytes.Value);
        Assert.Equal(MeasurementQuality.Partial, profileBytes.Quality);
        Assert.Equal(new IntegerValue(1), Find(result, FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, profileIndex, FileScanProbeContract.FIELD_SKIP_PROTECTED))!.Value);
        Assert.DoesNotContain(result.Measurements, m => m.Value is TextValue text && text.Value.EndsWith("private.docx", StringComparison.Ordinal));
        Assert.Equal(new IntegerValue(5), Find(result, FileScanProbeContract.TEMP_COUNT)!.Value);
        Assert.Equal(new IntegerValue(0), Find(result, FileScanProbeContract.UNCLASSIFIED_COUNT)!.Value);
    }

    /// <summary>프로필 아래 1,000,000,000바이트 이상 폴더(가짜 메타데이터)는 미분류 후보 측정값이 되고, 임시 폴더 안의 큰 파일은 후보가 아니다.</summary>
    [Fact]
    public async Task 대용량_미분류_후보를_측정값으로_낸다()
    {
        var source = FileScanServiceTests.Tree()
            .Dir(PROFILE, FakeDirectoryEntrySource.Folder("Documents"), FakeDirectoryEntrySource.Folder("AppData"), FakeDirectoryEntrySource.Folder("Games"))
            .Dir(PROFILE + @"\Games", FakeDirectoryEntrySource.File("world.pak", 2 * GB), FakeDirectoryEntrySource.File("intro.mp4", GB / 2))
            .Dir(PROFILE + @"\AppData\Local\Temp", FakeDirectoryEntrySource.File("huge.tmp", 9 * GB));
        var probe = new FileScanProbe(FileScanServiceTests.Service(source), new FakeClock());

        var result = await probe.RunAsync(FileScanServiceTests.Context(), CancellationToken.None);

        Assert.Equal(new IntegerValue(1), Find(result, FileScanProbeContract.UNCLASSIFIED_COUNT)!.Value);
        string Name(string field) => FileScanProbeContract.Name(FileScanProbeContract.UNCLASSIFIED_PREFIX, 0, field);
        Assert.Equal(new TextValue(PROFILE + @"\Games"), Find(result, Name(FileScanProbeContract.FIELD_PATH))!.Value);
        Assert.Equal(new IntegerValue((2 * GB) + (GB / 2)), Find(result, Name(FileScanProbeContract.FIELD_BYTES))!.Value);
        var extensions = Assert.IsType<TextListValue>(Find(result, Name(FileScanProbeContract.FIELD_TOP_EXTENSIONS))!.Value);
        Assert.Equal([".pak=2000000000", ".mp4=500000000"], extensions.Values);
        Assert.Equal(new TextValue(FakeDirectoryEntrySource.DEFAULT_WRITE.ToString("o", System.Globalization.CultureInfo.InvariantCulture)), Find(result, Name(FileScanProbeContract.FIELD_NEWEST_WRITE_UTC))!.Value);
    }

    /// <summary>
    /// 보호 루트는 경로가 아니라 출처 이름과 개수만 측정값으로 내고, 위치를 못 읽은 항목 수를 기록한다.
    /// 위치를 못 읽은 문서 폴더의 기본 위치에 있는 큰 파일(가짜 메타데이터)은 미분류 후보가 되지 않는다.
    /// </summary>
    [Fact]
    public async Task 보호_루트는_출처_이름만_내고_기본_위치를_후보로_올리지_않는다()
    {
        var source = FileScanServiceTests.Tree().Dir(PROFILE + @"\Documents", FakeDirectoryEntrySource.File("huge.vhdx", 5 * GB));
        var environment = new FakePathEnvironment { Profile = PROFILE }
            .WithVariable("ProgramData", @"C:\ProgramData")
            .WithVariable("TEMP", PROFILE + @"\AppData\Local\Temp")
            .WithVariable("SystemRoot", @"C:\Windows")
            .WithVariable("LocalAppData", PROFILE + @"\AppData\Local");
        var probe = new FileScanProbe(FileScanServiceTests.Service(source, environment: environment), new FakeClock());

        var result = await probe.RunAsync(FileScanServiceTests.Context(), CancellationToken.None);

        var labels = Assert.IsType<TextListValue>(Find(result, FileScanProbeContract.PROTECTED_ROOT_LABELS)!.Value);
        Assert.Equal([FileScanProbeContract.PROTECTED_LABEL_KNOWN_FOLDER + "Documents"], labels.Values);
        Assert.Equal(new IntegerValue(1), Find(result, FileScanProbeContract.PROTECTED_UNRESOLVED_COUNT)!.Value);
        Assert.Equal(new IntegerValue(0), Find(result, FileScanProbeContract.UNCLASSIFIED_COUNT)!.Value);
        Assert.DoesNotContain(result.Measurements, m => m.Value is TextValue text && text.Value.EndsWith(@"\Documents", StringComparison.OrdinalIgnoreCase));
    }
}
