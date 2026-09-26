/**
 * @file    : WuaSearchGateway.cs
 * @author  : rudals252
 * @brief   : Windows Update Agent COM을 늦은 바인딩(ProgID + dynamic, 상호 운용 패키지 없음)으로 전용 STA 스레드에서 비동기 검색(BeginSearch/EndSearch)하고 취소 시 RequestAbort하는 검색 전용 게이트웨이(서비스·정책 설정 불변, 다운로드·설치 없음)
 */

// 기본 패키지
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.CSharp.RuntimeBinder;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// WUA 검색 게이트웨이입니다(스펙 §4·§5, WUA 비동기 검색/취소 API 사용).
/// <list type="bullet">
/// <item><c>Microsoft.Update.Session</c>의 <c>CreateUpdateSearcher()</c>를 그대로 쓰며 ServerSelection·ServiceID·Online을 바꾸지 않습니다(WSUS·정책 존중).</item>
/// <item>COM 작업은 전용 STA 스레드에서 하고 결과만 작업(Task)으로 넘깁니다. 완료는 ISearchJob.IsCompleted를 짧은 간격으로 확인합니다.</item>
/// <item>취소 토큰이 신호되면 <c>ISearchJob.RequestAbort()</c>를 호출하고 짧게 기다린 뒤 취소로 끝냅니다.</item>
/// <item>COM 실패는 HRESULT로, 필요한 멤버가 없으면 요약 코드로 돌려주며 예외 원문을 남기지 않습니다. 업데이트의 드라이버 세부 속성을 못 읽으면 그 항목만 표시합니다.</item>
/// </list>
/// 다운로드·설치 API(CreateUpdateDownloader/CreateUpdateInstaller 등)는 호출하지 않습니다.
/// </summary>
public sealed class WuaSearchGateway : IUpdateSearchGateway
{
    /// <summary>WUA 세션 ProgID.</summary>
    public const string SESSION_PROG_ID = "Microsoft.Update.Session";

    /// <summary>WUA 시스템 정보 ProgID(재부팅 필요 여부).</summary>
    public const string SYSTEM_INFO_PROG_ID = "Microsoft.Update.SystemInfo";

    /// <summary>세션을 만들 수 없음 요약 코드.</summary>
    public const string ERROR_SESSION_UNAVAILABLE = "wua.sessionUnavailable";

    /// <summary>필요한 COM 멤버가 없음 요약 코드.</summary>
    public const string ERROR_MEMBER_MISSING = "wua.memberMissing";

    /// <summary>예상과 다른 값 형식 요약 코드.</summary>
    public const string ERROR_UNEXPECTED_TYPE = "wua.unexpectedType";

    /// <summary>완료 확인 간격.</summary>
    public static readonly TimeSpan DEFAULT_POLL_INTERVAL = TimeSpan.FromMilliseconds(250);

    /// <summary>중단 요청 뒤 검색이 끝나기를 기다리는 최대 시간.</summary>
    public static readonly TimeSpan DEFAULT_ABORT_WAIT = TimeSpan.FromSeconds(10);

    private const string THREAD_NAME = "PcOptimizer WUA search";

    private readonly Func<object?> _sessionFactory;
    private readonly Func<object?> _systemInfoFactory;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _abortWait;

    /// <summary>
    /// 실제 WUA COM 객체를 쓰는 게이트웨이를 만듭니다.
    /// </summary>
    public WuaSearchGateway()
        : this(() => CreateComObject(SESSION_PROG_ID), () => CreateComObject(SYSTEM_INFO_PROG_ID), DEFAULT_POLL_INTERVAL, DEFAULT_ABORT_WAIT)
    {
    }

