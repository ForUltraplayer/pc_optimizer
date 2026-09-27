/**
 * @file    : DesktopShellConnection.cs
 * @author  : rudals252
 * @brief   : 데스크톱 셸 COM 수명과 세션·토큰 승격 여부를 확인하는 Windows 어댑터(UAC가 꺼진 PC의 승격된 셸도 세션 셸로 인정)
 */
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PcOptimizer.App.Services;

/// <summary>기존 Explorer 데스크톱에 연결합니다. URL 직접 실행 경로는 없습니다.</summary>
internal sealed class DesktopShellConnection : IDesktopShell
{
    private const int DESKTOP_CLASS = 8;
    private const int NEED_DISPATCH = 1;
    private const uint BACKGROUND_VIEW = 0;
    private const uint TOKEN_QUERY = 8;
    private const int TOKEN_ELEVATION = 20;
    private const int SHOW_NORMAL = 1;
    private static readonly Guid SHELL_WINDOWS = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid TOP_LEVEL_BROWSER = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private static readonly Guid DISPATCH = new("00020400-0000-0000-C000-000000000046");
    private readonly List<object> _objects = [];
    private readonly nint _window;
    private readonly Process _process;
    private object? _application;
    private bool _disposed;

    private DesktopShellConnection(nint window, Process process) { _window = window; _process = process; }

    /// <summary>셸 창이 없으면 null, 연결 오류는 호출자가 실패 안내로 처리합니다.</summary>
    internal static IDesktopShell? Connect()
    {
        var window = GetShellWindow();
        if (window == 0 || GetWindowThreadProcessId(window, out var pid) == 0) { return null; }
        var connection = new DesktopShellConnection(window, Process.GetProcessById(checked((int)pid)));
        try
        {
            // UAC를 끈 PC(EnableLUA=0)는 셸 자체가 관리자 토큰이라 IsUnelevated가 false다. 그래도 사용자의 평소 권한이 그 셸이므로 연결은 유지하고,
            // 승격 여부는 호출자에게 알려 기록만 한다. 앱이 새 Explorer/브라우저를 관리자 토큰으로 직접 시작하는 경로는 여전히 없다.
            if (!connection.IsSessionShell) { connection.Dispose(); return null; }
            connection.Initialize();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    /// <inheritdoc />
    public bool IsSessionShell
    {
        get
        {
            if (_disposed || _process.HasExited || GetShellWindow() != _window
                || GetWindowThreadProcessId(_window, out var pid) == 0 || pid != _process.Id) { return false; }
            using var current = Process.GetCurrentProcess();
            return _process.SessionId == current.SessionId;
        }
    }

    /// <inheritdoc />
    public bool IsUnelevated
    {
        get
        {
            if (!IsSessionShell || !OpenProcessToken(_process.Handle, TOKEN_QUERY, out var token)) { return false; }
            using (token)
            {
                return GetTokenInformation(token, TOKEN_ELEVATION, out var elevation, sizeof(int), out _) && elevation == 0;
            }
        }
    }

    private T Retain<T>(T value) where T : class
    {
        if (!_objects.Any(item => ReferenceEquals(item, value))) { _objects.Add(value); }
        return value;
    }

    private void Initialize()
    {
        // 공식 데스크톱 자동화 경로. 새 Shell.Application 객체의 로컬 실행으로 대체하지 않는다.
        // https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643
        dynamic windows = Retain(Activator.CreateInstance(Type.GetTypeFromCLSID(SHELL_WINDOWS, throwOnError: true)!)!);
        object location = 0;
        object root = null!;
        int foundWindow;
        object desktop = windows.FindWindowSW(ref location, ref root, DESKTOP_CLASS, out foundWindow, NEED_DISPATCH);
        Retain(desktop);
        if (unchecked((uint)foundWindow) != unchecked((uint)_window.ToInt64())) { throw new InvalidOperationException("데스크톱 셸이 변경됐습니다."); }
        var service = TOP_LEVEL_BROWSER;
        var browserId = typeof(IShellBrowser).GUID;
        var browser = (IShellBrowser)Retain(((IComServiceProvider)desktop).QueryService(ref service, ref browserId));
        var view = Retain(browser.QueryActiveShellView());
        var dispatchId = DISPATCH;
        dynamic folder = Retain(view.GetItemObject(BACKGROUND_VIEW, ref dispatchId));
        _application = Retain((object)folder.Application);
    }

    /// <inheritdoc />
    public void Open(string absoluteUri)
    {
        if (!IsSessionShell || _application is null) { throw new InvalidOperationException("데스크톱 셸 연결이 유효하지 않습니다."); }
        // COM 서버가 종료되면 실패한다. 관리자 토큰으로 Explorer/브라우저를 시작하지 않는다.
        ((dynamic)_application).ShellExecute(absoluteUri, string.Empty, string.Empty, "open", SHOW_NORMAL);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;
        for (var i = _objects.Count - 1; i >= 0; i--)
        {
            if (Marshal.IsComObject(_objects[i])) { Marshal.ReleaseComObject(_objects[i]); }
        }
        _objects.Clear();
        _application = null;
        _process.Dispose();
    }

    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, out int information, int length, out int returned);

    // Windows SDK의 IUnknown 기반 vtable 순서. 사용하지 않는 슬롯은 호출하지 않는다.
    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IComServiceProvider
    {
        [return: MarshalAs(UnmanagedType.Interface)] object QueryService(ref Guid service, ref Guid interfaceId);
    }
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow(); void ContextSensitiveHelp(); void InsertMenus(); void SetMenu(); void RemoveMenus(); void SetStatusText();
        void EnableModeless(); void TranslateAccelerator(); void BrowseObject(); void GetViewStateStream(); void GetControlWindow(); void SendControlMsg();
        IShellView QueryActiveShellView();
    }
    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellView
    {
        void GetWindow(); void ContextSensitiveHelp(); void TranslateAccelerator(); void EnableModeless(); void UIActivate(); void Refresh();
        void CreateViewWindow(); void DestroyViewWindow(); void GetCurrentInfo(); void AddPropertySheetPages(); void SaveViewState(); void SelectItem();
        [return: MarshalAs(UnmanagedType.IDispatch)] object GetItemObject(uint aspect, ref Guid interfaceId);
    }
}
