/**
 * @file    : FileScanMeasurements.cs
 * @author  : rudals252
 * @brief   : 공유 파일 스캔 결과와 미분류 선정 결과를 FileScanProbeContract 측정값(보호 루트·루트별 합계와 건너뜀·볼륨·임시 위치·미분류 후보·부분 관측 폴더)으로 바꾸는 도우미
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 파일 스캔 측정값 생성 도우미입니다. 관측하지 못한 크기는 측정값을 만들지 않으며(0바이트로 바꾸지 않음), 경로는 path 필드에만 둡니다.
/// 보호 루트는 개인 경로일 수 있어 경로 대신 출처 이름만 기록합니다.
/// 크기 품질: 부분 집계는 Partial, 하드링크 중복 가능은 Estimated, 그 밖은 Observed.
/// </summary>
internal static class FileScanMeasurements
{
    private const string SOURCE_SCAN = "FileSystemEnumerator (metadata only)";
    private const string SOURCE_POLICY = @"rules\protect.json";
    private const string SOURCE_SELECTOR = "UnclassifiedFolderSelector";
    private const string ISO_8601_FORMAT = "o";
    private const char ENVIRONMENT_MARK = '%';

    /// <summary>
    /// 정책 무효 결과의 측정값을 만든다.
    /// </summary>
    public static List<Measurement> ForInvalidPolicy(ProtectionPolicyError error, DateTimeOffset observedAt)
    {
        return
        [
            new(FileScanProbeContract.POLICY_STATE, new TextValue(FileScanProbeContract.POLICY_INVALID), null, SOURCE_POLICY, observedAt, MeasurementQuality.Observed),
            new(FileScanProbeContract.POLICY_ERROR, new TextValue(error.ToString()), null, SOURCE_POLICY, observedAt, MeasurementQuality.Observed),
        ];
    }

    /// <summary>
    /// 정상 순회 결과의 측정값을 만든다.
    /// </summary>
    public static List<Measurement> Build(DirectoryScanResult result, UnclassifiedSelection selection, DateTimeOffset observedAt)
    {
        var protection = result.Protection!;
        var plan = result.Plan!;
        var traversal = result.Traversal!;
        var list = new List<Measurement>();
        void Add(string name, MeasurementValue value, string? unit = null, MeasurementQuality quality = MeasurementQuality.Observed, string source = SOURCE_SCAN)
            => list.Add(new Measurement(name, value, unit, source, observedAt, quality));

        Add(FileScanProbeContract.POLICY_STATE, new TextValue(FileScanProbeContract.POLICY_VALID), source: SOURCE_POLICY);
        Add(FileScanProbeContract.PROTECTED_ROOT_COUNT, new IntegerValue(protection.Roots.Count), source: SOURCE_POLICY);
        Add(FileScanProbeContract.PROTECTED_ROOT_LABELS, new TextListValue([.. protection.Roots.Select(ProtectionLabel).Distinct(StringComparer.Ordinal)]), source: SOURCE_POLICY);
        Add(FileScanProbeContract.PROTECTED_UNRESOLVED_COUNT, new IntegerValue(protection.UnresolvedCount), source: SOURCE_POLICY);
        Add(FileScanProbeContract.SYNC_ROOT_COUNT, new IntegerValue(protection.Roots.Count(root => root.Origin == ProtectedRootOrigin.CloudSync)), source: SOURCE_POLICY);
        Add(FileScanProbeContract.ELAPSED_MS, new IntegerValue((long)result.Elapsed.TotalMilliseconds), FileScanProbeContract.UNIT_MILLISECONDS);
        Add(FileScanProbeContract.HARD_LINK_DUPLICATE_COUNT, new IntegerValue(traversal.HardLinkDuplicateCount));
        Add(FileScanProbeContract.HARD_LINK_DUPLICATE_BYTES, new IntegerValue(traversal.HardLinkDuplicateBytes), FileScanProbeContract.UNIT_BYTES);

        AddRoots(plan, traversal, list, observedAt);
        AddVolumes(traversal, list, observedAt);
        AddTemps(result.Locations, list, observedAt);
        AddUnclassified(selection, list, observedAt);
        return list;
    }

    /// <summary>
    /// 보호 루트의 출처 이름을 만든다(경로가 아님). 시스템 경로는 템플릿의 마지막 이름에서 % 표시를 뺀 값(예: WinSxS, ProgramFiles(x86)).
    /// </summary>
    private static string ProtectionLabel(ProtectedRoot root)
    {
        return root.Origin switch
        {
            ProtectedRootOrigin.KnownFolder => FileScanProbeContract.PROTECTED_LABEL_KNOWN_FOLDER + root.Label,
            ProtectedRootOrigin.CloudSync => FileScanProbeContract.PROTECTED_LABEL_CLOUD + root.Label,
            _ => FileScanProbeContract.PROTECTED_LABEL_SYSTEM + PathScope.GetLeafName(root.Label).Trim(ENVIRONMENT_MARK),
        };
    }

    /// <summary>
    /// 크기 품질을 정한다.
    /// </summary>
    private static MeasurementQuality SizeQuality(bool partial, bool duplicatesPossible)
    {
        if (partial)
        {
            return MeasurementQuality.Partial;
        }

        return duplicatesPossible ? MeasurementQuality.Estimated : MeasurementQuality.Observed;
    }

    /// <summary>
    /// 루트별 상태·합계·건너뜀.
    /// </summary>
    private static void AddRoots(ScanPlan plan, TraversalResult traversal, List<Measurement> list, DateTimeOffset observedAt)
    {
        list.Add(new Measurement(FileScanProbeContract.ROOT_COUNT, new IntegerValue(plan.Roots.Count), null, SOURCE_SCAN, observedAt, MeasurementQuality.Observed));
        for (var index = 0; index < plan.Roots.Count; index++)
        {
            var root = plan.Roots[index];
            var traversed = traversal.Roots.FirstOrDefault(item => item.Id == root.Id);
            void Add(string field, MeasurementValue value, string? unit = null, MeasurementQuality quality = MeasurementQuality.Observed)
                => list.Add(new Measurement(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, index, field), value, unit, SOURCE_SCAN, observedAt, quality));

            Add(FileScanProbeContract.FIELD_ID, new TextValue(root.Id));
            if (root.Path is { } path)
            {
                Add(FileScanProbeContract.FIELD_PATH, new TextValue(path));
            }

            Add(FileScanProbeContract.FIELD_STATE, new TextValue(RootState(root, traversed)));
            if (root.NestedIn is { } outer)
            {
                Add(FileScanProbeContract.FIELD_NESTED_IN, new TextValue(outer));
            }

            if (traversed is null)
            {
                continue;
            }

            Add(FileScanProbeContract.FIELD_VOLUME, new TextValue(traversed.VolumeRoot));
            if (traversed.Totals is not { } totals)
            {
                continue;
            }

            Add(FileScanProbeContract.FIELD_BYTES, new IntegerValue(totals.Bytes), FileScanProbeContract.UNIT_BYTES, SizeQuality(totals.IsPartial, totals.DuplicatesPossible));
            Add(FileScanProbeContract.FIELD_FILE_COUNT, new IntegerValue(totals.FileCount), quality: totals.IsPartial ? MeasurementQuality.Partial : MeasurementQuality.Observed);
            Add(FileScanProbeContract.FIELD_DIRECTORY_COUNT, new IntegerValue(totals.DirectoryCount), quality: totals.IsPartial ? MeasurementQuality.Partial : MeasurementQuality.Observed);
            Add(FileScanProbeContract.FIELD_PARTIAL, new BooleanValue(totals.IsPartial));
            Add(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE, new BooleanValue(totals.DuplicatesPossible));
            Add(FileScanProbeContract.FIELD_COMPRESSED_SPARSE_COUNT, new IntegerValue(totals.CompressedOrSparseFileCount));
            Add(FileScanProbeContract.FIELD_COMPRESSED_SPARSE_LOGICAL, new IntegerValue(totals.CompressedOrSparseLogicalBytes), FileScanProbeContract.UNIT_BYTES);
            Add(FileScanProbeContract.FIELD_COMPRESSED_SPARSE_ALLOCATED, new IntegerValue(totals.CompressedOrSparseAllocatedBytes), FileScanProbeContract.UNIT_BYTES);
            Add(FileScanProbeContract.FIELD_SKIP_PROTECTED, new IntegerValue(totals.Skips.ProtectedExcluded));
            Add(FileScanProbeContract.FIELD_SKIP_ACCESS_DENIED, new IntegerValue(totals.Skips.AccessDenied));
            Add(FileScanProbeContract.FIELD_SKIP_IN_USE, new IntegerValue(totals.Skips.InUse));
            Add(FileScanProbeContract.FIELD_SKIP_REPARSE, new IntegerValue(totals.Skips.Reparse));
            Add(FileScanProbeContract.FIELD_SKIP_PLACEHOLDER, new IntegerValue(totals.Skips.Placeholder));
            Add(FileScanProbeContract.FIELD_SKIP_TIMEOUT, new IntegerValue(totals.Skips.Timeout));
        }
    }

    /// <summary>
    /// 계획 상태와 순회 상태로 루트 상태 코드를 정한다.
    /// </summary>
    private static string RootState(ScanRootPlan plan, RootTraversal? traversed)
    {
        return plan.State switch
        {
            ScanRootPlanState.Unresolved => FileScanProbeContract.ROOT_STATE_UNRESOLVED,
            ScanRootPlanState.Rejected => FileScanProbeContract.ROOT_STATE_REJECTED,
            ScanRootPlanState.Nested => FileScanProbeContract.ROOT_STATE_NESTED,
            ScanRootPlanState.Protected => FileScanProbeContract.ROOT_STATE_PROTECTED,
            _ => traversed?.State switch
            {
                RootScanState.Scanned => FileScanProbeContract.ROOT_STATE_SCANNED,
                RootScanState.Absent => FileScanProbeContract.ROOT_STATE_ABSENT,
                RootScanState.AccessDenied => FileScanProbeContract.ROOT_STATE_ACCESS_DENIED,
                RootScanState.ReparsePoint => FileScanProbeContract.ROOT_STATE_REPARSE,
                RootScanState.Protected => FileScanProbeContract.ROOT_STATE_PROTECTED,
                RootScanState.TimedOut => FileScanProbeContract.ROOT_STATE_TIMED_OUT,
                _ => FileScanProbeContract.ROOT_STATE_ERROR,
            },
        };
    }

    /// <summary>
    /// 볼륨별 소요 시간·시간 초과.
    /// </summary>
    private static void AddVolumes(TraversalResult traversal, List<Measurement> list, DateTimeOffset observedAt)
    {
        list.Add(new Measurement(FileScanProbeContract.VOLUME_COUNT, new IntegerValue(traversal.Volumes.Count), null, SOURCE_SCAN, observedAt, MeasurementQuality.Observed));
        for (var index = 0; index < traversal.Volumes.Count; index++)
        {
            var volume = traversal.Volumes[index];
            void Add(string field, MeasurementValue value, string? unit = null)
                => list.Add(new Measurement(FileScanProbeContract.Name(FileScanProbeContract.VOLUME_PREFIX, index, field), value, unit, SOURCE_SCAN, observedAt, MeasurementQuality.Observed));

            Add(FileScanProbeContract.FIELD_VOLUME, new TextValue(volume.VolumeRoot));
            Add(FileScanProbeContract.FIELD_ELAPSED_MS, new IntegerValue((long)volume.Elapsed.TotalMilliseconds), FileScanProbeContract.UNIT_MILLISECONDS);
            Add(FileScanProbeContract.FIELD_TIMED_OUT, new BooleanValue(volume.TimedOut));
        }
    }

    /// <summary>
    /// 표준 임시 위치별 상태·크기(관측했을 때만).
    /// </summary>
    private static void AddTemps(IReadOnlyList<LocationObservation> locations, List<Measurement> list, DateTimeOffset observedAt)
    {
        list.Add(new Measurement(FileScanProbeContract.TEMP_COUNT, new IntegerValue(locations.Count), null, SOURCE_SCAN, observedAt, MeasurementQuality.Observed));
        for (var index = 0; index < locations.Count; index++)
        {
            var location = locations[index];
            var partial = location.State == LocationState.Partial;
            void Add(string field, MeasurementValue value, string? unit = null, MeasurementQuality quality = MeasurementQuality.Observed)
                => list.Add(new Measurement(FileScanProbeContract.Name(FileScanProbeContract.TEMP_PREFIX, index, field), value, unit, SOURCE_SCAN, observedAt, quality));

            Add(FileScanProbeContract.FIELD_ID, new TextValue(location.Id));
            if (location.Path is { } path)
            {
                Add(FileScanProbeContract.FIELD_PATH, new TextValue(path));
            }

            Add(FileScanProbeContract.FIELD_STATE, new TextValue(LocationStateCode(location.State)));
            Add(FileScanProbeContract.FIELD_SYSTEM, new BooleanValue(location.IsSystem));
            if (location.Bytes is { } bytes && location.FileCount is { } files)
            {
                Add(FileScanProbeContract.FIELD_BYTES, new IntegerValue(bytes), FileScanProbeContract.UNIT_BYTES, SizeQuality(partial, location.DuplicatesPossible));
                Add(FileScanProbeContract.FIELD_FILE_COUNT, new IntegerValue(files), quality: partial ? MeasurementQuality.Partial : MeasurementQuality.Observed);
                Add(FileScanProbeContract.FIELD_PARTIAL, new BooleanValue(partial));
                Add(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE, new BooleanValue(location.DuplicatesPossible));
            }
        }
    }

    /// <summary>
    /// 임시 위치 상태 코드.
    /// </summary>
    private static string LocationStateCode(LocationState state)
    {
        return state switch
        {
            LocationState.Observed => FileScanProbeContract.LOCATION_STATE_OBSERVED,
            LocationState.Partial => FileScanProbeContract.LOCATION_STATE_PARTIAL,
            LocationState.Absent => FileScanProbeContract.LOCATION_STATE_ABSENT,
            LocationState.AccessDenied => FileScanProbeContract.LOCATION_STATE_ACCESS_DENIED,
            LocationState.Unresolved => FileScanProbeContract.LOCATION_STATE_UNRESOLVED,
            LocationState.Protected => FileScanProbeContract.LOCATION_STATE_PROTECTED,
            _ => FileScanProbeContract.LOCATION_STATE_NOT_OBSERVED,
        };
    }

    /// <summary>
    /// 미분류 후보와 부분 관측 대용량 폴더.
    /// </summary>
    private static void AddUnclassified(UnclassifiedSelection selection, List<Measurement> list, DateTimeOffset observedAt)
    {
        void AddTop(string name, MeasurementValue value, string? unit = null)
            => list.Add(new Measurement(name, value, unit, SOURCE_SELECTOR, observedAt, MeasurementQuality.Observed));

        AddTop(FileScanProbeContract.UNCLASSIFIED_MIN_BYTES, new IntegerValue(UnclassifiedFolderSelector.UNCLASSIFIED_MIN_BYTES), FileScanProbeContract.UNIT_BYTES);
        AddTop(FileScanProbeContract.UNCLASSIFIED_COUNT, new IntegerValue(selection.Candidates.Count));
        AddTop(FileScanProbeContract.UNCLASSIFIED_QUALIFYING_COUNT, new IntegerValue(selection.QualifyingCount));
        for (var index = 0; index < selection.Candidates.Count; index++)
        {
            var candidate = selection.Candidates[index];
            void Add(string field, MeasurementValue value, string? unit = null, MeasurementQuality quality = MeasurementQuality.Observed)
                => list.Add(new Measurement(FileScanProbeContract.Name(FileScanProbeContract.UNCLASSIFIED_PREFIX, index, field), value, unit, SOURCE_SELECTOR, observedAt, quality));

            Add(FileScanProbeContract.FIELD_PATH, new TextValue(candidate.Path));
            Add(FileScanProbeContract.FIELD_BYTES, new IntegerValue(candidate.Bytes), FileScanProbeContract.UNIT_BYTES, SizeQuality(false, candidate.DuplicatesPossible));
            Add(FileScanProbeContract.FIELD_FILE_COUNT, new IntegerValue(candidate.FileCount));
            Add(FileScanProbeContract.FIELD_TOP_EXTENSIONS, new TextListValue([.. candidate.TopExtensions.Select(share =>
                share.Extension + FileScanProbeContract.EXTENSION_SEPARATOR + share.Bytes.ToString(CultureInfo.InvariantCulture))]));
            if (candidate.NewestWriteUtc is { } newest)
            {
                Add(FileScanProbeContract.FIELD_NEWEST_WRITE_UTC, new TextValue(newest.ToUniversalTime().ToString(ISO_8601_FORMAT, CultureInfo.InvariantCulture)));
            }

            Add(FileScanProbeContract.FIELD_DUPLICATES_POSSIBLE, new BooleanValue(candidate.DuplicatesPossible));
            Add(FileScanProbeContract.FIELD_EXCLUDES_REPORTED_CHILDREN, new BooleanValue(candidate.ExcludesReportedDescendants));
        }

        AddTop(FileScanProbeContract.PARTIAL_FOLDER_COUNT, new IntegerValue(selection.PartialFolders.Count));
        AddTop(FileScanProbeContract.PARTIAL_FOLDER_TOTAL_COUNT, new IntegerValue(selection.PartialCount));
        for (var index = 0; index < selection.PartialFolders.Count; index++)
        {
            var folder = selection.PartialFolders[index];
            list.Add(new Measurement(
                FileScanProbeContract.Name(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, index, FileScanProbeContract.FIELD_PATH),
                new TextValue(folder.Path), null, SOURCE_SELECTOR, observedAt, MeasurementQuality.Observed));
            list.Add(new Measurement(
                FileScanProbeContract.Name(FileScanProbeContract.PARTIAL_FOLDER_PREFIX, index, FileScanProbeContract.FIELD_BYTES),
                new IntegerValue(folder.ObservedBytes), FileScanProbeContract.UNIT_BYTES, SOURCE_SELECTOR, observedAt, MeasurementQuality.Partial));
        }
    }
}
