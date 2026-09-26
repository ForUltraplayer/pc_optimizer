/**
 * @file    : StartupItemsRuleTests.cs
 * @author  : rudals252
 * @brief   : 시작 프로그램 규칙의 항목별 활성/비활성/알 수 없음 정보, 개수 기반 후보 없음, 부팅 영향 Unsupported, 수집 범위 명시, 제목·근거·상세의 전체 경로 부재, 부분 수집·실패 처리 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 시작 항목 하나입니다. Lookup·Kind·FirstByte는 StartupApproved 원시 측정값입니다(null이면 측정값 없음).
/// </summary>
public sealed record FakeStartupItem(
    string Name,
    string Source = StartupItemsProbeContract.SOURCE_HKCU_RUN,
    string? Lookup = StartupItemsProbeContract.LOOKUP_FOUND,
    string? Kind = StartupItemsProbeContract.KIND_BINARY,
    long? FirstByte = StartupItemsProbeContract.APPROVED_ENABLED,
    string? Command = null);

/// <summary>
/// <see cref="StartupItemsRule"/>을 검증합니다.
/// </summary>
public sealed class StartupItemsRuleTests
{
    private const string PERSONAL_PROFILE = @"C:\Users\Kimtester";
    private const string PERSONAL_COMMAND = "\"" + PERSONAL_PROFILE + @"\AppData\Local\Chat\chat.exe"" --minimized";
    private const int MANY_ITEMS = 40;

    private static readonly StartupItemsRule RULE = new();

    /// <summary>
    /// 항목으로 스냅샷을 만든다.
    /// </summary>
    private static ScanSnapshot Snapshot(IReadOnlyList<FakeStartupItem> items, ProbeStatus status = ProbeStatus.Success, params string[] unreadableSources)
    {
        var measurements = new List<Measurement>
        {
            RuleTestData.Integer(StartupItemsProbeContract.ITEM_COUNT, items.Count),
            RuleTestData.TextList(StartupItemsProbeContract.UNREADABLE_SOURCES, unreadableSources),
        };

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            string Name(string field) => StartupItemsProbeContract.ItemMeasurementName(index, field);
            measurements.Add(RuleTestData.Text(Name(StartupItemsProbeContract.FIELD_NAME), item.Name));
            measurements.Add(RuleTestData.Text(Name(StartupItemsProbeContract.FIELD_SOURCE), item.Source));
            measurements.Add(RuleTestData.Text(Name(StartupItemsProbeContract.FIELD_COMMAND), item.Command ?? PERSONAL_COMMAND));
            if (item.Lookup is not null)
            {
                measurements.Add(RuleTestData.Text(Name(StartupItemsProbeContract.FIELD_APPROVED_LOOKUP), item.Lookup));
            }

            if (item.Kind is not null)
            {
                measurements.Add(RuleTestData.Text(Name(StartupItemsProbeContract.FIELD_APPROVED_KIND), item.Kind));
            }

            if (item.FirstByte is { } firstByte)
            {
                measurements.Add(RuleTestData.Integer(Name(StartupItemsProbeContract.FIELD_APPROVED_FIRST_BYTE), firstByte));
            }
        }

        return HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(StartupItemsProbeContract.PROBE_ID, status, measurements));
    }

    /// <summary>
    /// 항목 Finding만 고른다.
    /// </summary>
    private static Finding[] Items(IReadOnlyList<Finding> findings)
    {
        return [.. findings.Where(f => f.Id.StartsWith(StartupItemsRule.FINDING_ID_PREFIX, StringComparison.Ordinal))];
    }

    /// <summary>StartupApproved 첫 바이트 0x02는 활성, 0x03은 비활성, 그 밖의 값·형식·조회 결과는 알 수 없음이다.</summary>
    [Theory]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0x02L, StartupApprovedState.Enabled)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0x03L, StartupApprovedState.Disabled)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0x00L, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0x06L, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0x07L, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0xFFL, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, null, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, "DWord", 0x02L, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, "String", 0x03L, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, null, 0x02L, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_MISSING, null, null, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_NOT_TRACKED, null, null, StartupApprovedState.Unknown)]
    [InlineData(StartupItemsProbeContract.LOOKUP_UNREADABLE, null, null, StartupApprovedState.Unknown)]
    [InlineData(null, StartupItemsProbeContract.KIND_BINARY, 0x02L, StartupApprovedState.Unknown)]
    public void StartupApproved_알려진_값만_해석한다(string? lookup, string? kind, long? firstByte, StartupApprovedState expected)
    {
        Assert.Equal(expected, StartupApprovedClassifier.Classify(lookup, kind, firstByte));
    }

    /// <summary>항목마다 정보 하나이며 제목에 활성/비활성/알 수 없음을 표시하고 시작 앱 설정 열기를 제공한다.</summary>
    [Fact]
    public void 항목마다_상태를_담은_정보를_낸다()
    {
        var findings = RULE.Evaluate(Snapshot(
        [
            new FakeStartupItem("Chat"),
            new FakeStartupItem("Updater", FirstByte: StartupItemsProbeContract.APPROVED_DISABLED),
            new FakeStartupItem("Cleanup", StartupItemsProbeContract.SOURCE_HKCU_RUN_ONCE, StartupItemsProbeContract.LOOKUP_NOT_TRACKED, null, null),
        ]));

        var items = Items(findings);
        Assert.Equal(3, items.Length);
        Assert.All(items, finding =>
        {
            Assert.Equal(Verdict.Info, finding.Verdict);
            Assert.Equal(FindingCategory.Startup, finding.Category);
            Assert.Null(finding.Recommendation);
            Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: StartupItemsRule.STARTUP_SETTINGS_URI });
        });
        Assert.Contains("Chat", items[0].Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Startup_State_Enabled, items[0].Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Startup_State_Disabled, items[1].Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Startup_State_Unknown, items[2].Title, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Startup_Evidence_NotTracked, items[2].Evidence, StringComparison.Ordinal);
    }

    /// <summary>StartupApproved 값이 없거나 해석할 수 없으면 활성으로 추정하지 않고 알 수 없음으로 표시한다.</summary>
    [Theory]
    [InlineData(StartupItemsProbeContract.LOOKUP_MISSING, null, null)]
    [InlineData(StartupItemsProbeContract.LOOKUP_UNREADABLE, null, null)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, "DWord", null)]
    [InlineData(StartupItemsProbeContract.LOOKUP_FOUND, StartupItemsProbeContract.KIND_BINARY, 0x06L)]
    public void 해석할_수_없는_상태는_알_수_없음이다(string lookup, string? kind, long? firstByte)
    {
        var finding = Assert.Single(Items(RULE.Evaluate(Snapshot([new FakeStartupItem("Tool", Lookup: lookup, Kind: kind, FirstByte: firstByte)]))));

        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains(CoreStrings.Startup_State_Unknown, finding.Title, StringComparison.Ordinal);
        Assert.DoesNotContain(CoreStrings.Startup_State_Enabled, finding.Title, StringComparison.Ordinal);
    }

    /// <summary>항목이 많아도 개수로 후보를 만들지 않는다(모든 Finding이 Candidate가 아님).</summary>
    [Fact]
    public void 항목_수로_후보를_만들지_않는다()
    {
        FakeStartupItem[] many = [.. Enumerable.Range(0, MANY_ITEMS).Select(i => new FakeStartupItem($"App{i}"))];

        var findings = RULE.Evaluate(Snapshot(many));

        Assert.Equal(MANY_ITEMS, Items(findings).Length);
        Assert.DoesNotContain(findings, f => f.Verdict == Verdict.Candidate);
        Assert.All(findings, f => Assert.Null(f.Recommendation));
    }

    /// <summary>부팅 영향은 CannotVerify(Unsupported) 하나로만 표시하고 정상/문제로 추정하지 않는다.</summary>
    [Fact]
    public void 부팅_영향은_지원_불가_하나다()
    {
        var findings = RULE.Evaluate(Snapshot([new FakeStartupItem("Chat")]));

        var bootImpact = Assert.Single(findings, f => f.Id == StartupItemsRule.BOOT_IMPACT_FINDING_ID);
        Assert.Equal(Verdict.CannotVerify, bootImpact.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, bootImpact.CannotVerifyReason);
        Assert.Equal(FindingCategory.Startup, bootImpact.Category);
        Assert.Equal(CoreStrings.Startup_Title_BootImpact, bootImpact.Title);
        Assert.Empty(bootImpact.Measured);
    }

    /// <summary>요약 정보는 항목 수와 수집 범위(예약 작업·서비스·패키지 앱 자동 실행 미포함)를 명시한다.</summary>
    [Fact]
    public void 요약은_개수와_수집_범위를_명시한다()
    {
        var findings = RULE.Evaluate(Snapshot([new FakeStartupItem("Chat"), new FakeStartupItem("Sync", StartupItemsProbeContract.SOURCE_HKLM32_RUN)]));

        var summary = Assert.Single(findings, f => f.Id == StartupItemsRule.SUMMARY_FINDING_ID);
        Assert.Equal(Verdict.Info, summary.Verdict);
        Assert.Contains("2", summary.Title, StringComparison.Ordinal);
        Assert.Equal(CoreStrings.Startup_Evidence_Coverage, summary.Evidence);
        Assert.Contains("예약 작업", summary.Evidence, StringComparison.Ordinal);
        Assert.Contains("서비스", summary.Evidence, StringComparison.Ordinal);
        Assert.Contains("패키지", summary.Evidence, StringComparison.Ordinal);
        Assert.Contains(summary.Actions, a => a is OpenSettingsAction { Uri: StartupItemsRule.STARTUP_SETTINGS_URI });
    }

    /// <summary>항목이 0개여도(조회 성공) 요약과 부팅 영향만 내고 실패로 바꾸지 않는다.</summary>
    [Fact]
    public void 항목이_없어도_요약을_낸다()
    {
        var findings = RULE.Evaluate(Snapshot([]));

        Assert.Equal(2, findings.Count);
        Assert.Contains("0", Assert.Single(findings, f => f.Id == StartupItemsRule.SUMMARY_FINDING_ID).Title, StringComparison.Ordinal);
        Assert.Empty(Items(findings));
    }

    /// <summary>제목·근거·상세에는 전체 경로를 넣지 않고(명령 문자열은 측정값에만), 경로 형태의 이름은 파일 이름만 보인다.</summary>
    [Fact]
    public void 제목_근거_상세에_전체_경로가_없다()
    {
        var findings = RULE.Evaluate(Snapshot(
            [
                new FakeStartupItem("Chat"),
                new FakeStartupItem(PERSONAL_PROFILE + @"\Tools\helper.exe", StartupItemsProbeContract.SOURCE_HKLM64_RUN),
                new FakeStartupItem("Notes.lnk", StartupItemsProbeContract.SOURCE_USER_FOLDER, Command: PERSONAL_PROFILE + @"\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Notes.lnk"),
            ],
            ProbeStatus.Partial,
            StartupItemsProbeContract.SOURCE_HKLM32_RUN));

        foreach (var finding in findings)
        {
            var text = finding.Title + "\n" + finding.Evidence + "\n" + (finding.Detail ?? string.Empty);
            Assert.DoesNotContain(@":\", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Kimtester", text, StringComparison.OrdinalIgnoreCase);
        }

        var items = Items(findings);
        Assert.Contains("helper.exe", items[1].Title, StringComparison.Ordinal);
        Assert.Contains(items[0].Measured, m => m.Name.EndsWith("." + StartupItemsProbeContract.FIELD_COMMAND, StringComparison.Ordinal));
    }

    /// <summary>같은 이름이라도 출처가 다르면 서로 다른 Finding ID이며 출처가 근거에 표시된다.</summary>
    [Fact]
    public void 출처가_다르면_ID가_다르다()
    {
        var items = Items(RULE.Evaluate(Snapshot(
        [
            new FakeStartupItem("Sync", StartupItemsProbeContract.SOURCE_HKLM64_RUN),
            new FakeStartupItem("Sync", StartupItemsProbeContract.SOURCE_HKLM32_RUN),
        ])));

        Assert.Equal(2, items.Length);
        Assert.NotEqual(items[0].Id, items[1].Id);
        Assert.Contains(CoreStrings.Startup_Source_Hklm64Run, items[0].Evidence, StringComparison.Ordinal);
        Assert.Contains(CoreStrings.Startup_Source_Hklm32Run, items[1].Evidence, StringComparison.Ordinal);
    }

    /// <summary>부분 수집이면 읽은 항목은 유지하고, 요약 상세에 읽지 못한 위치 이름(경로 아님)을 적는다.</summary>
    [Fact]
    public void 부분_수집은_읽지_못한_위치를_요약에_적는다()
    {
        var findings = RULE.Evaluate(Snapshot([new FakeStartupItem("Chat")], ProbeStatus.Partial, StartupItemsProbeContract.SOURCE_HKLM64_RUN));

        Assert.Single(Items(findings));
        var summary = Assert.Single(findings, f => f.Id == StartupItemsRule.SUMMARY_FINDING_ID);
        Assert.NotNull(summary.Detail);
        Assert.Contains(CoreStrings.Startup_Source_Hklm64Run, summary.Detail, StringComparison.Ordinal);
    }

    /// <summary>프로브가 실패·건너뜀이면 판정하지 않는다(엔진이 상태 Finding으로 알림).</summary>
    [Theory]
    [InlineData(ProbeStatus.Failed)]
    [InlineData(ProbeStatus.Skipped)]
    [InlineData(ProbeStatus.Cancelled)]
    public void 실패한_프로브는_판정하지_않는다(ProbeStatus status)
    {
        var snapshot = HardwareRuleTestData.Snapshot(EngineTestData.CreateResult(StartupItemsProbeContract.PROBE_ID, status));

        Assert.Empty(RULE.Evaluate(snapshot));
    }
}
