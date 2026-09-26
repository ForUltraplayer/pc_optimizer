/**
 * @file    : ElevationRelauncherTests.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 재검사 시작기가 같은 실행 파일을 runas·셸 실행·고정 인자로만 시작하고, UAC 취소(1223)·그 밖의 실패·사전 점검 실패를 결과로 돌려주며 SID를 로그에 남기지 않는지 가짜 시작기로 검증(실제 UAC 없음)
 */

// 기본 패키지
using System.ComponentModel;
using System.Text.RegularExpressions;

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Tests.Unit.App.Fakes;
using PcOptimizer.Tests.Unit.Engine.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>
/// <see cref="ElevationRelauncher"/>를 검증합니다. 프로세스를 실제로 시작하지 않습니다.
/// </summary>
public sealed class ElevationRelauncherTests
{
    private const string APP_PATH = @"C:\Program Files\PcOptimizer\PcOptimizer.App.exe";
    private const int ERROR_FILE_NOT_FOUND = 2;

    private static readonly Regex FIXED_ARGUMENTS = new(
        "^--elevated-rescan --origin-sid " + Regex.Escape(FakeElevationState.USER_SID) + " --context [0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private readonly RecordingLogger _logger = new();

    /// <summary>
    /// 시작기를 만든다.
    /// </summary>
    private ElevationRelauncher Create(RecordingProcessStarter starter, IElevationState? elevation = null, string? processPath = APP_PATH)
    {
        return new ElevationRelauncher(starter, elevation ?? new FakeElevationState(isElevated: false), () => processPath, _logger);
    }

    /// <summary>일반 권한이면 같은 실행 파일을 runas·셸 실행으로, 고정된 세 플래그와 SID·GUID만 붙여 시작한다.</summary>
    [Fact]
    public void 같은_실행_파일을_runas와_고정_인자로_시작한다()
    {
        var starter = new RecordingProcessStarter();

        var result = Create(starter).Relaunch();

        Assert.Equal(ElevationRelaunchOutcome.Started, result.Outcome);
        Assert.Null(result.ErrorCode);
        var startInfo = Assert.Single(starter.Started);
        Assert.Equal(APP_PATH, startInfo.FileName);
        Assert.Equal(ElevationRelauncher.RUNAS_VERB, startInfo.Verb);
        Assert.True(startInfo.UseShellExecute);
        Assert.Matches(FIXED_ARGUMENTS, startInfo.Arguments);
        Assert.Empty(startInfo.ArgumentList);
        var parsed = ElevatedRescanArguments.Parse(startInfo.Arguments.Split(' '));
        Assert.NotNull(parsed);
        Assert.Equal(FakeElevationState.USER_SID, parsed.OriginSid);
    }

    /// <summary>요청마다 새 컨텍스트 ID를 만든다.</summary>
    [Fact]
    public void 요청마다_새_컨텍스트_ID다()
    {
        var starter = new RecordingProcessStarter();
        var relauncher = Create(starter);

        relauncher.Relaunch();
        relauncher.Relaunch();

        var first = ElevatedRescanArguments.Parse(starter.Started[0].Arguments.Split(' '));
        var second = ElevatedRescanArguments.Parse(starter.Started[1].Arguments.Split(' '));
        Assert.NotEqual(first!.ContextId, second!.ContextId);
    }

    /// <summary>UAC 취소(Win32 오류 1223)는 실패가 아니라 취소 결과다.</summary>
    [Fact]
    public void UAC_취소는_취소_결과다()
    {
        var starter = new RecordingProcessStarter { ThrowOnStart = new Win32Exception(ElevationRelauncher.ERROR_CANCELLED) };

        var result = Create(starter).Relaunch();

        Assert.Equal(ElevationRelaunchOutcome.Cancelled, result.Outcome);
        Assert.Null(result.ErrorCode);
        Assert.Single(starter.Started);
    }

    /// <summary>그 밖의 시작 실패는 예외 형식 이름만 돌려주고 메시지 원문을 담지 않는다.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 그_밖의_실패는_형식_이름만_돌려준다(bool win32)
    {
        Exception error = win32
            ? new Win32Exception(ERROR_FILE_NOT_FOUND, @"C:\Users\Kimtester secret")
            : new InvalidOperationException(@"C:\Users\Kimtester secret");
        var starter = new RecordingProcessStarter { ThrowOnStart = error };

        var result = Create(starter).Relaunch();

        Assert.Equal(ElevationRelaunchOutcome.Failed, result.Outcome);
        Assert.Equal(error.GetType().Name, result.ErrorCode);
        Assert.All(_logger.Entries, entry => Assert.DoesNotContain("Kimtester", entry.Message, StringComparison.Ordinal));
    }

    /// <summary>이미 관리자 권한·실행 파일 경로 불명·dotnet 호스트·SID 불명이면 시작하지 않고 실패 코드를 돌려준다.</summary>
    [Theory]
    [InlineData(true, APP_PATH, FakeElevationState.USER_SID, ElevationRelauncher.ERROR_ALREADY_ELEVATED)]
    [InlineData(false, null, FakeElevationState.USER_SID, ElevationRelauncher.ERROR_PROCESS_PATH_UNAVAILABLE)]
    [InlineData(false, @"C:\Program Files\dotnet\dotnet.exe", FakeElevationState.USER_SID, ElevationRelauncher.ERROR_UNSUPPORTED_HOST)]
    [InlineData(false, APP_PATH, null, ElevationRelauncher.ERROR_SID_UNAVAILABLE)]
    [InlineData(false, APP_PATH, "S-1-5-21-1 --rules evil.ini", ElevationRelauncher.ERROR_SID_UNAVAILABLE)]
    public void 사전_점검에_걸리면_시작하지_않는다(bool isElevated, string? processPath, string? sid, string expectedCode)
    {
        var starter = new RecordingProcessStarter();

        var result = Create(starter, new FakeElevationState(isElevated, sid), processPath).Relaunch();

        Assert.Equal(ElevationRelaunchOutcome.Failed, result.Outcome);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Empty(starter.Started);
    }

    /// <summary>원래 사용자 SID는 로그에 남기지 않는다.</summary>
    [Fact]
    public void SID를_로그에_남기지_않는다()
    {
        Create(new RecordingProcessStarter()).Relaunch();
        Create(new RecordingProcessStarter { ThrowOnStart = new Win32Exception(ElevationRelauncher.ERROR_CANCELLED) }).Relaunch();

        Assert.NotEmpty(_logger.Entries);
        Assert.All(_logger.Entries, entry => Assert.DoesNotContain(FakeElevationState.USER_SID, entry.Message, StringComparison.OrdinalIgnoreCase));
    }
}
