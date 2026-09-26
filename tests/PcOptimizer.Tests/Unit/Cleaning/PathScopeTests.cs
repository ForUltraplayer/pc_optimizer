/**
 * @file    : PathScopeTests.cs
 * @author  : rudals252
 * @brief   : 경로 비교 도우미의 정규화(구분자·끝 구분자·드라이브 루트), 디렉터리 경계 단위 포함 판정(대소문자 무시, 이름이 겹치는 형제 제외), 부모·마지막 이름, 드라이브 절대 경로 판정 단위 테스트
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Tests.Unit.Cleaning;

/// <summary>
/// <see cref="PathScope"/>를 검증합니다(파일 시스템 접근 없음).
/// </summary>
public sealed class PathScopeTests
{
    /// <summary>'/'는 '\'로, 끝 구분자는 없애고 드라이브 루트는 "C:\"로 둔다.</summary>
    [Theory]
    [InlineData(@"C:/Users/a/", @"C:\Users\a")]
    [InlineData(@"C:\Users\a\\", @"C:\Users\a")]
    [InlineData(@"C:", @"C:\")]
    [InlineData(@"c:\", @"c:\")]
    public void 경로를_정규화한다(string input, string expected)
    {
        Assert.Equal(expected, PathScope.Normalize(input));
    }

    /// <summary>같거나 하위이면 참(대소문자 무시)이고, 이름 앞부분만 같은 형제는 거짓이다.</summary>
    [Theory]
    [InlineData(@"C:\Users\a", @"C:\Users\a", true)]
    [InlineData(@"c:\USERS\A\x", @"C:\Users\a", true)]
    [InlineData(@"C:\Users\ab", @"C:\Users\a", false)]
    [InlineData(@"C:\Users", @"C:\Users\a", false)]
    [InlineData(@"C:\anything", @"C:\", true)]
    [InlineData(@"D:\Users\a", @"C:\Users\a", false)]
    public void 디렉터리_경계로_포함을_판정한다(string path, string root, bool expected)
    {
        Assert.Equal(expected, PathScope.IsSameOrUnder(path, root));
    }

    /// <summary>하위 판정은 같은 경로를 포함하지 않는다.</summary>
    [Fact]
    public void 하위_판정은_같은_경로를_제외한다()
    {
        Assert.False(PathScope.IsStrictlyUnder(@"C:\a", @"c:\A\"));
        Assert.True(PathScope.IsStrictlyUnder(@"C:\a\b", @"C:\a"));
    }

    /// <summary>부모와 마지막 이름을 구한다(드라이브 루트의 부모는 없음).</summary>
    [Fact]
    public void 부모와_마지막_이름을_구한다()
    {
        Assert.Equal(@"C:\Users", PathScope.GetParent(@"C:\Users\a\"));
        Assert.Equal(@"C:\", PathScope.GetParent(@"C:\Users"));
        Assert.Null(PathScope.GetParent(@"C:\"));
        Assert.Equal("a", PathScope.GetLeafName(@"C:\Users\a\"));
        Assert.Equal(@"C:\", PathScope.GetLeafName(@"C:\"));
    }

    /// <summary>드라이브 절대 경로만 참이고 UNC·상대 경로는 거짓이다.</summary>
    [Theory]
    [InlineData(@"C:\x", true)]
    [InlineData(@"C:x", false)]
    [InlineData(@"\\server\share", false)]
    [InlineData(@"relative\x", false)]
    [InlineData(@"\\?\C:\x", false)]
    public void 드라이브_절대_경로를_판정한다(string path, bool expected)
    {
        Assert.Equal(expected, PathScope.IsDriveAbsolute(path));
    }
}
