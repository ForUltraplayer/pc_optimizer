/**
 * @file    : DriverVersionComparerTests.cs
 * @author  : rudals252
 * @brief   : NVIDIA 표기 드라이버 버전(주.부)의 정수 단위 비교·엄격 형식 검사·목록 포함 여부·최고 버전 선택 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.Tests.Unit.Drivers;

/// <summary>
/// <see cref="DriverVersionComparer"/>와 <see cref="DriverVersionNumber"/>를 검증합니다. 버전은 테스트 예시이며 이 PC 값이 아닙니다.
/// </summary>
public sealed class DriverVersionComparerTests
{
    /// <summary>주·부 버전을 각각 정수로 비교한다(617.14 &gt; 616.92 &gt; 616.56).</summary>
    [Theory]
    [InlineData("617.14", "616.92", 1)]
    [InlineData("616.92", "616.56", 1)]
    [InlineData("616.56", "617.14", -1)]
    [InlineData("616.56", "616.56", 0)]
    [InlineData("1000.01", "999.99", 1)]
    public void 주_부_버전을_정수로_비교한다(string left, string right, int expectedSign)
    {
        Assert.True(DriverVersionComparer.TryCompare(left, right, out var result));
        Assert.Equal(expectedSign, Math.Sign(result));
    }

    /// <summary>"617.14"와 "617.9"는 소수가 아니라 정수(14 &gt; 9)로 비교한다.</summary>
    [Fact]
    public void 부_버전은_소수가_아니라_정수로_비교한다()
    {
        Assert.True(DriverVersionComparer.TryCompare("617.14", "617.9", out var result));
        Assert.True(result > 0);
        Assert.True(DriverVersionComparer.TryCompare("617.9", "617.14", out var reverse));
        Assert.True(reverse < 0);
    }

    /// <summary>숫자.숫자 형식이 아니면 비교하지 않는다.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("617")]
    [InlineData("617.")]
    [InlineData(".14")]
    [InlineData("617.14.1")]
    [InlineData("v617.14")]
    [InlineData("617.14 ")]
    [InlineData("-617.14")]
    [InlineData("６１７.１４")]
    public void 형식이_아니면_해석하지_않는다(string? text)
    {
        Assert.False(DriverVersionNumber.TryParse(text, out _));
        Assert.False(DriverVersionComparer.TryCompare(text, "617.14", out _));
    }

    /// <summary>NVIDIA 응답 버전은 세·네 자리 주 버전과 두 자리 부 버전만 받는다.</summary>
    [Theory]
    [InlineData("617.14", true)]
    [InlineData("1001.02", true)]
    [InlineData("617.9", false)]
    [InlineData("61.14", false)]
    [InlineData("617.140", false)]
    [InlineData("617,14", false)]
    public void NVIDIA_응답_형식을_엄격히_검사한다(string text, bool expected)
    {
        Assert.Equal(expected, DriverVersionNumber.IsNvidiaResponseFormat(text));
    }

    /// <summary>목록 포함 여부는 문자열이 아니라 수치로 확인한다.</summary>
    [Fact]
    public void 목록_포함은_수치로_확인한다()
    {
        Assert.True(DriverVersionComparer.ContainsVersion(["617.14", "616.56"], "616.56"));
        Assert.False(DriverVersionComparer.ContainsVersion(["617.14", "616.92"], "616.56"));
        Assert.False(DriverVersionComparer.ContainsVersion(["617.14", "잘못된 값"], "616.5"));
    }

    /// <summary>가장 높은 버전을 수치로 고른다(해석할 수 없는 값은 무시).</summary>
    [Fact]
    public void 가장_높은_버전을_고른다()
    {
        Assert.Equal("617.14", DriverVersionComparer.Highest(["616.92", "617.9", "617.14", "x"]));
        Assert.Null(DriverVersionComparer.Highest(["x"]));
    }
}
