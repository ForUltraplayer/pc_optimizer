/**
 * @file    : NvidiaDriverVersionTests.cs
 * @author  : rudals252
 * @brief   : Windows 드라이버 버전 → NVIDIA 표기 버전 변환 벡터와 형식 오류(null) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>
/// <see cref="NvidiaDriverVersion"/> 변환을 검증합니다. 벡터는 공개된 버전 표기 규칙 예시이며 이 PC 값이 아닙니다.
/// </summary>
public sealed class NvidiaDriverVersionTests
{
    /// <summary>마지막 두 그룹을 이은 값의 마지막 5자리를 "xxx.yy"로 표기한다.</summary>
    [Theory]
    [InlineData("32.0.16.1656", "616.56")]
    [InlineData("31.0.15.5222", "552.22")]
    [InlineData("31.0.15.3623", "536.23")]
    [InlineData("30.0.14.7141", "471.41")]
    [InlineData("27.21.14.5671", "456.71")]
    [InlineData("32.0.15.8157", "581.57")]
    [InlineData("26.21.14.4120", "441.20")]
    [InlineData(" 32.0.16.1656 ", "616.56")]
    public void 알려진_벡터를_변환한다(string windowsVersion, string expected)
    {
        Assert.Equal(expected, NvidiaDriverVersion.FromWindowsVersion(windowsVersion));
    }

    /// <summary>마지막 그룹은 4자리로 채워 이어 붙인다(예: 102 → 0102).</summary>
    [Fact]
    public void 마지막_그룹은_4자리로_채운다()
    {
        Assert.Equal("401.02", NvidiaDriverVersion.FromWindowsVersion("30.0.14.102"));
    }

    /// <summary>형식이 맞지 않으면 null이다(추측 변환 없음).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("32.0.16")]
    [InlineData("32.0.16.1656.1")]
    [InlineData("32.0.x.1656")]
    [InlineData("32.0.16.99999")]
    [InlineData("-1.0.16.1656")]
    [InlineData("32.0.-16.1656")]
    [InlineData("32..16.1656")]
    public void 형식_오류는_null이다(string? windowsVersion)
    {
        Assert.Null(NvidiaDriverVersion.FromWindowsVersion(windowsVersion));
    }
}
