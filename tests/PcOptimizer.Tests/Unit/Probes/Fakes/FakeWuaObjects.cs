/**
 * @file    : FakeWuaObjects.cs
 * @author  : rudals252
 * @brief   : WUA COM 객체(세션·검색기·검색 작업·결과·업데이트·문자열 모음·시스템 정보)를 늦은 바인딩(dynamic)으로 흉내 내는 공개 가짜 객체와 가짜 검색 게이트웨이(실제 Windows Update 호출 없음)
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 WUA 세션입니다. 게이트웨이의 dynamic 호출이 Probes 어셈블리에서 이 형식에 접근하므로 public입니다.
/// </summary>
public sealed class FakeUpdateSession(FakeUpdateSearcher searcher)
{
    /// <summary>검색기를 만든 횟수.</summary>
    public int SearcherCreated { get; private set; }

    /// <summary>
    /// 검색기를 만듭니다.
    /// </summary>
    public FakeUpdateSearcher CreateUpdateSearcher()
    {
        SearcherCreated++;
        return searcher;
    }
}

/// <summary>
/// 가짜 검색기입니다. ServerSelection 등 설정 속성을 바꾸면 기록합니다.
/// </summary>
public sealed class FakeUpdateSearcher(FakeSearchJob job, Func<FakeSearchResult> endSearch)
{
    private int _serverSelection;

    /// <summary>받은 검색 조건.</summary>
    public string? Criteria { get; private set; }

    /// <summary>받은 콜백 인자.</summary>
    public object? Callback { get; private set; }

    /// <summary>EndSearch 호출 횟수.</summary>
    public int EndSearchCalls { get; private set; }

    /// <summary>설정 속성을 바꾼 횟수(0이어야 함).</summary>
    public int SettingChanges { get; private set; }

    /// <summary>서버 선택(바꾸면 기록).</summary>
    public int ServerSelection
    {
        get => _serverSelection;
        set
        {
            SettingChanges++;
            _serverSelection = value;
        }
    }

    /// <summary>
    /// 비동기 검색을 시작합니다.
    /// </summary>
    public FakeSearchJob BeginSearch(string criteria, object callback, object? state)
    {
        Criteria = criteria;
        Callback = callback;
        return job;
    }

    /// <summary>
    /// 검색을 마칩니다.
    /// </summary>
    public FakeSearchResult EndSearch(FakeSearchJob searchJob)
    {
        EndSearchCalls++;
        return endSearch();
    }
}

/// <summary>
/// 가짜 검색 작업입니다. 처음부터 끝났거나, 중단 요청 뒤에 끝나거나, 끝나지 않습니다.
/// </summary>
public sealed class FakeSearchJob(bool completedAtStart, bool completesOnAbort = true)
{
    private volatile bool _completed = completedAtStart;

    /// <summary>중단 요청 횟수.</summary>
    public int AbortRequests { get; private set; }

    /// <summary>완료 여부.</summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// 중단을 요청합니다.
    /// </summary>
    public void RequestAbort()
    {
        AbortRequests++;
        if (completesOnAbort)
        {
            _completed = true;
        }
    }
}

/// <summary>
/// 가짜 검색 결과입니다.
/// </summary>
public sealed class FakeSearchResult(int resultCode, params object[] updates)
{
    /// <summary>결과 코드.</summary>
    public int ResultCode => resultCode;

    /// <summary>업데이트 모음.</summary>
    public FakeCollection Updates { get; } = new(updates);
}

/// <summary>
/// 가짜 COM 모음(Count, Item(i))입니다.
/// </summary>
public sealed class FakeCollection(object[] items)
{
    /// <summary>항목 수.</summary>
    public int Count => items.Length;

    /// <summary>
    /// 항목을 돌려줍니다.
    /// </summary>
    public object Item(int index) => items[index];
}

/// <summary>
/// 드라이버 세부 속성까지 있는 가짜 드라이버 업데이트입니다.
/// </summary>
public sealed class FakeDriverUpdate
{
    /// <summary>제목.</summary>
    public string Title { get; init; } = "테스트 제조사 - Display - 1.2.3.4";

    /// <summary>드라이버 모델.</summary>
    public string DriverModel { get; init; } = "테스트 모델";

    /// <summary>드라이버 제조사.</summary>
    public string DriverManufacturer { get; init; } = "테스트 제조사";

    /// <summary>드라이버 날짜.</summary>
    public DateTime DriverVerDate { get; init; } = new(2026, 3, 4);

    /// <summary>드라이버 클래스.</summary>
    public string DriverClass { get; init; } = "Display";

    /// <summary>이미 내려받았는지.</summary>
    public bool IsDownloaded { get; init; }

    /// <summary>KB 문서 ID.</summary>
    public FakeCollection KBArticleIDs { get; init; } = new(["5000001"]);
}

/// <summary>
/// 드라이버 세부 속성(IWindowsDriverUpdate)이 없는 가짜 업데이트입니다(멤버 없음 경로).
/// </summary>
public sealed class FakePlainUpdate
{
    /// <summary>제목.</summary>
    public string Title { get; init; } = "세부 정보 없는 업데이트";

    /// <summary>이미 내려받았는지.</summary>
    public bool IsDownloaded { get; init; }
}

/// <summary>
/// 가짜 WUA 시스템 정보입니다.
/// </summary>
public sealed class FakeSystemInfo(bool rebootRequired)
{
    /// <summary>재부팅 필요 여부.</summary>
    public bool RebootRequired => rebootRequired;
}

/// <summary>
/// 정한 결과를 돌려주거나 취소를 기다리는 가짜 검색 게이트웨이입니다(프로브 테스트용).
/// </summary>
internal sealed class FakeUpdateSearchGateway(Func<CancellationToken, Task<WuaSearchOutcome>> search) : IUpdateSearchGateway
{
    /// <summary>검색 호출 횟수.</summary>
    public int Calls { get; private set; }

    /// <summary>받은 검색 조건.</summary>
    public string? Criteria { get; private set; }

    /// <summary>
    /// 결과를 바로 돌려주는 게이트웨이를 만든다.
    /// </summary>
    public static FakeUpdateSearchGateway Returning(WuaSearchOutcome outcome) => new(_ => Task.FromResult(outcome));

    /// <inheritdoc />
    public Task<WuaSearchOutcome> SearchAsync(string criteria, CancellationToken ct)
    {
        Calls++;
        Criteria = criteria;
        return search(ct);
    }
}
