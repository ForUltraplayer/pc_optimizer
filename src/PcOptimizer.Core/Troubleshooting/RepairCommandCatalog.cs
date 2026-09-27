/**
 * @file    : RepairCommandCatalog.cs
 * @author  : rudals252
 * @brief   : 직접 실행 명령(System32 고정 실행 파일·고정 인자)과 내장 도구 열기 대상의 닫힌 목록. 사용자 입력이나 JSON은 명령을 추가할 수 없다
 */
namespace PcOptimizer.Core.Troubleshooting;

/// <summary>직접 실행 명령 한 항목입니다. 실행 파일 이름은 System32 아래 파일 이름만 두고 절대 경로는 실행기가 붙입니다.</summary>
/// <param name="Id">명령 ID(카탈로그 JSON의 command 값).</param>
/// <param name="Executable">System32 안의 실행 파일 이름.</param>
/// <param name="Arguments">고정 인자. <see cref="SYSTEM_DRIVE_TOKEN"/>은 실행기가 시스템 드라이브 문자(예: C:)로 바꿉니다.</param>
/// <param name="Timeout">실행 시간 상한.</param>
/// <param name="RebootRequired">완료 뒤 다시 시작이 필요한지.</param>
/// <param name="SuccessExitCodes">성공으로 보는 종료 코드.</param>
public sealed record RepairCommand(string Id, string Executable, IReadOnlyList<string> Arguments, TimeSpan Timeout, bool RebootRequired, IReadOnlyList<int> SuccessExitCodes);

/// <summary>Windows 내장 명령의 닫힌 목록입니다.</summary>
public static class RepairCommandCatalog
{
    /// <summary>인자 안에서 시스템 드라이브(예: C:)로 치환되는 토큰입니다.</summary>
    public const string SYSTEM_DRIVE_TOKEN = "{SystemDrive}";
    /// <summary>시스템 복원 지점 생성은 프로세스가 아니라 WMI 호출로 처리하는 특수 명령입니다.</summary>
    public const string RESTORE_POINT = "restore-point";
    /// <summary>DISM 구성 요소 저장소 복구.</summary>
    public const string DISM_RESTORE_HEALTH = "dism-restorehealth";
    /// <summary>시스템 파일 검사.</summary>
    public const string SFC_SCANNOW = "sfc-scannow";
    /// <summary>시스템 드라이브 온라인 검사(고치지 않음).</summary>
    public const string CHKDSK_SCAN = "chkdsk-scan";
    /// <summary>다음 부팅 때 시스템 드라이브 검사 예약(chkntfs /C).</summary>
    public const string CHKDSK_SCHEDULE = "chkdsk-schedule";
    /// <summary>DNS 캐시 비우기.</summary>
    public const string FLUSH_DNS = "flushdns";
    /// <summary>Winsock 카탈로그 초기화.</summary>
    public const string WINSOCK_RESET = "winsock-reset";

    private static readonly TimeSpan LONG_TIMEOUT = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan MEDIUM_TIMEOUT = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan SHORT_TIMEOUT = TimeSpan.FromMinutes(2);
    private static readonly int[] ZERO = [0];

    /// <summary>등록된 명령(ID 순서는 표시 순서가 아님).</summary>
    public static readonly IReadOnlyList<RepairCommand> Commands =
    [
        new(DISM_RESTORE_HEALTH, "dism.exe", ["/Online", "/Cleanup-Image", "/RestoreHealth"], LONG_TIMEOUT, false, ZERO),
        new(SFC_SCANNOW, "sfc.exe", ["/scannow"], MEDIUM_TIMEOUT, false, ZERO),
        new(CHKDSK_SCAN, "chkdsk.exe", [SYSTEM_DRIVE_TOKEN, "/scan"], MEDIUM_TIMEOUT, false, ZERO),
        new(CHKDSK_SCHEDULE, "chkntfs.exe", ["/C", SYSTEM_DRIVE_TOKEN], SHORT_TIMEOUT, true, ZERO),
        new(FLUSH_DNS, "ipconfig.exe", ["/flushdns"], SHORT_TIMEOUT, false, ZERO),
        new(WINSOCK_RESET, "netsh.exe", ["winsock", "reset"], SHORT_TIMEOUT, true, ZERO),
    ];

    /// <summary>ID로 명령을 찾습니다. 복원 지점 ID는 프로세스 명령이 아니므로 null입니다.</summary>
    public static RepairCommand? Find(string? id) => id is null ? null : Commands.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal));

    /// <summary>카탈로그 JSON이 참조할 수 있는 명령 ID인지 확인합니다.</summary>
    public static bool IsKnown(string? id) => id == RESTORE_POINT || Find(id) is not null;
}

/// <summary>내장 도구 열기 대상 한 항목입니다.</summary>
/// <param name="Id">대상 ID(카탈로그 JSON의 openTarget 값).</param>
/// <param name="Executable">System32 안의 실행 파일 이름. 설정 URI면 null.</param>
/// <param name="Arguments">고정 인자.</param>
/// <param name="SettingsUri">ms-settings URI. 실행 파일이면 null.</param>
public sealed record BuiltInTool(string Id, string? Executable, IReadOnlyList<string> Arguments, string? SettingsUri);

/// <summary>Windows 내장 GUI 도구·설정 화면의 닫힌 목록입니다.</summary>
public static class BuiltInToolCatalog
{
    /// <summary>Windows 메모리 진단.</summary>
    public const string MDSCHED = "mdsched";
    /// <summary>디스크 관리(mmc).</summary>
    public const string DISK_MANAGEMENT = "diskmgmt";
    /// <summary>이벤트 뷰어(mmc).</summary>
    public const string EVENT_VIEWER = "eventvwr";
    /// <summary>신뢰성 모니터.</summary>
    public const string RELIABILITY = "reliability";
    /// <summary>설정 → 문제 해결.</summary>
    public const string NETWORK_TROUBLESHOOTER = "network-troubleshooter";
    /// <summary>설정 → 문제 해결 URI.</summary>
    public const string TROUBLESHOOT_SETTINGS_URI = "ms-settings:troubleshoot";

    /// <summary>등록된 대상.</summary>
    public static readonly IReadOnlyList<BuiltInTool> Tools =
    [
        new(MDSCHED, "MdSched.exe", [], null),
        new(DISK_MANAGEMENT, "mmc.exe", ["diskmgmt.msc"], null),
        new(EVENT_VIEWER, "mmc.exe", ["eventvwr.msc"], null),
        new(RELIABILITY, "perfmon.exe", ["/rel"], null),
        new(NETWORK_TROUBLESHOOTER, null, [], TROUBLESHOOT_SETTINGS_URI),
    ];

    /// <summary>ID로 대상을 찾습니다.</summary>
    public static BuiltInTool? Find(string? id) => id is null ? null : Tools.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
}
