/**
 * @file    : ElevatedRescanArgumentsTests.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 재검사 고정 인자의 해석(정상·순서·잡음 무시·누락/잘못된 SID·GUID·중복은 일반 시작)과 명령줄 생성 형태를 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="ElevatedRescanArguments"/>를 검증합니다.
/// </summary>
public sealed class ElevatedRescanArgumentsTests
{
    private const string SID = "S-1-5-21-1111111111-2222222222-3333333333-1001";
    private const string CONTEXT_TEXT = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static readonly Guid CONTEXT = Guid.Parse(CONTEXT_TEXT);

    /// <summary>
    /// 공백으로 나눈 인자 목록(빈 문자열은 인자 없음).
    /// </summary>
    private static string[] Split(string commandLine)
    {
        return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>고정 형태의 인자는 재검사 인자로 해석한다(플래그 순서는 무관).</summary>
    [Theory]
    [InlineData($"--elevated-rescan --origin-sid {SID} --context {CONTEXT_TEXT}")]
    [InlineData($"--context {CONTEXT_TEXT} --elevated-rescan --origin-sid {SID}")]
    public void 고정_인자를_해석한다(string commandLine)
    {
        var parsed = ElevatedRescanArguments.Parse(Split(commandLine));

        Assert.NotNull(parsed);
        Assert.Equal(SID, parsed.OriginSid);
        Assert.Equal(CONTEXT, parsed.ContextId);
    }

    /// <summary>알 수 없는 인자는 무시하며 경로·명령·다른 값으로 쓰지 않는다.</summary>
    [Fact]
    public void 알_수_없는_인자는_무시한다()
    {
        string[] args = [@"C:\Windows\System32\cmd.exe", "--elevated-rescan", "/c", "calc.exe", "--origin-sid", SID, "--rules", @"C:\Users\Public\evil.ini", "--context", CONTEXT_TEXT, "&", "whoami"];

        var parsed = ElevatedRescanArguments.Parse(args);

        Assert.NotNull(parsed);
        Assert.Equal(SID, parsed.OriginSid);
        Assert.Equal(CONTEXT, parsed.ContextId);
    }

    /// <summary>플래그·SID·컨텍스트가 하나라도 없으면 재검사가 아니라 일반 시작(null)이다.</summary>
    [Theory]
    [InlineData("")]
    [InlineData($"--origin-sid {SID} --context {CONTEXT_TEXT}")]
    [InlineData($"--elevated-rescan --context {CONTEXT_TEXT}")]
    [InlineData($"--elevated-rescan --origin-sid {SID}")]
    [InlineData("--elevated-rescan --origin-sid")]
    [InlineData($"--elevated-rescan --context {CONTEXT_TEXT} --origin-sid")]
    public void 누락되면_일반_시작이다(string commandLine)
    {
        Assert.Null(ElevatedRescanArguments.Parse(Split(commandLine)));
    }

    /// <summary>SID·GUID 형식이 아니면(경로·명령·주입 문자열 포함) 일반 시작이다.</summary>
    [Theory]
    [InlineData(@"C:\Windows\System32\cmd.exe", CONTEXT_TEXT)]
    [InlineData("S-1-5-21-abc-1001", CONTEXT_TEXT)]
    [InlineData("S-1-5-21-1-2-3 & calc", CONTEXT_TEXT)]
    [InlineData("S-1", CONTEXT_TEXT)]
    [InlineData(SID + "\n", CONTEXT_TEXT)]
    [InlineData("", CONTEXT_TEXT)]
    [InlineData("--context", CONTEXT_TEXT)]
    [InlineData(SID, "not-a-guid")]
    [InlineData(SID, "{" + CONTEXT_TEXT + "}")]
    [InlineData(SID, "")]
    public void 형식이_틀리면_일반_시작이다(string sid, string context)
    {
        Assert.Null(ElevatedRescanArguments.Parse(["--elevated-rescan", "--origin-sid", sid, "--context", context]));
    }

    /// <summary>같은 플래그가 두 번 나오면 어느 값을 믿을지 모호하므로 일반 시작이다.</summary>
    [Theory]
    [InlineData($"--elevated-rescan --elevated-rescan --origin-sid {SID} --context {CONTEXT_TEXT}")]
    [InlineData($"--elevated-rescan --origin-sid {SID} --origin-sid S-1-5-21-9-9-9-500 --context {CONTEXT_TEXT}")]
    [InlineData($"--elevated-rescan --origin-sid {SID} --context {CONTEXT_TEXT} --context {CONTEXT_TEXT}")]
    public void 중복_플래그는_일반_시작이다(string commandLine)
    {
        Assert.Null(ElevatedRescanArguments.Parse(Split(commandLine)));
    }

    /// <summary>플래그 이름은 대소문자까지 정확히 같아야 한다.</summary>
    [Fact]
    public void 플래그는_정확히_일치해야_한다()
    {
        Assert.Null(ElevatedRescanArguments.Parse(["--Elevated-Rescan", "--origin-sid", SID, "--context", CONTEXT_TEXT]));
    }

    /// <summary>명령줄은 정해진 세 플래그와 SID·GUID만 담고, 다시 해석하면 같은 값이다.</summary>
    [Fact]
    public void 명령줄은_고정_형태다()
    {
        var arguments = new ElevatedRescanArguments(SID, CONTEXT);

        var commandLine = arguments.ToCommandLine();

        Assert.Equal($"--elevated-rescan --origin-sid {SID} --context {CONTEXT_TEXT}", commandLine);
        Assert.Equal(arguments, ElevatedRescanArguments.Parse(commandLine.Split(' ')));
    }

    /// <summary>SID 형식이 아니면 명령줄을 만들지 않는다(인자 주입 방지).</summary>
    [Fact]
    public void 잘못된_SID로는_명령줄을_만들지_않는다()
    {
        var arguments = new ElevatedRescanArguments("S-1-5-21-1 --rules C:\\evil.ini", CONTEXT);

        Assert.Throws<InvalidOperationException>(() => arguments.ToCommandLine());
    }
}
