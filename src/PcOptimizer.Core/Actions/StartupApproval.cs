/**
 * @file    : StartupApproval.cs
 * @author  : rudals252
 * @brief   : 작업 관리자 시작 앱 토글이 쓰는 StartupApproved 12바이트 값의 해석·검증·생성(첫 DWORD 0x02 사용, 0x03 사용 안 함 + FILETIME)
 */
using System.Buffers.Binary;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Core.Actions;

/// <summary>
/// StartupApproved 값 형식은 Microsoft 공식 문서가 없습니다. 이 앱은 Windows 11 24H2/26200에서 관측한 형식(모든 값 12바이트, 활성은 0x02+0 8바이트,
/// 비활성은 0x03+비활성화 시각 FILETIME)만 인정하고, 그 밖의 길이·형식은 변경 대상에서 제외합니다.
/// </summary>
public static class StartupApproval
{
    /// <summary>관측된 값 길이(바이트).</summary>
    public const int VALUE_LENGTH = 12;
    /// <summary>레지스트리 REG_BINARY 형식 코드.</summary>
    public const int REG_BINARY = 3;
    /// <summary>첫 DWORD: 작업 관리자 "사용".</summary>
    public const uint ENABLED_MARKER = 0x02;
    /// <summary>첫 DWORD: 작업 관리자 "사용 안 함".</summary>
    public const uint DISABLED_MARKER = 0x03;
    private const int MARKER_LENGTH = sizeof(uint);
    private const int TIMESTAMP_OFFSET = MARKER_LENGTH;

    /// <summary>REG_BINARY 12바이트이고 첫 DWORD가 0x02(뒤 8바이트 0) 또는 0x03인 값만 지원합니다. 값 없음은 별도로 활성으로 해석합니다.</summary>
    public static bool ValidValue(RollbackValue value)
    {
        if (!value.Exists || value.NativeType != REG_BINARY || value.Data.Length != VALUE_LENGTH) { return false; }
        var marker = BinaryPrimitives.ReadUInt32LittleEndian(value.Data);
        return marker switch
        {
            ENABLED_MARKER => !value.Data.AsSpan(TIMESTAMP_OFFSET).ContainsAnyExcept((byte)0),
            DISABLED_MARKER => true,
            _ => false,
        };
    }

    /// <summary>값이 없으면 Windows는 활성으로 취급합니다. 지원하지 않는 형식은 알 수 없음입니다.</summary>
    public static StartupApprovedState StateOf(RollbackValue value)
    {
        if (!value.Exists) { return StartupApprovedState.Enabled; }
        if (!ValidValue(value)) { return StartupApprovedState.Unknown; }
        return BinaryPrimitives.ReadUInt32LittleEndian(value.Data) == ENABLED_MARKER ? StartupApprovedState.Enabled : StartupApprovedState.Disabled;
    }

    /// <summary>작업 관리자 "사용"과 같은 값입니다(0x02 + 0).</summary>
    public static RollbackValue Enabled()
    {
        var data = new byte[VALUE_LENGTH];
        BinaryPrimitives.WriteUInt32LittleEndian(data, ENABLED_MARKER);
        return new(true, REG_BINARY, data);
    }

    /// <summary>작업 관리자 "사용 안 함"과 같은 값입니다(0x03 + 비활성화 시각 FILETIME).</summary>
    public static RollbackValue Disabled(DateTimeOffset disabledAt)
    {
        var data = new byte[VALUE_LENGTH];
        BinaryPrimitives.WriteUInt32LittleEndian(data, DISABLED_MARKER);
        BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(TIMESTAMP_OFFSET), disabledAt.UtcDateTime.ToFileTimeUtc());
        return new(true, REG_BINARY, data);
    }

    /// <summary>현재 상태의 반대 상태 값을 만듭니다. 알 수 없는 형식은 null입니다.</summary>
    public static RollbackValue? Toggled(RollbackValue current, DateTimeOffset now) => StateOf(current) switch
    {
        StartupApprovedState.Enabled => Disabled(now),
        StartupApprovedState.Disabled => Enabled(),
        _ => null,
    };

    /// <summary>실행 출처(Run·시작 폴더)에 대응하는 StartupApproved 출처입니다. 대응이 없으면 null입니다.</summary>
    public static string? SourceFor(string? registrationSource) => registrationSource switch
    {
        StartupRegistration.Source => StartupRegistration.ApprovalUser,
        StartupRegistration.Machine64 => StartupRegistration.ApprovalMachine64,
        StartupRegistration.Machine32 => StartupRegistration.ApprovalMachine32,
        StartupRegistration.UserFolder => StartupRegistration.ApprovalUserFolder,
        StartupRegistration.CommonFolder => StartupRegistration.ApprovalCommonFolder,
        _ => null,
    };
}
