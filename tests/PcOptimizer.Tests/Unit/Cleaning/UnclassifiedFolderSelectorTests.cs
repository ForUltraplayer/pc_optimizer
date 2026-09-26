/**
 * @file    : UnclassifiedFolderSelectorTests.cs
 * @author  : rudals252
 * @brief   : 미분류 폴더 선정의 1,000,000,000바이트 경계, bottom-up 부모/자식 중복 제거와 부모 잔여 크기 규칙, 동률 경로 정렬, 상위 20개, 부분 관측 제외, 보호·제외 위치 비보고와 확장자·수정 시각 집계를 가짜 집계로 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;

namespace PcOptimizer.Tests.Unit.Cleaning;

/// <summary>
/// <see cref="UnclassifiedFolderSelector"/>를 가짜 디렉터리 집계로 검증합니다. 큰 크기는 모두 가짜 메타데이터입니다.
/// </summary>
public sealed class UnclassifiedFolderSelectorTests
{
    private const string PROFILE = @"C:\Users\tester";
    private const string PROGRAM_DATA = @"C:\ProgramData";
    private const long GB = 1_000_000_000;

    private static readonly DateTimeOffset OLD = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NEW = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 확장자 하나로 된 가짜 집계를 만든다.
    /// </summary>
    private static DirectoryAggregate Dir(string path, long bytes, long skipped = 0, string extension = ".bin", DateTimeOffset? newest = null, bool duplicates = false)
    {
        var extensions = bytes > 0 ? new Dictionary<string, long> { [extension] = bytes } : [];
        return new DirectoryAggregate(path, bytes, bytes > 0 ? 1 : 0, skipped, extensions, bytes > 0 ? newest ?? OLD : null, duplicates);
    }

    /// <summary>
    /// 기본 루트(프로필·ProgramData)와 제외 목록으로 선정한다.
    /// </summary>
    private static UnclassifiedSelection Select(IEnumerable<DirectoryAggregate> directories, params string[] excluded)
    {
        return UnclassifiedFolderSelector.Select(directories, [PROFILE, PROGRAM_DATA], excluded);
    }

    /// <summary>정확히 1,000,000,000바이트는 후보이고 1바이트 적으면 후보가 아니다.</summary>
    [Fact]
    public void 임계값_경계는_이상이다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\exact", UnclassifiedFolderSelector.UNCLASSIFIED_MIN_BYTES),
            Dir(PROFILE + @"\below", UnclassifiedFolderSelector.UNCLASSIFIED_MIN_BYTES - 1),
        ]);

        var candidate = Assert.Single(selection.Candidates);
        Assert.Equal(PROFILE + @"\exact", candidate.Path);
        Assert.Equal(1_000_000_000, candidate.Bytes);
        Assert.Equal(1, selection.QualifyingCount);
    }

    /// <summary>임계값 이상인 자식이 있으면 부모는 잔여 크기가 임계값 미만일 때 보고하지 않는다(가장 깊은 후보 우선).</summary>
    [Fact]
    public void 자식_후보가_있으면_부모는_잔여_크기로만_판정한다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\parent", GB / 5),
            Dir(PROFILE + @"\parent\child", GB + (GB / 2)),
        ]);

        var candidate = Assert.Single(selection.Candidates);
        Assert.Equal(PROFILE + @"\parent\child", candidate.Path);
        Assert.False(candidate.ExcludesReportedDescendants);
    }

    /// <summary>부모의 잔여 크기(보고된 자식 제외)가 임계값 이상이면 부모도 잔여 크기로 보고한다.</summary>
    [Fact]
    public void 부모_잔여_크기가_임계값_이상이면_함께_보고한다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\parent", GB + (GB / 5), extension: ".iso"),
            Dir(PROFILE + @"\parent\child", GB + (GB / 2), extension: ".mp4"),
        ]);

        Assert.Equal([PROFILE + @"\parent\child", PROFILE + @"\parent"], selection.Candidates.Select(c => c.Path));
        var parent = selection.Candidates[1];
        Assert.Equal(GB + (GB / 5), parent.Bytes);
        Assert.True(parent.ExcludesReportedDescendants);
        Assert.Equal([new ExtensionShare(".iso", GB + (GB / 5))], parent.TopExtensions);
    }

    /// <summary>임계값 미만 자식들은 부모에 합쳐져 부모가 후보가 된다.</summary>
    [Fact]
    public void 작은_자식들은_부모로_합쳐진다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\parent", 0),
            Dir(PROFILE + @"\parent\a", GB * 6 / 10),
            Dir(PROFILE + @"\parent\b", GB * 6 / 10),
        ]);

        var candidate = Assert.Single(selection.Candidates);
        Assert.Equal(PROFILE + @"\parent", candidate.Path);
        Assert.Equal(GB * 12 / 10, candidate.Bytes);
        Assert.Equal(2, candidate.FileCount);
    }

    /// <summary>크기가 같으면 경로의 서수(ordinal) 오름차순이다.</summary>
    [Fact]
    public void 동률은_경로_서수_오름차순이다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\beta", 2 * GB),
            Dir(PROFILE + @"\alpha", 2 * GB),
            Dir(PROFILE + @"\Zeta", 2 * GB),
            Dir(PROFILE + @"\big", 3 * GB),
        ]);

        // 대소문자 무시 정렬이면 alpha, beta, Zeta 순이지만 서수 정렬은 대문자가 먼저다.
        Assert.Equal(
            [PROFILE + @"\big", PROFILE + @"\Zeta", PROFILE + @"\alpha", PROFILE + @"\beta"],
            selection.Candidates.Select(c => c.Path));
    }

    /// <summary>후보가 20개를 넘으면 크기순 상위 20개만 남기고 전체 개수는 따로 알린다.</summary>
    [Fact]
    public void 상위_20개만_남긴다()
    {
        var directories = new List<DirectoryAggregate> { Dir(PROFILE, 0) };
        for (var index = 0; index < 25; index++)
        {
            directories.Add(Dir($@"{PROFILE}\d{index:00}", GB + (index * 1000)));
        }

        var selection = Select(directories);

        Assert.Equal(UnclassifiedFolderSelector.UNCLASSIFIED_TOP_COUNT, selection.Candidates.Count);
        Assert.Equal(25, selection.QualifyingCount);
        Assert.Equal(PROFILE + @"\d24", selection.Candidates[0].Path);
        Assert.Equal(PROFILE + @"\d05", selection.Candidates[^1].Path);
    }

    /// <summary>건너뛴 항목이 있는(완전 관측이 아닌) 폴더와 그 조상은 후보가 아니며 부분 관측 폴더로 따로 알린다. 완전 관측 자식은 후보가 된다.</summary>
    [Fact]
    public void 완전_관측이_아니면_후보에서_빼고_부분_관측으로_알린다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\games", GB / 10),
            Dir(PROFILE + @"\games\denied", 4 * GB, skipped: 1),
            Dir(PROFILE + @"\games\ok", 2 * GB),
        ]);

        var candidate = Assert.Single(selection.Candidates);
        Assert.Equal(PROFILE + @"\games\ok", candidate.Path);
        var partial = Assert.Single(selection.PartialFolders);
        Assert.Equal(new PartialFolder(PROFILE + @"\games\denied", 4 * GB), partial);
        Assert.Equal(1, selection.PartialCount);
    }

    /// <summary>자식에 건너뛴 항목이 있으면 부모도 완전 관측이 아니어서 후보가 아니다.</summary>
    [Fact]
    public void 하위에_건너뛴_항목이_있으면_부모도_후보가_아니다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 0),
            Dir(PROFILE + @"\parent", 3 * GB),
            Dir(PROFILE + @"\parent\small", GB / 10, skipped: 2),
        ]);

        Assert.Empty(selection.Candidates);
        Assert.Equal(PROFILE + @"\parent", Assert.Single(selection.PartialFolders).Path);
    }

    /// <summary>제외 위치(임시 폴더)와 보호 루트는 후보가 아니고, 크기가 부모에 합쳐지지 않으며, 그 안의 건너뜀은 부모를 부분 관측으로 만들지 않는다.</summary>
    [Fact]
    public void 제외와_보호_위치는_보고하지_않고_부모에_합치지_않는다()
    {
        var temp = PROFILE + @"\AppData\Local\Temp";
        var documents = PROFILE + @"\Documents";
        var selection = Select(
            [
                Dir(PROFILE, 0),
                Dir(PROFILE + @"\AppData", 0),
                Dir(PROFILE + @"\AppData\Local", GB / 10),
                Dir(temp, 5 * GB),
                Dir(temp + @"\locked", GB, skipped: 3),
                Dir(documents, 9 * GB),
            ],
            temp,
            documents);

        Assert.Empty(selection.Candidates);
        Assert.Empty(selection.PartialFolders);
    }

    /// <summary>스캔 루트 자체(프로필·ProgramData)는 후보가 아니고, 루트 밖 폴더는 무시한다.</summary>
    [Fact]
    public void 루트_자체와_루트_밖_폴더는_후보가_아니다()
    {
        var selection = Select(
        [
            Dir(PROFILE, 3 * GB),
            Dir(PROGRAM_DATA, 3 * GB),
            Dir(PROGRAM_DATA + @"\Vendor", 2 * GB),
            Dir(@"C:\Windows\Temp", 5 * GB),
            Dir(@"C:\Users\other\big", 5 * GB),
        ]);

        Assert.Equal(PROGRAM_DATA + @"\Vendor", Assert.Single(selection.Candidates).Path);
    }

    /// <summary>후보의 확장자는 크기순 상위 5개(동률은 확장자 서수)이고, 수정 시각은 가장 최근, 중복 가능 여부는 하나라도 있으면 참이다.</summary>
    [Fact]
    public void 확장자_분포와_수정_시각과_중복_가능을_집계한다()
    {
        var root = PROFILE + @"\media";
        var own = new DirectoryAggregate(
            root,
            GB,
            6,
            0,
            new Dictionary<string, long> { [".mp4"] = 400_000_000, [".mkv"] = 300_000_000, [".zip"] = 100_000_000, [".7z"] = 100_000_000, [""] = 50_000_000, [".txt"] = 50_000_000 },
            OLD,
            false);
        var child = Dir(root + @"\sub", GB / 2, extension: ".mp4", newest: NEW, duplicates: true);

        var selection = Select([Dir(PROFILE, 0), own, child]);

        var candidate = Assert.Single(selection.Candidates);
        Assert.Equal(root, candidate.Path);
        Assert.Equal(GB + (GB / 2), candidate.Bytes);
        Assert.Equal(7, candidate.FileCount);
        Assert.Equal(
            [
                new ExtensionShare(".mp4", 900_000_000),
                new ExtensionShare(".mkv", 300_000_000),
                new ExtensionShare(".7z", 100_000_000),
                new ExtensionShare(".zip", 100_000_000),
                new ExtensionShare(string.Empty, 50_000_000),
            ],
            candidate.TopExtensions);
        Assert.Equal(NEW, candidate.NewestWriteUtc);
        Assert.True(candidate.DuplicatesPossible);
    }

    /// <summary>경로 대소문자·끝 구분자가 달라도 같은 폴더로 이어 붙인다.</summary>
    [Fact]
    public void 경로는_대소문자와_끝_구분자를_무시하고_연결한다()
    {
        var selection = UnclassifiedFolderSelector.Select(
            [Dir(@"c:\users\TESTER\", 0), Dir(@"C:\Users\tester\Parent\", 0), Dir(@"C:\USERS\tester\parent\x", GB * 6 / 10), Dir(@"C:\Users\tester\PARENT\y", GB * 6 / 10)],
            [PROFILE + @"\"],
            []);

        Assert.Equal(@"C:\Users\tester\Parent", Assert.Single(selection.Candidates).Path);
    }
}
