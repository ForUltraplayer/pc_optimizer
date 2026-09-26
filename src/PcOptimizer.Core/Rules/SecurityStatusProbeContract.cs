/**
 * @file    : SecurityStatusProbeContract.cs
 * @author  : rudals252
 * @brief   : 보안 상태 프로브와 규칙이 공유하는 프로브 ID·Win32_DeviceGuard 측정 이름(VBS 상태, 설정/실행 중 보안 서비스)과 원시 값 의미 계약 상수
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 보안 상태 프로브(Probes)와 <see cref="SecurityStatusRule"/>(Core)가 공유하는 측정값 계약입니다.
/// VBS 상태와 메모리 무결성(HVCI) 상태는 서로 다른 필드에서 오며 하나의 값으로 합치지 않습니다.
/// </summary>
public static class SecurityStatusProbeContract
{
    /// <summary>보안 상태 프로브 ID.</summary>
    public const string PROBE_ID = "hardware.securityStatus";

    /// <summary>VBS 상태 원시 값(VirtualizationBasedSecurityStatus, 정수).</summary>
    public const string VBS_STATUS = "security.vbsStatus";

    /// <summary>설정된 보안 서비스 코드 목록(SecurityServicesConfigured, 숫자 문자열 목록).</summary>
    public const string SERVICES_CONFIGURED = "security.securityServicesConfigured";

    /// <summary>실행 중인 보안 서비스 코드 목록(SecurityServicesRunning, 숫자 문자열 목록).</summary>
    public const string SERVICES_RUNNING = "security.securityServicesRunning";

    /// <summary>사용 가능한 보안 속성 코드 목록(AvailableSecurityProperties, 숫자 문자열 목록).</summary>
    public const string AVAILABLE_PROPERTIES = "security.availableSecurityProperties";

    /// <summary>VBS 상태: 사용 안 함.</summary>
    public const long VBS_STATUS_NOT_ENABLED = 0;

    /// <summary>VBS 상태: 사용하도록 설정했지만 실행 중이 아님.</summary>
    public const long VBS_STATUS_ENABLED_NOT_RUNNING = 1;

    /// <summary>VBS 상태: 실행 중.</summary>
    public const long VBS_STATUS_RUNNING = 2;

    /// <summary>보안 서비스 코드: 하이퍼바이저 적용 코드 무결성(HVCI, 메모리 무결성).</summary>
    public const string SERVICE_HVCI = "2";
}