    /// <summary>
    /// 세션·시스템 정보 객체 생성기를 지정해 게이트웨이를 만듭니다(테스트에서 COM 대신 늦은 바인딩 가능한 가짜 객체 사용).
    /// </summary>
    /// <param name="sessionFactory">세션 생성기(없으면 null 반환).</param>
    /// <param name="systemInfoFactory">시스템 정보 생성기(없으면 null 반환).</param>
    /// <param name="pollInterval">완료 확인 간격.</param>
    /// <param name="abortWait">중단 요청 뒤 기다리는 최대 시간.</param>
    public WuaSearchGateway(Func<object?> sessionFactory, Func<object?> systemInfoFactory, TimeSpan pollInterval, TimeSpan abortWait)
    {
        ArgumentNullException.ThrowIfNull(sessionFactory);
        ArgumentNullException.ThrowIfNull(systemInfoFactory);
        _sessionFactory = sessionFactory;
        _systemInfoFactory = systemInfoFactory;
        _pollInterval = pollInterval;
        _abortWait = abortWait;
    }

    /// <inheritdoc />
    public Task<WuaSearchOutcome> SearchAsync(string criteria, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ct.ThrowIfCancellationRequested();

        var completion = new TaskCompletionSource<WuaSearchOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(RunSearch(criteria, ct));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(ct);
            }
            catch (Exception ex)
            {
                // 예상하지 못한 예외는 작업으로 넘겨 실행기가 형식 이름만 기록하게 한다.
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = THREAD_NAME,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// ProgID로 COM 객체를 만든다(등록되지 않았으면 null).
    /// </summary>
    private static object? CreateComObject(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: false);
        return type is null ? null : Activator.CreateInstance(type);
    }

    /// <summary>
    /// STA 스레드에서 검색을 한 번 실행한다. 취소되면 중단을 요청하고 <see cref="OperationCanceledException"/>을 던진다.
    /// </summary>
    private WuaSearchOutcome RunSearch(string criteria, CancellationToken ct)
    {
        object? session = null;
        object? searcher = null;
        object? job = null;
        try
        {
            session = _sessionFactory();
            if (session is null)
            {
                return Error(ERROR_SESSION_UNAVAILABLE);
            }

            searcher = ((dynamic)session).CreateUpdateSearcher();
            job = ((dynamic)searcher!).BeginSearch(criteria, new UnknownWrapper(new WuaSearchCallback()), null);
            if (!WaitForCompletion(job!, ct))
            {
                AbortAndWait(searcher!, job!);
                ct.ThrowIfCancellationRequested();
            }

            dynamic result = ((dynamic)searcher!).EndSearch((dynamic)job!);
            int resultCode = (int)result.ResultCode;
            var updates = ReadUpdates(result.Updates);
            return new WuaSearchOutcome(resultCode, updates, ReadRebootRequired(), null, null);
        }
        catch (COMException ex)
        {
            return new WuaSearchOutcome(null, [], null, ex.HResult, null);
        }
        catch (RuntimeBinderException)
        {
            return Error(ERROR_MEMBER_MISSING);
        }
        catch (InvalidCastException)
        {
            return Error(ERROR_UNEXPECTED_TYPE);
        }
        finally
        {
            Release(job);
            Release(searcher);
            Release(session);
        }
    }

