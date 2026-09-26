/**
 * @file    : FileAppLogger.cs
 * @author  : rudals252
 * @brief   : %LocalAppData%\PcOptimizer\logs 일 단위 롤링 파일 로거(7일 보관·총 20MiB 상한·예외 형식 이름만·개인정보 치환). 앱 로그 파일만 순환
 */

// 기본 패키지
using System.Globalization;
using System.IO;
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;

namespace PcOptimizer.App.Services;

/// <summary>
/// 프로젝트 공용 로거의 파일 구현입니다(스펙 §8).
/// <list type="bullet">
/// <item>파일: <c>pcoptimizer-yyyyMMdd.log</c>(UTC 날짜), 날짜가 바뀌면 새 파일.</item>
/// <item>보관 기간이 지난 앱 로그는 지우고, 총량이 상한을 넘으면 오래된 앱 로그부터 지운다. 상한에 닿으면 더 쓰지 않는다.</item>
/// <item>예외는 형식 이름만 남기고 메시지·스택은 남기지 않는다. 메시지의 개인 경로·사용자명·PC명은 치환한다.</item>
/// <item>로그 쓰기 실패는 앱 동작을 멈추지 않도록 삼킨다.</item>
/// </list>
/// </summary>
public sealed class FileAppLogger : IAppLogger
{
    /// <summary>기본 총량 상한(20MiB).</summary>
    public const long DEFAULT_MAX_TOTAL_BYTES = 20L * 1024 * 1024;

    /// <summary>로그 파일 이름 접두사.</summary>
    public const string FILE_PREFIX = "pcoptimizer-";

    /// <summary>로그 파일 확장자.</summary>
    public const string FILE_EXTENSION = ".log";

    /// <summary>기본 보관 기간(7일).</summary>
    public static readonly TimeSpan DEFAULT_RETENTION = TimeSpan.FromDays(7);

    private const string DATE_FORMAT = "yyyyMMdd";
    private const string TIMESTAMP_FORMAT = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    private const string APP_FOLDER = "PcOptimizer";
    private const string LOGS_FOLDER = "logs";
    private const string LEVEL_DEBUG = "DEBUG";
    private const string LEVEL_INFO = "INFO";
    private const string LEVEL_WARN = "WARN";
    private const string LEVEL_ERROR = "ERROR";

    private static readonly Encoding FILE_ENCODING = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly object _sync = new();
    private readonly string _directory;
    private readonly IClock _clock;
    private readonly PersonalDataScrubber _scrubber;
    private readonly TimeSpan _retention;
    private readonly long _maxTotalBytes;

    private string? _currentFileName;
    private long _totalBytes;

    /// <summary>
    /// 파일 로거를 만듭니다. 폴더는 첫 기록 때 만듭니다.
    /// </summary>
    /// <param name="directory">로그 폴더.</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="scrubber">개인정보 치환기.</param>
    /// <param name="retention">보관 기간.</param>
    /// <param name="maxTotalBytes">앱 로그 총량 상한(바이트).</param>
    public FileAppLogger(string directory, IClock clock, PersonalDataScrubber scrubber, TimeSpan retention, long maxTotalBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(scrubber);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retention, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxTotalBytes, 0);

