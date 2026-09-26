/**
 * @file    : FileScanSmokeTests.cs
 * @author  : rudals252
 * @brief   : [Smoke] 테스트 폴더에 mklink /J 정션과 mklink /H 하드링크를 실제로 만들어 건너뜀·한 번만 세기를 확인하고, Win32 파일 ID·할당 크기 P/Invoke와 이 PC의 실제 파일 스캔 프로브(Success 또는 Partial, 루트 합계·건너뜀·볼륨·미분류 수 출력)를 실행
 */

// 기본 패키지
using System.Diagnostics;
using System.IO;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>
/// 파일 스캔 실제 PC 스모크 테스트입니다. 링크 테스트는 테스트 전용 임시 폴더 안에서만 만들고 지웁니다.
/// </summary>
[Trait("Category", "Smoke")]
public sealed class FileScanSmokeTests(ITestOutputHelper output) : IDisposable
{
    private const string CMD = "cmd.exe";
    private static readonly TimeSpan BUDGET = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PROCESS_TIMEOUT = TimeSpan.FromSeconds(30);
    private readonly TestDirectory _directory = new();

    /// <summary>
    /// 테스트 폴더를 정리한다(정션은 링크만 지움).
    /// </summary>
    public void Dispose()
    {
        _directory.Dispose();
    }

    /// <summary>
    /// cmd.exe의 mklink를 실행한다.
    /// </summary>
    private static void MkLink(string option, string link, string target)
    {
        var start = new ProcessStartInfo(CMD) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "/c", "mklink", option, link, target })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        Assert.True(process.WaitForExit(PROCESS_TIMEOUT));
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd());
    }

    /// <summary>실제 정션(mklink /J)은 따라가지 않고 reparse로 세며, 대상 폴더 크기는 한 번만 센다.</summary>
    [Fact]
    public async Task 실제_정션은_건너뛰고_센다()
    {
        _directory.File(@"target\payload.bin", 7);
        MkLink("/J", _directory.PathOf("link"), _directory.PathOf("target"));
        Assert.True(new DirectoryInfo(_directory.PathOf("link")).Attributes.HasFlag(FileAttributes.ReparsePoint));

        var result = await FileSystemScanner.CreateDefault().ScanAsync(
            [new ScanTarget("root", _directory.Root)], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);

        var totals = Assert.Single(result.Roots).Totals!;
        output.WriteLine($"bytes={totals.Bytes} files={totals.FileCount} reparse={totals.Skips.Reparse}");
        Assert.Equal(7, totals.Bytes);
        Assert.Equal(1, totals.FileCount);
        Assert.Equal(1, totals.Skips.Reparse);
        Assert.False(result.TryGetDirectoryTotals(_directory.PathOf("link"), out _));
    }

    /// <summary>실제 하드링크(mklink /H)는 64MiB 이상이면 파일 ID가 같아 한 번만 센다(Win32 FILE_ID_INFO 조회 확인).</summary>
    [Fact]
    public async Task 실제_하드링크는_한_번만_센다()
    {
        var big = _directory.PathOf(@"one\big.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(big)!);
        using (var stream = new FileStream(big, FileMode.CreateNew))
        {
            stream.SetLength(FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES);
        }

        Directory.CreateDirectory(_directory.PathOf("two"));
        MkLink("/H", _directory.PathOf(@"two\same.bin"), big);
        var first = Win32FileIdentityReader.Instance.TryGetIdentity(big);
        var second = Win32FileIdentityReader.Instance.TryGetIdentity(_directory.PathOf(@"two\same.bin"));
        output.WriteLine($"id1={first} id2={second}");
        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, Win32FileIdentityReader.Instance.TryGetIdentity(_directory.File(@"three\other.bin", 1)));
        Assert.Equal(1, Win32FileIdentityReader.Instance.TryGetAllocatedSize(_directory.PathOf(@"three\other.bin")) is > 0 ? 1 : 0);

        var result = await FileSystemScanner.CreateDefault().ScanAsync(
            [new ScanTarget("root", _directory.Root)], new ResolvedProtection([]), [], BUDGET, CancellationToken.None);

        var totals = Assert.Single(result.Roots).Totals!;
        output.WriteLine($"bytes={totals.Bytes} files={totals.FileCount} hardLinkDuplicates={result.HardLinkDuplicateCount}");
        Assert.Equal(FileSystemScanner.HARD_LINK_CHECK_MIN_BYTES + 1, totals.Bytes);
        Assert.Equal(2, totals.FileCount);
        Assert.Equal(1, result.HardLinkDuplicateCount);
    }

    /// <summary>
    /// 이 PC에서 실제 파일 스캔 프로브를 실행하면 Success 또는 Partial이다(Failed 아님). 루트 합계·건너뜀·볼륨·임시 위치·미분류 수를 출력한다.
    /// </summary>
    [Fact]
    public async Task 실제_파일_스캔_프로브는_실패하지_않는다()
    {
        var context = new ScanContext(
            Guid.NewGuid(), new UserContext("smoke-user", ScanService.IsCurrentProcessElevated()), OnlineCheckRequested: false, DateTimeOffset.UtcNow);
        var probe = new FileScanProbe(FileScanService.CreateDefault(), SystemClock.Instance);
        var stopwatch = Stopwatch.StartNew();

        var result = await probe.RunAsync(context, CancellationToken.None);

        output.WriteLine($"status={result.Status} elevated={context.IsElevated} wall={stopwatch.Elapsed.TotalSeconds:0.0}s defaultTimeout={probe.DefaultTimeout}");
        foreach (var issue in result.Issues)
        {
            output.WriteLine($"issue {issue.Reason}: {issue.Summary}");
        }

        foreach (var measurement in result.Measurements.Where(m => !m.Name.EndsWith(".path", StringComparison.Ordinal) && m.Name != FileScanProbeContract.PROTECTED_ROOT_PATHS))
        {
            var value = measurement.Value switch
            {
                IntegerValue integer => integer.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BooleanValue boolean => boolean.Value.ToString(),
                TextValue text => text.Value,
                TextListValue list => string.Join(" | ", list.Values),
                DecimalValue number => number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => string.Empty,
            };
            output.WriteLine($"{measurement.Name} = {value} [{measurement.Quality}]");
        }

        Assert.Contains(result.Status, new[] { ProbeStatus.Success, ProbeStatus.Partial });
        Assert.Equal(FileScanProbeContract.POLICY_VALID, (result.Measurements.Single(m => m.Name == FileScanProbeContract.POLICY_STATE).Value as TextValue)?.Value);
    }
}
