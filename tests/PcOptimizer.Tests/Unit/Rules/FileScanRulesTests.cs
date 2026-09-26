/**
 * @file    : FileScanRulesTests.cs
 * @author  : rudals252
 * @brief   : 임시 위치 규칙(관측·부분·없음·접근 거부/관리자 권한 필요·미관측·해석 실패), 미분류 폴더 규칙(NoRule, 제목은 마지막 폴더 이름만, 전체 경로 비노출, 수정 시각 문구, 요약·부분 관측), 파일 스캔 요약 규칙(확보 가능량 아님, 건너뜀 개수, 정책 무효)을 가짜 측정값으로 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="TempLocationsRule"/>, <see cref="UnclassifiedFolderRule"/>, <see cref="FileScanSummaryRule"/>을 검증합니다.
/// </summary>
public sealed class FileScanRulesTests
{
    private const string PROFILE = @"C:\Users\tester";
    private const string CANDIDATE_PATH = PROFILE + @"\PrivateProjects\BigGames";
    private const long GB = 1_000_000_000;

    private static readonly DateTimeOffset NEWEST = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    /// <summary>
    /// 화면 문장(제목·근거·상세)을 모은다.
    /// </summary>
    private static string Texts(Finding finding)
    {
        return string.Join("\n", finding.Title, finding.Evidence, finding.Detail ?? string.Empty);
    }

    /// <summary>관측한 임시 위치는 Info이고 크기와 '확보 가능량 아님'·'논리 크기 추정, 중복 가능'을 알린다.</summary>
    [Fact]
    public void 관측한_임시_위치는_정보다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Temps(
            new FakeTemp(FileScanProbeContract.LOCATION_USER_TEMP, FileScanProbeContract.LOCATION_STATE_OBSERVED, false, 2_500_000_000, 1234)));

        var finding = Assert.Single(new TempLocationsRule().Evaluate(snapshot));

        Assert.Equal(TempLocationsRule.FINDING_ID_PREFIX + FileScanProbeContract.LOCATION_USER_TEMP, finding.Id);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.Storage, finding.Category);
        Assert.Contains("2.5 GB", finding.Title, StringComparison.Ordinal);
        Assert.Contains("확보 가능량이 아니", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("논리 크기 추정, 중복 가능", finding.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain(PROFILE, Texts(finding), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(finding.Measured, m => m.Name.EndsWith(FileScanProbeContract.FIELD_PATH, StringComparison.Ordinal));
    }

    /// <summary>부분 관측 임시 위치는 '일부만'을 제목에 밝히고 크기 측정값 품질이 Partial이다.</summary>
    [Fact]
    public void 부분_관측_임시_위치는_부분임을_밝힌다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Temps(
            new FakeTemp(FileScanProbeContract.LOCATION_USER_TEMP, FileScanProbeContract.LOCATION_STATE_PARTIAL, false, 300_000_000, 50, Partial: true)));

        var finding = Assert.Single(new TempLocationsRule().Evaluate(snapshot));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("일부만", finding.Title, StringComparison.Ordinal);
        Assert.Contains(finding.Measured, m => m.Quality == MeasurementQuality.Partial);
    }

    /// <summary>없는 위치는 Info '없음'이고 크기를 0으로 쓰지 않는다.</summary>
    [Fact]
    public void 없는_임시_위치는_없음이다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Temps(
            new FakeTemp(FileScanProbeContract.LOCATION_UPDATE_DOWNLOAD, FileScanProbeContract.LOCATION_STATE_ABSENT, true)));

        var finding = Assert.Single(new TempLocationsRule().Evaluate(snapshot));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("없음", finding.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("0 B", finding.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(finding.Measured, m => m.Name.EndsWith(FileScanProbeContract.FIELD_BYTES, StringComparison.Ordinal));
    }

    /// <summary>접근 거부: 일반 권한의 시스템 위치는 관리자 권한 필요, 관리자 권한이거나 사용자 위치면 접근 거부다.</summary>
    [Theory]
    [InlineData(true, false, CannotVerifyReason.ElevationRequired)]
    [InlineData(true, true, CannotVerifyReason.AccessDenied)]
    [InlineData(false, false, CannotVerifyReason.AccessDenied)]
    public void 접근_거부는_권한에_따라_사유가_다르다(bool system, bool elevated, CannotVerifyReason expected)
    {
        var snapshot = FileScanTestData.Snapshot(
            FileScanTestData.Temps(new FakeTemp(FileScanProbeContract.LOCATION_WINDOWS_TEMP, FileScanProbeContract.LOCATION_STATE_ACCESS_DENIED, system)),
            elevated: elevated);

        var finding = Assert.Single(new TempLocationsRule().Evaluate(snapshot));

        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(expected, finding.CannotVerifyReason);
    }

    /// <summary>미관측은 PartialData, 해석 실패는 Unsupported다.</summary>
    [Theory]
    [InlineData(FileScanProbeContract.LOCATION_STATE_NOT_OBSERVED, CannotVerifyReason.PartialData)]
    [InlineData(FileScanProbeContract.LOCATION_STATE_UNRESOLVED, CannotVerifyReason.Unsupported)]
    [InlineData(FileScanProbeContract.LOCATION_STATE_PROTECTED, CannotVerifyReason.Unsupported)]
    public void 관측하지_못한_위치는_확인_불가다(string state, CannotVerifyReason expected)
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Temps(new FakeTemp(FileScanProbeContract.LOCATION_DELIVERY_OPTIMIZATION, state, true)));

        var finding = Assert.Single(new TempLocationsRule().Evaluate(snapshot));

        Assert.Equal(expected, finding.CannotVerifyReason);
    }

    /// <summary>프로브가 실패·없음이면 임시 위치·미분류 규칙은 Finding을 만들지 않는다(상태 변환이 맡음).</summary>
    [Fact]
    public void 프로브가_실패하면_위치별_Finding이_없다()
    {
        var failed = FileScanTestData.Snapshot([], ProbeStatus.Failed);
        var empty = new ScanSnapshot(Guid.NewGuid(), []);

        Assert.Empty(new TempLocationsRule().Evaluate(failed));
        Assert.Empty(new TempLocationsRule().Evaluate(empty));
        Assert.Empty(new UnclassifiedFolderRule().Evaluate(failed));
        Assert.Empty(new UnclassifiedFolderRule().Evaluate(empty));
        Assert.Empty(new FileScanSummaryRule().Evaluate(empty));
    }

    /// <summary>
    /// 미분류 후보는 CannotVerify(NoRule)이며 제목에는 마지막 폴더 이름만, 근거·상세에는 전체 경로·상위 폴더 이름이 없고, 경로는 측정값에만 있다.
    /// 최근 시각은 '수정 시각'으로 표시하고 '사용 시각'이라 하지 않는다.
    /// </summary>
    [Fact]
    public void 미분류_후보는_규칙_없음이고_경로를_숨긴다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Unclassified(
            [new FakeCandidate(CANDIDATE_PATH, 3 * GB, 120, [".iso=2000000000", ".mp4=900000000", "=100000000"], NEWEST)]));

        var findings = new UnclassifiedFolderRule().Evaluate(snapshot);

        var candidate = Assert.Single(findings, f => f.Id.StartsWith(UnclassifiedFolderRule.FINDING_ID_PREFIX, StringComparison.Ordinal)
            && f.Id != UnclassifiedFolderRule.SUMMARY_FINDING_ID && f.Id != UnclassifiedFolderRule.PARTIAL_FINDING_ID);
        Assert.Equal(UnclassifiedFolderRule.FINDING_ID_PREFIX + CANDIDATE_PATH, candidate.Id);
        Assert.Equal(FindingCategory.Unclassified, candidate.Category);
        Assert.Equal(Verdict.CannotVerify, candidate.Verdict);
        Assert.Equal(CannotVerifyReason.NoRule, candidate.CannotVerifyReason);
        Assert.Contains("BigGames", candidate.Title, StringComparison.Ordinal);
        Assert.Contains("3.0 GB", candidate.Title, StringComparison.Ordinal);
        var texts = Texts(candidate);
        Assert.DoesNotContain(PROFILE, texts, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateProjects", texts, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\", texts, StringComparison.Ordinal);
        Assert.DoesNotContain("BigGames", candidate.Evidence + candidate.Detail, StringComparison.Ordinal);
        Assert.Contains("수정 시각", candidate.Detail, StringComparison.Ordinal);
        Assert.Contains("2026-03-04", candidate.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("최근 사용", texts, StringComparison.Ordinal);
        Assert.DoesNotContain("사용 시각:", texts, StringComparison.Ordinal);
        Assert.Contains(".iso", candidate.Detail, StringComparison.Ordinal);
        Assert.Contains("확장자 없음", candidate.Detail, StringComparison.Ordinal);
        Assert.Contains("확보 가능량이 아니", candidate.Evidence, StringComparison.Ordinal);
        Assert.DoesNotContain("확보 가능량:", texts, StringComparison.Ordinal);
        Assert.Contains(candidate.Measured, m => m.Value is TextValue { Value: CANDIDATE_PATH });
        Assert.Contains(candidate.Actions, action => action is ShowDetailsAction);
    }

    /// <summary>하위 후보를 뺀 부모 후보는 그 사실을 상세에 밝힌다.</summary>
    [Fact]
    public void 하위_후보를_뺀_부모는_상세에_밝힌다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Unclassified(
            [new FakeCandidate(PROFILE + @"\Parent", 2 * GB, 3, [".bin=2000000000"], NEWEST, ExcludesChildren: true)]));

        var candidate = new UnclassifiedFolderRule().Evaluate(snapshot).Single(f => f.CannotVerifyReason == CannotVerifyReason.NoRule);

        Assert.Contains("하위 폴더", candidate.Detail, StringComparison.Ordinal);
    }

    /// <summary>요약 Info는 후보 수와 선정 기준·상위 폴더 잔여 생략을 알리고, 후보가 없으면 '없음' 제목이다.</summary>
    [Fact]
    public void 미분류_요약은_기준과_생략_가능성을_알린다()
    {
        var some = FileScanTestData.Snapshot(FileScanTestData.Unclassified(
            [new FakeCandidate(CANDIDATE_PATH, 3 * GB, 1, [], null)], qualifying: 25));
        var none = FileScanTestData.Snapshot(FileScanTestData.Unclassified([]));

        var summary = new UnclassifiedFolderRule().Evaluate(some).Single(f => f.Id == UnclassifiedFolderRule.SUMMARY_FINDING_ID);
        var empty = new UnclassifiedFolderRule().Evaluate(none).Single(f => f.Id == UnclassifiedFolderRule.SUMMARY_FINDING_ID);

        Assert.Equal(Verdict.Info, summary.Verdict);
        Assert.Contains("25", summary.Title, StringComparison.Ordinal);
        Assert.Contains("1,000,000,000", summary.Evidence, StringComparison.Ordinal);
        Assert.Contains("생략", summary.Evidence, StringComparison.Ordinal);
        Assert.Equal(Verdict.Info, empty.Verdict);
        Assert.Contains("없어요", empty.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(new UnclassifiedFolderRule().Evaluate(none), f => f.CannotVerifyReason == CannotVerifyReason.NoRule);
    }

    /// <summary>부분 관측 대용량 폴더는 순위 밖 CannotVerify(PartialData) 하나로 알리고 전체 경로를 문장에 넣지 않는다.</summary>
    [Fact]
    public void 부분_관측_폴더는_순위_밖_확인_불가로_알린다()
    {
        var snapshot = FileScanTestData.Snapshot(FileScanTestData.Unclassified([], [(PROFILE + @"\AppData\Local", 30 * GB), (@"C:\ProgramData\Vendor", 2 * GB)]));

        var partial = new UnclassifiedFolderRule().Evaluate(snapshot).Single(f => f.Id == UnclassifiedFolderRule.PARTIAL_FINDING_ID);

        Assert.Equal(CannotVerifyReason.PartialData, partial.CannotVerifyReason);
        Assert.Contains("2", partial.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\", Texts(partial), StringComparison.Ordinal);
        Assert.Contains("Local", partial.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// 요약 Info: 관측 루트 수·소요 시간, '확보 가능량이 아님', 사유별 건너뜀 개수, 시간 초과 볼륨, 위치를 못 찾은 보호 항목 수, Google Drive 미감지를 알린다.
    /// </summary>
    [Fact]
    public void 파일_스캔_요약은_건너뜀과_한계를_알린다()
    {
        var measurements = new List<Measurement>
        {
            FileScanTestData.M(FileScanProbeContract.POLICY_STATE, new TextValue(FileScanProbeContract.POLICY_VALID)),
            FileScanTestData.M(FileScanProbeContract.PROTECTED_ROOT_COUNT, new IntegerValue(9)),
            FileScanTestData.M(FileScanProbeContract.ELAPSED_MS, new IntegerValue(12_345)),
            FileScanTestData.M(FileScanProbeContract.HARD_LINK_DUPLICATE_COUNT, new IntegerValue(2)),
            FileScanTestData.M(FileScanProbeContract.PROTECTED_UNRESOLVED_COUNT, new IntegerValue(3)),
            FileScanTestData.M(FileScanProbeContract.ROOT_COUNT, new IntegerValue(2)),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, 0, FileScanProbeContract.FIELD_STATE), new TextValue(FileScanProbeContract.ROOT_STATE_SCANNED)),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, 0, FileScanProbeContract.FIELD_SKIP_ACCESS_DENIED), new IntegerValue(4)),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, 0, FileScanProbeContract.FIELD_SKIP_REPARSE), new IntegerValue(7)),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, 0, FileScanProbeContract.FIELD_SKIP_PROTECTED), new IntegerValue(5)),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.ROOT_PREFIX, 1, FileScanProbeContract.FIELD_STATE), new TextValue(FileScanProbeContract.ROOT_STATE_NESTED)),
            FileScanTestData.M(FileScanProbeContract.VOLUME_COUNT, new IntegerValue(1)),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.VOLUME_PREFIX, 0, FileScanProbeContract.FIELD_VOLUME), new TextValue(@"C:\")),
            FileScanTestData.M(FileScanProbeContract.Name(FileScanProbeContract.VOLUME_PREFIX, 0, FileScanProbeContract.FIELD_TIMED_OUT), new BooleanValue(true)),
        };

        var finding = Assert.Single(new FileScanSummaryRule().Evaluate(FileScanTestData.Snapshot(measurements, ProbeStatus.Partial)));

        Assert.Equal(FileScanSummaryRule.SUMMARY_FINDING_ID, finding.Id);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains("1곳", finding.Title, StringComparison.Ordinal);
        Assert.Contains("12", finding.Title, StringComparison.Ordinal);
        Assert.Contains("확보 가능량이 아니", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("9곳", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("접근 거부 4", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("정션 등) 7", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("보호 제외 5", finding.Detail, StringComparison.Ordinal);
        Assert.Contains(@"C:\", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("Google Drive", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("하드링크 2", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("3개는 이 PC에서 위치를 찾지 못했어요", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("기본 위치를 보호", finding.Detail, StringComparison.Ordinal);
    }

    /// <summary>보호 정책이 무효이면 요약 규칙은 파일 검사를 하지 않았다는 CannotVerify(ProbeError)와 리소스 상세를 낸다.</summary>
    [Fact]
    public void 정책이_무효이면_검사하지_않았음을_알린다()
    {
        var snapshot = FileScanTestData.Snapshot(
            [
                FileScanTestData.M(FileScanProbeContract.POLICY_STATE, new TextValue(FileScanProbeContract.POLICY_INVALID)),
                FileScanTestData.M(FileScanProbeContract.POLICY_ERROR, new TextValue("UnknownKind")),
            ],
            ProbeStatus.Failed,
            issues: [new Issue(CannotVerifyReason.ProbeError, "정책 오류")]);

        var finding = Assert.Single(new FileScanSummaryRule().Evaluate(snapshot));

        Assert.Equal(FileScanSummaryRule.POLICY_FINDING_ID, finding.Id);
        Assert.Equal(CannotVerifyReason.ProbeError, finding.CannotVerifyReason);
        Assert.Contains("UnknownKind", finding.Detail, StringComparison.Ordinal);
        Assert.Contains("protect.json", finding.Detail, StringComparison.Ordinal);
    }
}