        _directory = directory;
        _clock = clock;
        _scrubber = scrubber;
        _retention = retention;
        _maxTotalBytes = maxTotalBytes;
    }

    /// <summary>기본 로그 폴더(%LocalAppData%\PcOptimizer\logs).</summary>
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), APP_FOLDER, LOGS_FOLDER);

    /// <summary>
    /// 기본 폴더·보관 기간·상한으로 로거를 만듭니다.
    /// </summary>
    /// <returns>로거.</returns>
    public static FileAppLogger CreateDefault()
    {
        return new FileAppLogger(DefaultDirectory, SystemClock.Instance, PersonalDataScrubber.FromEnvironment(), DEFAULT_RETENTION, DEFAULT_MAX_TOTAL_BYTES);
    }

    /// <summary>
    /// 지정한 UTC 시각의 로그 파일 이름을 돌려줍니다.
    /// </summary>
    /// <param name="utc">UTC 시각.</param>
    /// <returns>파일 이름.</returns>
    public static string FileNameFor(DateTimeOffset utc)
    {
        return FILE_PREFIX + utc.UtcDateTime.ToString(DATE_FORMAT, CultureInfo.InvariantCulture) + FILE_EXTENSION;
    }

    /// <inheritdoc />
    public void Debug(string category, string message, Exception? ex = null) => Write(LEVEL_DEBUG, category, message, ex);

    /// <inheritdoc />
    public void Info(string category, string message, Exception? ex = null) => Write(LEVEL_INFO, category, message, ex);

    /// <inheritdoc />
    public void Warn(string category, string message, Exception? ex = null) => Write(LEVEL_WARN, category, message, ex);

    /// <inheritdoc />
    public void Error(string category, string message, Exception? ex = null) => Write(LEVEL_ERROR, category, message, ex);

    /// <summary>
    /// 한 줄을 기록한다. 날짜가 바뀌었으면 먼저 보관 정책을 적용한다.
    /// </summary>
    private void Write(string level, string category, string message, Exception? ex)
    {
        var now = _clock.UtcNow;
        var line = FormatLine(now, level, category, message, ex);
        var lineBytes = FILE_ENCODING.GetByteCount(line);

        lock (_sync)
        {
            try
            {
                var fileName = FileNameFor(now);
                if (!string.Equals(fileName, _currentFileName, StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(_directory);
                    _currentFileName = fileName;
                    ApplyRetention(now);
                }

                if (_totalBytes + lineBytes > _maxTotalBytes)
                {
                    PruneOldestUntilFits(lineBytes);
                    if (_totalBytes + lineBytes > _maxTotalBytes)
                    {
                        return;
                    }
                }

                File.AppendAllText(Path.Combine(_directory, fileName), line, FILE_ENCODING);
                _totalBytes += lineBytes;
            }
            catch (IOException)
            {
                // 로그 기록 실패로 앱 동작을 멈추지 않는다.
            }
            catch (UnauthorizedAccessException)
            {
                // 로그 폴더 권한 문제로 앱 동작을 멈추지 않는다.
            }
        }
    }

    /// <summary>
    /// 로그 한 줄을 만든다. 예외는 형식 이름만 붙인다.
    /// </summary>
    private string FormatLine(DateTimeOffset now, string level, string category, string message, Exception? ex)
    {
        var builder = new StringBuilder()
            .Append(now.UtcDateTime.ToString(TIMESTAMP_FORMAT, CultureInfo.InvariantCulture))
            .Append(' ').Append(level)
            .Append(' ').Append(category)
            .Append(": ").Append(_scrubber.Scrub(message ?? string.Empty));
        if (ex is not null)
        {
            builder.Append(" exception=").Append(ex.GetType().Name);
        }

        return builder.Append(Environment.NewLine).ToString();
    }

    /// <summary>
    /// 보관 기간이 지난 앱 로그를 지우고 현재 총량을 다시 계산한다.
    /// </summary>
    private void ApplyRetention(DateTimeOffset now)
    {
        var cutoff = FileNameFor(now - _retention);
        foreach (var file in ListAppLogs())
        {
            if (string.CompareOrdinal(file.Name, cutoff) < 0)
            {
                file.Delete();
            }
        }

        _totalBytes = ListAppLogs().Sum(file => file.Length);
    }

    /// <summary>
    /// 새 줄이 들어갈 때까지 오래된(현재 파일이 아닌) 앱 로그부터 지운다.
    /// </summary>
    private void PruneOldestUntilFits(long incomingBytes)
    {
        foreach (var file in ListAppLogs())
        {
            if (_totalBytes + incomingBytes <= _maxTotalBytes)
            {
                return;
            }

            if (string.Equals(file.Name, _currentFileName, StringComparison.Ordinal))
            {
                continue;
            }

            var length = file.Length;
            file.Delete();
            _totalBytes -= length;
        }
    }

    /// <summary>
    /// 로그 폴더의 앱 로그 파일을 이름(=날짜) 오름차순으로 나열한다. 이름 형식이 다른 파일은 건드리지 않는다.
    /// </summary>
    private List<FileInfo> ListAppLogs()
    {
        var directory = new DirectoryInfo(_directory);
        if (!directory.Exists)
        {
            return [];
        }

        return [.. directory.EnumerateFiles(FILE_PREFIX + "*" + FILE_EXTENSION)
            .Where(file => IsAppLogName(file.Name))
            .OrderBy(file => file.Name, StringComparer.Ordinal)];
    }

    /// <summary>
    /// 파일 이름이 이 로거가 만드는 형식(pcoptimizer-yyyyMMdd.log)인지 확인한다.
    /// </summary>
    private static bool IsAppLogName(string name)
    {
        if (!name.StartsWith(FILE_PREFIX, StringComparison.Ordinal) || !name.EndsWith(FILE_EXTENSION, StringComparison.Ordinal))
        {
            return false;
        }

        var datePart = name[FILE_PREFIX.Length..^FILE_EXTENSION.Length];
        return DateTime.TryParseExact(datePart, DATE_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
