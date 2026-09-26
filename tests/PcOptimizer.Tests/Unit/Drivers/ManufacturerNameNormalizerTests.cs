/**
 * @file    : ManufacturerNameNormalizerTests.cs
 * @author  : rudals252
 * @brief   : 제조사 이름 정규화(소문자·문장 부호 제거·끝의 Inc./Corp./Co., Ltd./Corporation 제거) 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Drivers;

namespace PcOptimizer.Tests.Unit.Drivers;

/// <summary>
/// <see cref="ManufacturerNameNormalizer"/>를 검증합니다.
/// </summary>
public sealed class ManufacturerNameNormalizerTests
{
    /// <summary>회사 형태 접미사와 문장 부호를 없애고 소문자로 바꾼다.</summary>
    [Theory]
    [InlineData("Micro-Star International Co., Ltd.", "micro star international")]
    [InlineData("Dell Inc.", "dell")]
    [InlineData("  LENOVO  ", "lenovo")]
    [InlineData("ASUSTeK COMPUTER INC.", "asustek computer")]
    [InlineData("Microsoft Corporation", "microsoft")]
    [InlineData("Gigabyte Technology Co., Ltd.", "gigabyte technology")]
    [InlineData("Hewlett-Packard", "hewlett packard")]
    [InlineData("VMware, Inc.", "vmware")]
    [InlineData("Corp Inc.", "corp")]
    public void 이름을_정규화한다(string raw, string expected)
    {
        Assert.Equal(expected, ManufacturerNameNormalizer.Normalize(raw));
    }

    /// <summary>값이 없거나 문장 부호뿐이면 빈 문자열이다.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ., ")]
    public void 값이_없으면_빈_문자열이다(string? raw)
    {
        Assert.Equal(string.Empty, ManufacturerNameNormalizer.Normalize(raw));
    }
}
