/**
 * @file    : FileAppLoggerTests.cs
 * @author  : rudals252
 * @brief   : 파일 로거의 일 단위 파일·예외 형식 이름만 기록·개인 경로 치환·7일 보관·총량 상한 단위 테스트(임시 폴더 사용)
 */

// 기본 패키지
using System.IO;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="FileAppLogger"/>를 테스트 전용 임시 폴더에서 검증합니다.
/// </summary>
public sealed class FileAppLoggerTests : IDisposable
{
    private const string SECRET_MESSAGE = @"C:\Users\Kimtester\secret.txt 파일을 열 수 없음";
    private const long SMALL_CAP_BYTES = 4096;
    private const int FILLER_BYTES = 3000;
    private const int FLOOD_LINE_COUNT = 200;
    private const int FLOOD_LINE_LENGTH = 100;

    private static readonly PersonalDataScrubber SCRUBBER = new(@"C:\Users\Kimtester", "Kimtester", "DESKTOP-FAKE01");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"pcoptimizer-log-test-{Guid.NewGuid():N}");
    private readonly FakeClock _clock = new() { UtcNow = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero) };

    /// <summary>
    /// 임시 폴더를 지운다.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>
    /// 테스트용 로거를 만든다.
    /// </summary>
    private FileAppLogger CreateLogger(long maxTotalBytes = FileAppLogger.DEFAULT_MAX_TOTAL_BYTES)
    {
        return new FileAppLogger(_directory, _clock, SCRUBBER, FileAppLogger.DEFAULT_RETENTION, maxTotalBytes);
    }

    /// <summary>
    /// 오늘 로그 파일 내용을 읽는다.
    /// </summary>
    private string ReadToday()
    {
        return File.ReadAllText(Path.Combine(_directory, FileAppLogger.FileNameFor(_clock.UtcNow)));
    }

    /// <summary>날짜별 파일에 기록한다.</summary>
    [Fact]
    public void 날짜별_파일에_기록한다()
    {
        var logger = CreateLogger();

        logger.Info("Test", "first");
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        logger.Info("Test", "second");

        Assert.Equal(2, Directory.GetFiles(_directory).Length);
        Assert.Contains("second", ReadToday(), StringComparison.Ordinal);
        Assert.DoesNotContain("first", ReadToday(), StringComparison.Ordinal);
    }

    /// <summary>예외는 형식 이름만 남기고 메시지·스택은 남기지 않는다.</summary>
    [Fact]
    public void 예외는_형식_이름만_기록한다()
    {
        var logger = CreateLogger();
        Exception thrown;
        try
        {
            throw new IOException(SECRET_MESSAGE);
        }
        catch (IOException ex)
        {
            thrown = ex;
        }

        logger.Error("Test", "open failed", thrown);

        var text = ReadToday();
        Assert.Contains(nameof(IOException), text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(예외는_형식_이름만_기록한다), text, StringComparison.Ordinal);
    }

    /// <summary>메시지 속 개인 경로·사용자명·PC명은 치환한다.</summary>
    [Fact]
    public void 메시지의_개인_정보를_치환한다()
    {
        var logger = CreateLogger();

        logger.Warn("Test", SECRET_MESSAGE + " on DESKTOP-FAKE01");

        var text = ReadToday();
        Assert.DoesNotContain("Kimtester", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DESKTOP-FAKE01", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>보관 기간(7일)보다 오래된 앱 로그만 지우고 다른 파일은 건드리지 않는다.</summary>
    [Fact]
    public void 오래된_앱_로그만_지운다()
    {
        Directory.CreateDirectory(_directory);
        var old = Path.Combine(_directory, FileAppLogger.FileNameFor(_clock.UtcNow.AddDays(-8)));
        var recent = Path.Combine(_directory, FileAppLogger.FileNameFor(_clock.UtcNow.AddDays(-6)));
        var foreign = Path.Combine(_directory, "not-ours.txt");
        File.WriteAllText(old, "old");
        File.WriteAllText(recent, "recent");
        File.WriteAllText(foreign, "keep");

        CreateLogger().Info("Test", "now");

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(foreign));
    }

    /// <summary>총량 상한을 넘으면 오래된 앱 로그부터 지운다.</summary>
    [Fact]
    public void 총량_상한을_넘으면_오래된_로그부터_지운다()
    {
        Directory.CreateDirectory(_directory);
        var older = Path.Combine(_directory, FileAppLogger.FileNameFor(_clock.UtcNow.AddDays(-2)));
        var newer = Path.Combine(_directory, FileAppLogger.FileNameFor(_clock.UtcNow.AddDays(-1)));
        File.WriteAllText(older, new string('a', FILLER_BYTES));
        File.WriteAllText(newer, new string('b', FILLER_BYTES));

        CreateLogger(SMALL_CAP_BYTES).Info("Test", "now");

        Assert.False(File.Exists(older));
        Assert.True(File.Exists(newer));
        var total = Directory.GetFiles(_directory).Sum(file => new FileInfo(file).Length);
        Assert.True(total <= SMALL_CAP_BYTES, $"총 {total}바이트");
    }

    /// <summary>오늘 파일이 상한에 닿으면 더 쓰지 않는다(상한을 넘기지 않음).</summary>
    [Fact]
    public void 상한에_닿으면_더_쓰지_않는다()
    {
        var logger = CreateLogger(SMALL_CAP_BYTES);

        for (var i = 0; i < FLOOD_LINE_COUNT; i++)
        {
            logger.Info("Test", new string('x', FLOOD_LINE_LENGTH));
        }

        var total = Directory.GetFiles(_directory).Sum(file => new FileInfo(file).Length);
        Assert.True(total <= SMALL_CAP_BYTES, $"총 {total}바이트");
    }
}
