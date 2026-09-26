/**
 * @file    : FakePowerPlatform.cs
 * @author  : rudals252
 * @brief   : 활성 계획·계획 이름·전원 상태를 고정 값 또는 Win32 오류 코드로 돌려주는 테스트용 전원 API
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 고정 값을 돌려주는 가짜 전원 API입니다. 오류 코드가 0이 아니면 해당 조회가 실패합니다.
/// </summary>
internal sealed class FakePowerPlatform : IPowerPlatform
{
    /// <summary>테스트용 Win32 오류 코드(ERROR_FILE_NOT_FOUND).</summary>
    public const uint SAMPLE_ERROR = 2;

    /// <summary>돌려줄 활성 계획 GUID.</summary>
    public Guid SchemeGuid { get; init; } = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");

    /// <summary>활성 계획 조회 오류 코드.</summary>
    public uint SchemeError { get; init; }

    /// <summary>돌려줄 계획 이름.</summary>
    public string? FriendlyName { get; init; } = "fixture plan";

    /// <summary>계획 이름 조회 오류 코드.</summary>
    public uint FriendlyNameError { get; init; }

    /// <summary>돌려줄 전원 상태.</summary>
    public PowerStatusReading? Status { get; init; } = new(AcLineStatus: 1, BatteryFlag: 128, BatteryLifePercent: 255);

    /// <summary>전원 상태 조회 오류 코드.</summary>
    public uint StatusError { get; init; }

    /// <inheritdoc />
    public uint GetActiveScheme(out Guid schemeGuid)
    {
        schemeGuid = SchemeError == 0 ? SchemeGuid : Guid.Empty;
        return SchemeError;
    }

    /// <inheritdoc />
    public uint ReadFriendlyName(Guid schemeGuid, out string? friendlyName)
    {
        friendlyName = FriendlyNameError == 0 ? FriendlyName : null;
        return FriendlyNameError;
    }

    /// <inheritdoc />
    public uint GetPowerStatus(out PowerStatusReading? reading)
    {
        reading = StatusError == 0 ? Status : null;
        return StatusError;
    }
}
