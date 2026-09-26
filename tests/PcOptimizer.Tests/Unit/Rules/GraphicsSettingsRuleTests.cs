/**
 * @file    : GraphicsSettingsRuleTests.cs
 * @author  : rudals252
 * @brief   : 그래픽 설정 규칙의 HAGS·게임 모드 설정값 정보, 키 부재 시 Unsupported(꺼짐으로 대체 금지), 알 수 없는 값·형식, 읽기 실패, 설정 URI, 지원·실행·적용 단정 문구 부재 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Resources;
using PcOptimizer.Core.Rules;
using PcOptimizer.Tests.Unit.Engine;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// 가짜 레지스트리 값 하나입니다. Exists가 null이면 "읽지 못함"(존재 여부 측정값 없음)입니다.
/// </summary>
public sealed record FakeRegistryValue(bool? Exists, string? Kind = GraphicsSettingsProbeContract.KIND_DWORD, long? Value = null);

/// <summary>
/// <see cref="GraphicsSettingsRule"/>을 검증합니다.
/// </summary>
public sealed class GraphicsSettingsRuleTests
{
    private static readonly GraphicsSettingsRule RULE = new();

    /// <summary>설정값만으로 단정하면 안 되는 표현.</summary>
    private static readonly string[] FORBIDDEN_PHRASES = ["지원됩니다", "지원돼요", "실행 중이에요", "적용 완료", "적용됐", "적용되었"];

    /// <summary>
    /// 두 설정 값으로 스냅샷을 만든다.
    /// </summary>
    private static ScanSnapshot Snapshot(FakeRegistryValue hags, FakeRegistryValue gameMode, ProbeStatus status = ProbeStatus.Success)
    {
        var hagsMeasurements = new List<Measurement>();
        var gameModeMeasurements = new List<Measurement>();
        Add(hagsMeasurements, GraphicsSettingsProbeContract.HAGS_PREFIX, hags);
        Add(gameModeMeasurements, GraphicsSettingsProbeContract.GAME_MODE_PREFIX, gameMode);
        return HardwareRuleTestData.Snapshot(
            EngineTestData.CreateResult(GraphicsSettingsProbeContract.PROBE_ID, status, hagsMeasurements),
            EngineTestData.CreateResult(GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID, status, gameModeMeasurements));
    }

    /// <summary>
    /// 값 하나의 측정값을 추가한다.
    /// </summary>
    private static void Add(List<Measurement> measurements, string prefix, FakeRegistryValue value)
    {
        if (value.Exists is not { } exists)
        {
            return;
        }

        measurements.Add(HardwareRuleTestData.Boolean(GraphicsSettingsProbeContract.MeasurementName(prefix, GraphicsSettingsProbeContract.FIELD_EXISTS), exists));
        if (!exists)
        {
            return;
        }

        if (value.Kind is not null)
        {
            measurements.Add(RuleTestData.Text(GraphicsSettingsProbeContract.MeasurementName(prefix, GraphicsSettingsProbeContract.FIELD_KIND), value.Kind));
        }

        if (value.Value is { } number)
        {
            measurements.Add(RuleTestData.Integer(GraphicsSettingsProbeContract.MeasurementName(prefix, GraphicsSettingsProbeContract.FIELD_VALUE), number));
        }
    }

    /// <summary>
    /// ID로 Finding을 찾는다.
    /// </summary>
    private static Finding Find(IReadOnlyList<Finding> findings, string id)
    {
        return Assert.Single(findings, f => f.Id == id);
    }

    /// <summary>HAGS 2는 켜짐, 1은 꺼짐 설정값 Info이며 그래픽 고급 설정 열기를 제공한다.</summary>
    [Theory]
    [InlineData(GraphicsSettingsProbeContract.HAGS_ENABLED, true)]
    [InlineData(GraphicsSettingsProbeContract.HAGS_DISABLED, false)]
    public void HAGS_알려진_값은_설정값_정보다(long value, bool on)
    {
        var findings = RULE.Evaluate(Snapshot(new FakeRegistryValue(true, Value: value), new FakeRegistryValue(false)));

        var finding = Find(findings, GraphicsSettingsRule.HAGS_FINDING_ID);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Equal(FindingCategory.Graphics, finding.Category);
        Assert.Contains(on ? CoreStrings.Graphics_State_On : CoreStrings.Graphics_State_Off, finding.Title, StringComparison.Ordinal);
        Assert.Contains("설정값", finding.Title, StringComparison.Ordinal);
        Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: GraphicsSettingsRule.HAGS_SETTINGS_URI });
    }

    /// <summary>게임 모드 1은 켜짐, 0은 꺼짐 설정값 Info이며 게임 모드 설정 열기를 제공한다.</summary>
    [Theory]
    [InlineData(GraphicsSettingsProbeContract.GAME_MODE_ENABLED, true)]
    [InlineData(GraphicsSettingsProbeContract.GAME_MODE_DISABLED, false)]
    public void 게임_모드_알려진_값은_설정값_정보다(long value, bool on)
    {
        var findings = RULE.Evaluate(Snapshot(new FakeRegistryValue(false), new FakeRegistryValue(true, Value: value)));

        var finding = Find(findings, GraphicsSettingsRule.GAME_MODE_FINDING_ID);
        Assert.Equal(Verdict.Info, finding.Verdict);
        Assert.Contains(on ? CoreStrings.Graphics_State_On : CoreStrings.Graphics_State_Off, finding.Title, StringComparison.Ordinal);
        Assert.Contains(finding.Actions, a => a is OpenSettingsAction { Uri: GraphicsSettingsRule.GAME_MODE_SETTINGS_URI });
    }

    /// <summary>값이 없으면 꺼짐이 아니라 CannotVerify(Unsupported)이며 '꺼짐'으로 표시하지 않는다.</summary>
    [Fact]
    public void 키_부재는_꺼짐이_아닌_지원_불가다()
    {
        var findings = RULE.Evaluate(Snapshot(new FakeRegistryValue(false), new FakeRegistryValue(false)));

        Assert.Equal(2, findings.Count);
        Assert.All(findings, finding =>
        {
            Assert.Equal(Verdict.CannotVerify, finding.Verdict);
            Assert.Equal(CannotVerifyReason.Unsupported, finding.CannotVerifyReason);
            Assert.DoesNotContain(CoreStrings.Graphics_State_Off, finding.Title, StringComparison.Ordinal);
        });
    }

    /// <summary>알려지지 않은 값이나 DWORD가 아닌 형식은 CannotVerify(Unsupported)다.</summary>
    [Theory]
    [InlineData(GraphicsSettingsProbeContract.KIND_DWORD, 3L)]
    [InlineData(GraphicsSettingsProbeContract.KIND_DWORD, 0L)]
    [InlineData("String", null)]
    [InlineData(GraphicsSettingsProbeContract.KIND_DWORD, null)]
    public void 알려지지_않은_값은_해석하지_않는다(string kind, long? value)
    {
        var findings = RULE.Evaluate(Snapshot(new FakeRegistryValue(true, kind, value), new FakeRegistryValue(false)));

        var finding = Find(findings, GraphicsSettingsRule.HAGS_FINDING_ID);
        Assert.Equal(Verdict.CannotVerify, finding.Verdict);
        Assert.Equal(CannotVerifyReason.Unsupported, finding.CannotVerifyReason);
    }

    /// <summary>값을 읽지 못한 부분 수집은 CannotVerify(PartialData)이며 다른 설정의 Info는 유지한다.</summary>
    [Fact]
    public void 읽지_못한_값은_부분_데이터다()
    {
        var findings = RULE.Evaluate(Snapshot(
            new FakeRegistryValue(null), new FakeRegistryValue(true, Value: GraphicsSettingsProbeContract.GAME_MODE_ENABLED), ProbeStatus.Partial));

        Assert.Equal(CannotVerifyReason.PartialData, Find(findings, GraphicsSettingsRule.HAGS_FINDING_ID).CannotVerifyReason);
        Assert.Equal(Verdict.Info, Find(findings, GraphicsSettingsRule.GAME_MODE_FINDING_ID).Verdict);
    }

    /// <summary>프로브가 실패하면 판정하지 않는다.</summary>
    [Fact]
    public void 실패한_프로브는_판정하지_않는다()
    {
        var snapshot = HardwareRuleTestData.Snapshot(
            EngineTestData.CreateResult(GraphicsSettingsProbeContract.PROBE_ID, ProbeStatus.Failed),
            EngineTestData.CreateResult(GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID, ProbeStatus.Failed));

        Assert.Empty(RULE.Evaluate(snapshot));
    }

    /// <summary>사용자 범위 게임 모드 프로브를 건너뛰어도(다른 계정으로 승격) 시스템 범위 HAGS 판정은 유지하고 게임 모드는 판정하지 않는다.</summary>
    [Fact]
    public void 게임_모드_프로브를_건너뛰어도_HAGS는_판정한다()
    {
        var hags = new List<Measurement>();
        Add(hags, GraphicsSettingsProbeContract.HAGS_PREFIX, new FakeRegistryValue(true, Value: GraphicsSettingsProbeContract.HAGS_ENABLED));
        var snapshot = HardwareRuleTestData.Snapshot(
            EngineTestData.CreateResult(GraphicsSettingsProbeContract.PROBE_ID, ProbeStatus.Success, hags),
            EngineTestData.CreateResult(GraphicsSettingsProbeContract.GAME_MODE_PROBE_ID, ProbeStatus.Skipped));

        var finding = Assert.Single(RULE.Evaluate(snapshot));
        Assert.Equal(GraphicsSettingsRule.HAGS_FINDING_ID, finding.Id);
        Assert.Equal(Verdict.Info, finding.Verdict);
    }

    /// <summary>설정값만으로 지원·실행·재부팅 적용을 단정하지 않는다.</summary>
    [Theory]
    [InlineData(2L, 1L)]
    [InlineData(1L, 0L)]
    [InlineData(null, null)]
    public void 지원_실행_적용을_단정하지_않는다(long? hags, long? gameMode)
    {
        var findings = RULE.Evaluate(Snapshot(
            new FakeRegistryValue(hags is not null, Value: hags), new FakeRegistryValue(gameMode is not null, Value: gameMode)));

        foreach (var finding in findings)
        {
            var text = RuleTestData.AllText(finding);
            Assert.All(FORBIDDEN_PHRASES, phrase => Assert.DoesNotContain(phrase, text, StringComparison.Ordinal));
            Assert.NotEqual(Verdict.Candidate, finding.Verdict);
        }
    }
}
