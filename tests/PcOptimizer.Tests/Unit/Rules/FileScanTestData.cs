/**
 * @file    : FileScanTestData.cs
 * @author  : rudals252
 * @brief   : 파일 스캔 규칙 단위 테스트용 가짜 측정값(임시 위치·미분류 후보·부분 관측 폴더·루트 합계) 생성 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 임시 위치 하나입니다. 크기가 null이면 크기 측정값이 없습니다.
/// </summary>
internal sealed record FakeTemp(string Id, string State, bool System, long? Bytes = null, long? Files = null, bool Partial = false, bool Duplicates = true, string? Path = null);

/// <summary>
/// 가짜 미분류 후보 하나입니다.
/// </summary>
internal sealed record FakeCandidate(string Path, long Bytes, long Files, string[] Extensions, DateTimeOffset? Newest, bool Duplicates = true, bool ExcludesChildren = false);

/// <summary>
/// 파일 스캔 규칙 테스트 데이터 도우미입니다. 큰 크기는 메타데이터 값일 뿐 실제 파일이 아닙니다.
/// </summary>
internal static class FileScanTestData
{
    /// <summary>테스트용 관측 시각.</summary>
    public static readonly DateTimeOffset OBSERVED_AT = new(2026, 9, 26, 1, 2, 3, TimeSpan.Zero);

    /// <summary>
    /// 측정값 하나를 만든다.
    /// </summary>
    public static Measurement M(string name, MeasurementValue value, MeasurementQuality quality = MeasurementQuality.Observed)
    {
        return new Measurement(name, value, null, "fake", OBSERVED_AT, quality);
    }

    /// <summary>
    /// 임시 위치 측정값을 만든다.
    /// </summary>
    public static List<Measurement> Temps(params FakeTemp[] temps)
    {
        var list = new List<Measurement> { M(FileScanProbeContract.TEMP_COUNT, new IntegerValue(temps.Length)) };
        for (var index = 0; index < temps.Length; index++)
        {
            var temp = temps[index];
            string Name(string field) => FileScanProbeContract.Name(FileScanProbeContract.TEMP_PREFIX, index, field);
            list.Add(M(Name(FileScanProbeContract.FIELD_ID), new TextValue(temp.Id)));
            list.Add(M(Name(FileScanProbeContract.FIELD_STATE), new TextValue(temp.State)));
            list.Add(M(Name(FileScanProbeContract.FIELD_SYSTEM), new BooleanValue(temp.System)));
            list.Add(M(Name(FileScanProbeContract.FIELD_PATH), new TextValue(temp.Path ?? @"C:\Users\tester\AppData\Local\Temp")));
            if (temp.Bytes is { } bytes)
            {
                list.Add(M(Name(FileScanProbeContract.FIELD_BYTES), new IntegerValue(bytes), temp.Partial ? MeasurementQuality.Partial : MeasurementQuality.Estimated));
                list.Add(M(Name(FileScanProbeContract.FIELD_FILE_COUNT), new IntegerValue(temp.Files ?? 0)));
                list.Add(M(Name(FileScanProbeContract.FIELD_PARTIAL), new BooleanValue(temp.Partial)));
                list.Add(M(Name(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE), new BooleanValue(temp.Duplicates)));
            }
        }

        return list;
    }

    /// <summary>
    /// 미분류 후보·부분 관측 폴더 측정값을 만든다.
    /// </summary>
    public static List<Measurement> Unclassified(FakeCandidate[] candidates, (string Path, long Bytes)[]? partials = null, int? qualifying = null)
    {
        partials ??= [];
        var list = new List<Measurement>
        {
            M(FileScanProbeContract.UNCLASSIFIED_MIN_BYTES, new IntegerValue(1_000_000_000)),
            M(FileScanProbeContract.UNCLASSIFIED_COUNT, new IntegerValue(candidates.Length)),
            M(FileScanProbeContract.UNCLASSIFIED_QUALIFYING_COUNT, new IntegerValue(qualifying ?? candidates.Length)),
            M(FileScanProbeContract.PARTIAL_FOLDER_COUNT, new IntegerValue(partials.Length)),
            M(FileScanProbeContract.PARTIAL_FOLDER_TOTAL_COUNT, new IntegerValue(partials.Length)),
        };
        for (var index = 0; index < candidates.Length; index++)
        {
            var candidate = candidates[index];
            string Name(string field) => FileScanProbeContract.Name(FileScanProbeContract.UNCLASSIFIED_PREFIX, index, field);
            list.Add(M(Name(FileScanProbeContract.FIELD_PATH), new TextValue(candidate.Path)));
            list.Add(M(Name(FileScanProbeContract.FIELD_BYTES), new IntegerValue(candidate.Bytes), MeasurementQuality.Estimated));
            list.Add(M(Name(FileScanProbeContract.FIELD_FILE_COUNT), new IntegerValue(candidate.Files)));
            list.Add(M(Name(FileScanProbeContract.FIELD_TOP_EXTENSIONS), new TextListValue(candidate.Extensions)));
            if (candidate.Newest is { } newest)
            {
                list.Add(M(Name(FileScanProbeContract.FIELD_NEWEST_WRITE_UTC), new TextValue(newest.ToString("o", CultureInfo.InvariantCulture))));
            }

            list.Add(M(Name(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE), new BooleanValue(candidate.Duplicates)));
            list.Add(M(Name(FileScanProbeContract.FIELD_EXCLUDES_REPORTED_CHILDREN), new BooleanValue(candidate.ExcludesChildren)));
        }

        for (var index = 0; index < partials.Length; index++)
        {
            list.Add(M(FileScanProbeContract.Name(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, index, FileScanProbeContract.FIELD_PATH), new TextValue(partials[index].Path)));
            list.Add(M(FileScanProbeContract.Name(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, index, FileScanProbeContract.FIELD_BYTES), new IntegerValue(partials[index].Bytes), MeasurementQuality.Partial));
        }

        return list;
    }

    /// <summary>
    /// 파일 스캔 프로브 결과 스냅샷을 만든다.
    /// </summary>
    public static ScanSnapshot Snapshot(IEnumerable<Measurement> measurements, ProbeStatus status = ProbeStatus.Success, bool elevated = false, IReadOnlyList<Issue>? issues = null)
    {
        var result = new ProbeResult(
            FileScanProbeContract.PROBE_ID,
            status,
            [.. measurements],
            issues ?? [],
            OBSERVED_AT,
            TimeSpan.FromSeconds(3),
            new UserContext("anon", elevated));
        return new ScanSnapshot(Guid.NewGuid(), [result]);
    }
}