    /// <summary>
    /// 작업이 끝날 때까지 확인한다. 끝나면 true, 먼저 취소되면 false. 대기(WaitOne)는 STA에서 COM 메시지를 처리한다.
    /// </summary>
    private bool WaitForCompletion(object job, CancellationToken ct)
    {
        while (!IsCompleted(job))
        {
            if (ct.WaitHandle.WaitOne(_pollInterval))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 중단을 요청하고 정해진 시간 안에 끝나면 EndSearch로 작업을 마무리한다(결과는 쓰지 않음).
    /// </summary>
    private void AbortAndWait(object searcher, object job)
    {
        try
        {
            ((dynamic)job).RequestAbort();
            var deadline = DateTime.UtcNow + _abortWait;
            while (!IsCompleted(job) && DateTime.UtcNow < deadline)
            {
                // STA에서 COM 메시지를 처리하며 기다린다(Thread.Sleep은 메시지를 처리하지 않음).
                Thread.CurrentThread.Join(_pollInterval);
            }

            if (IsCompleted(job))
            {
                _ = ((dynamic)searcher).EndSearch((dynamic)job);
            }
        }
        catch (Exception ex) when (ex is COMException or RuntimeBinderException or InvalidCastException)
        {
            // 중단 과정의 실패는 취소 결과를 바꾸지 않는다.
        }
    }

    /// <summary>
    /// 작업 완료 여부를 읽는다.
    /// </summary>
    private static bool IsCompleted(object job)
    {
        return (bool)((dynamic)job).IsCompleted;
    }

    /// <summary>
    /// 업데이트 목록을 읽는다. 드라이버 세부 속성을 못 읽은 항목은 DetailsAvailable=false로 남긴다.
    /// </summary>
    private static List<WuaDriverUpdate> ReadUpdates(dynamic updates)
    {
        var list = new List<WuaDriverUpdate>();
        int count = (int)updates.Count;
        for (var index = 0; index < count; index++)
        {
            dynamic update = updates.Item(index);
            try
            {
                list.Add(ReadUpdate(update));
            }
            finally
            {
                Release((object?)update);
            }
        }

        return list;
    }

    /// <summary>
    /// 업데이트 하나를 읽는다(각 속성 읽기를 따로 보호).
    /// </summary>
    private static WuaDriverUpdate ReadUpdate(dynamic update)
    {
        var detailsAvailable = true;
        string? Text(Func<object?> read)
        {
            var value = TryRead(read, out var ok);
            detailsAvailable &= ok;
            return value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        var title = TryRead(() => update.Title, out _) as string;
        var model = Text(() => update.DriverModel);
        var manufacturer = Text(() => update.DriverManufacturer);
        var driverClass = Text(() => update.DriverClass);
        var dateValue = TryRead(() => update.DriverVerDate, out var dateOk);
        detailsAvailable &= dateOk;
        var downloaded = TryRead(() => update.IsDownloaded, out _) as bool?;
        return new WuaDriverUpdate(title, model, manufacturer, dateValue as DateTime?, driverClass, ReadKbIds(update), downloaded, detailsAvailable);
    }

    /// <summary>
    /// KB 문서 ID 목록을 읽는다(못 읽으면 빈 목록).
    /// </summary>
    private static List<string> ReadKbIds(dynamic update)
    {
        var ids = new List<string>();
        var collection = TryRead(() => update.KBArticleIDs, out var ok);
        if (!ok || collection is null)
        {
            return ids;
        }

        try
        {
            dynamic kb = collection;
            int count = (int)kb.Count;
            for (var index = 0; index < count; index++)
            {
                if (kb.Item(index) is string id)
                {
                    ids.Add(id);
                }
            }
        }
        catch (Exception ex) when (ex is COMException or RuntimeBinderException or InvalidCastException)
        {
            // KB 목록을 끝까지 못 읽으면 읽은 것까지만 쓴다.
        }
        finally
        {
            Release(collection);
        }

        return ids;
    }

    /// <summary>
    /// 재부팅 필요 여부를 읽는다(못 읽으면 null).
    /// </summary>
    private bool? ReadRebootRequired()
    {
        object? info = null;
        try
        {
            info = _systemInfoFactory();
            return info is null ? null : (bool)((dynamic)info).RebootRequired;
        }
        catch (Exception ex) when (ex is COMException or RuntimeBinderException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            Release(info);
        }
    }

    /// <summary>
    /// 속성 하나를 읽는다. 멤버가 없거나 COM 오류면 null과 false.
    /// </summary>
    private static object? TryRead(Func<object?> read, out bool ok)
    {
        try
        {
            ok = true;
            return read();
        }
        catch (Exception ex) when (ex is COMException or RuntimeBinderException or InvalidCastException)
        {
            ok = false;
            return null;
        }
    }

    /// <summary>
    /// COM 객체면 참조를 해제한다.
    /// </summary>
    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    /// <summary>
    /// 요약 코드 실패 결과를 만든다.
    /// </summary>
    private static WuaSearchOutcome Error(string code)
    {
        return new WuaSearchOutcome(null, [], null, null, code);
    }
}
