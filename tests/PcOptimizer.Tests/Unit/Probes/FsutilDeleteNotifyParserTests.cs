/**
 * @file    : FsutilDeleteNotifyParserTests.cs
 * @author  : rudals252
 * @brief   : fsutil DisableDeleteNotify 출력 파서를 한국어(실측 CP949 포함)·영어 fixture와 형식 변형(설정 안 됨·대소문자·빈 입력)으로 검증
 */

// 기본 패키지
using System.IO;
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// <see cref="FsutilDeleteNotifyParser"/>를 검증합니다. fixture는 Fixtures/Fsutil 아래에 있으며,
/// ko-captured는 이 PC에서 캡처한 실제 출력(비식별), 나머지는 형식 변형을 위한 대표 샘플입니다.
/// </summary>
public sealed class FsutilDeleteNotifyParserTests
{
    private const string FIXTURE_FOLDER = "Fixtures";
    private const string FSUTIL_FOLDER = "Fsutil";
    private const string NTFS = TrimPolicyProbeContract.FILE_SYSTEM_NTFS;
    private const string REFS = TrimPolicyProbeContract.FILE_SYSTEM_REFS;

    /// <summary>
    /// fixture 파일 경로를 만든다.
    /// </summary>
    private static string FixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, FIXTURE_FOLDER, FSUTIL_FOLDER, fileName);
    }

    /// <summary>
    /// UTF-8 fixture를 읽어 파싱한다.
    /// </summary>
    private static IReadOnlyDictionary<string, long> ParseFixture(string fileName)
    {
        return FsutilDeleteNotifyParser.Parse(File.ReadAllText(FixturePath(fileName), Encoding.UTF8));
    }

    /// <summary>이 PC에서 캡처한 한국어 출력(NTFS·ReFS 모두 0)을 읽는다.</summary>
    [Fact]
    public void 한국어_실측_출력을_읽는다()
    {
        var values = ParseFixture("ko-captured.txt");

        Assert.Equal(0, values[NTFS]);
        Assert.Equal(0, values[REFS]);
        Assert.Equal(2, values.Count);
    }

    /// <summary>CP949 원시 바이트를 바이트 보존(Latin-1)으로 디코딩해도 ASCII 키·숫자만으로 읽는다(프로브의 디코딩 방식).</summary>
    [Fact]
    public void CP949_원시_바이트도_읽는다()
    {
        var text = Encoding.Latin1.GetString(File.ReadAllBytes(FixturePath("ko-captured-cp949.bin")));

        var values = FsutilDeleteNotifyParser.Parse(text);

        Assert.Equal(0, values[NTFS]);
        Assert.Equal(0, values[REFS]);
    }

    /// <summary>한국어 출력에서 파일 시스템별로 서로 다른 값을 구분한다.</summary>
    [Fact]
    public void 한국어_파일_시스템별_값을_구분한다()
    {
        var values = ParseFixture("ko-ntfs-enabled-refs-disabled.txt");

        Assert.Equal(0, values[NTFS]);
        Assert.Equal(1, values[REFS]);
    }

    /// <summary>영어 출력을 읽는다.</summary>
    [Fact]
    public void 영어_출력을_읽는다()
    {
        var values = ParseFixture("en-enabled.txt");

        Assert.Equal(0, values[NTFS]);
        Assert.Equal(0, values[REFS]);
    }

    /// <summary>"설정되지 않음" 줄은 값으로 만들지 않는다(0으로 대체하지 않음).</summary>
    [Theory]
    [InlineData("en-ntfs-disabled-refs-not-set.txt", 1L)]
    [InlineData("ko-refs-not-set.txt", 0L)]
    public void 설정되지_않은_파일_시스템은_값이_없다(string fileName, long expectedNtfs)
    {
        var values = ParseFixture(fileName);

        Assert.Equal(expectedNtfs, values[NTFS]);
        Assert.False(values.ContainsKey(REFS));
    }

    /// <summary>대소문자·공백·LF 줄바꿈 변형과 NUL 문자가 섞여도 키 이름과 끝 숫자로 읽는다.</summary>
    [Fact]
    public void 형식_변형을_허용한다()
    {
        var values = FsutilDeleteNotifyParser.Parse("  ntfs   disabledeletenotify=1\nREFS DisableDeleteNotify   =   0 (x)\n");

        Assert.Equal(1, values[NTFS]);
        Assert.Equal(0, values[REFS]);
        Assert.Equal(1, FsutilDeleteNotifyParser.Parse("N\0T\0F\0S\0 \0D\0i\0s\0a\0b\0l\0e\0D\0e\0l\0e\0t\0e\0N\0o\0t\0i\0f\0y\0 \0=\0 \01\0")[NTFS]);
    }

    /// <summary>키가 줄 중간에 있거나 알 수 없는 파일 시스템이면 무시한다.</summary>
    [Fact]
    public void 줄_중간의_키와_알_수_없는_파일_시스템은_무시한다()
    {
        var values = FsutilDeleteNotifyParser.Parse("note: NTFS DisableDeleteNotify = 1\r\nFAT32 DisableDeleteNotify = 1\r\nDisableDeleteNotify = 1\r\n");

        Assert.Empty(values);
    }

    /// <summary>빈 입력·null·관계없는 출력은 빈 결과다(프로브가 Failed로 처리).</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("오류: 액세스가 거부되었습니다.\r\n")]
    [InlineData("The FSUTIL utility requires that you have administrative privileges.")]
    public void 관계없는_출력은_빈_결과다(string? output)
    {
        Assert.Empty(FsutilDeleteNotifyParser.Parse(output));
    }

    /// <summary>같은 파일 시스템이 두 번 나오면 첫 값을 쓴다.</summary>
    [Fact]
    public void 중복_줄은_첫_값을_쓴다()
    {
        var values = FsutilDeleteNotifyParser.Parse("NTFS DisableDeleteNotify = 0\r\nNTFS DisableDeleteNotify = 1\r\n");

        Assert.Equal(0, values[NTFS]);
    }
}
