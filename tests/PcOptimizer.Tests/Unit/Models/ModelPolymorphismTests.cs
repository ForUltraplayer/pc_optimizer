/**
 * @file    : ModelPolymorphismTests.cs
 * @author  : rudals252
 * @brief   : 닫힌 계층(MeasurementValue, FindingAction)이 사용자 지정 변환기 없이 System.Text.Json으로 왕복되는지 검증하는 단위 테스트
 */

// 기본 패키지
using System.Text.Json;

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Models;

/// <summary>
/// P2의 JSON 내보내기가 사용자 지정 변환기 없이 가능하도록 다형성 특성이 붙어 있는지 확인합니다.
/// </summary>
public class ModelPolymorphismTests
{
    /// <summary>
    /// 측정값의 모든 구체 형식이 기반 형식으로 직렬화·역직렬화된 뒤에도 같은 형식과 값을 유지한다.
    /// </summary>
    [Fact]
    public void MeasurementValue는_구체_형식을_유지하며_왕복된다()
    {
        MeasurementValue[] values =
        [
            new IntegerValue(144),
            new DecimalValue(59.94),
            new BooleanValue(false),
            new TextValue("DDR5"),
            new TextListValue(["C:", "D:"]),
        ];

        foreach (var value in values)
        {
            var json = JsonSerializer.Serialize(value);
            var restored = JsonSerializer.Deserialize<MeasurementValue>(json);

            Assert.NotNull(restored);
            Assert.Equal(value.GetType(), restored.GetType());
        }

        var list = JsonSerializer.Deserialize<MeasurementValue>(JsonSerializer.Serialize<MeasurementValue>(values[4]));
        Assert.Equal(["C:", "D:"], Assert.IsType<TextListValue>(list).Values);
    }

    /// <summary>
    /// 동작의 모든 구체 형식이 기반 형식으로 왕복된 뒤에도 같은 형식과 값을 유지한다.
    /// </summary>
    [Fact]
    public void FindingAction은_구체_형식을_유지하며_왕복된다()
    {
        FindingAction[] actions =
        [
            new ShowDetailsAction(),
            new OpenSettingsAction("ms-settings:display"),
            new KeepAction(),
            new ApplyAction(),
            new OpenLinkAction("https://www.nvidia.com/"),
        ];

        foreach (var action in actions)
        {
            var json = JsonSerializer.Serialize(action);
            var restored = JsonSerializer.Deserialize<FindingAction>(json);

            Assert.Equal(action, restored);
        }
    }
}
