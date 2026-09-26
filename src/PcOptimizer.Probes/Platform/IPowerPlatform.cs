/**
 * @file    : IPowerPlatform.cs
 * @author  : rudals252
 * @brief   : 전원 계획·전원 상태 조회 계약과 원시 전원 상태 레코드. 테스트에서 fixture 값으로 바꿔 끼운다
 */

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// Windows가 보고한 원시 전원 상태입니다(SYSTEM_POWER_STATUS의 조회 값).
/// </summary>
/// <param name="AcLineStatus">AC 전원 상태 원시 값.</param>
/// <param name="BatteryFlag">배터리 플래그 원시 값.</param>
/// <param name="BatteryLifePercent">배터리 잔량 원시 값.</param>
public sealed record PowerStatusReading(byte AcLineStatus, byte BatteryFlag, byte BatteryLifePercent);

/// <summary>
/// 전원 계획·전원 상태 조회 계약입니다. 모든 메서드는 예외 대신 Win32 오류 코드를 돌려줍니다.
/// </summary>
public interface IPowerPlatform
{
    /// <summary>
    /// 활성 전원 계획 GUID를 읽습니다.
    /// </summary>
    /// <param name="schemeGuid">읽은 GUID.</param>
    /// <returns>Win32 오류 코드(0이면 성공).</returns>
    uint GetActiveScheme(out Guid schemeGuid);

    /// <summary>
    /// 전원 계획의 표시 이름을 읽습니다.
    /// </summary>
    /// <param name="schemeGuid">계획 GUID.</param>
    /// <param name="friendlyName">읽은 이름(실패하면 null).</param>
    /// <returns>Win32 오류 코드(0이면 성공).</returns>
    uint ReadFriendlyName(Guid schemeGuid, out string? friendlyName);

    /// <summary>
    /// AC/배터리 전원 상태를 읽습니다.
    /// </summary>
    /// <param name="reading">읽은 상태(실패하면 null).</param>
    /// <returns>Win32 오류 코드(0이면 성공).</returns>
    uint GetPowerStatus(out PowerStatusReading? reading);
}
