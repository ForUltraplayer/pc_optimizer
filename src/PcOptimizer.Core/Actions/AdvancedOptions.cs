/**
 * @file    : AdvancedOptions.cs
 * @author  : rudals252
 * @brief   : 고급 설정의 닫힌 대상·선택·관측 계약. 임의 경로나 명령을 받지 않는다
 */
namespace PcOptimizer.Core.Actions;

/// <summary>현재 직접 제어를 구현한 옵션입니다.</summary>
public enum AdvancedOption { Mpo, Hags, GameMode }
/// <summary>기본값 복귀는 작업 전 값 복원과 다릅니다.</summary>
public enum AdvancedSetting { Default, On, Off }
/// <summary>재부팅 목적입니다. AdvancedStartup은 안전 모드 직행이 아닙니다.</summary>
public enum RestartDestination { Firmware, AdvancedStartup }
/// <summary>설정값 관측입니다. 실제 GPU 처리 중 여부를 뜻하지 않습니다.</summary>
public sealed record AdvancedObservation(AdvancedOption Option, AdvancedSetting? Setting, bool CanChange, string Detail);

/// <summary>기능 이름과 조치 매핑을 코드에 고정합니다.</summary>
public static class AdvancedOptions
{
    /// <summary>옵션별 조치 ID입니다.</summary>
    public static ActionId Action(AdvancedOption option) => option switch
    {
        AdvancedOption.Mpo => ActionId.Mpo, AdvancedOption.Hags => ActionId.Hags,
        AdvancedOption.GameMode => ActionId.GameMode, _ => throw new ArgumentOutOfRangeException(nameof(option)),
    };
    /// <summary>실행과 복구 기록에서 사용하는 한국어 이름입니다.</summary>
    public static string Name(AdvancedOption option) => option switch
    {
        AdvancedOption.Mpo => "MPO · 화면 오버레이", AdvancedOption.Hags => "GPU 하드웨어 가속 일정 예약(HAGS)",
        AdvancedOption.GameMode => "Windows 게임 모드", _ => "알 수 없는 고급 설정",
    };
    /// <summary>공통 원본 부재 값입니다.</summary>
    public static RollbackValue Absent => new(false, 0, []);
    /// <summary>32비트 레지스트리 값을 손실 없이 표현합니다.</summary>
    public static RollbackValue Dword(int value) => new(true, 4, BitConverter.GetBytes(value));
    /// <summary>선택 가능한 값만 원문으로 변환합니다. MPO 강제 On은 지원하지 않습니다.</summary>
    public static RollbackValue Desired(AdvancedOption option, AdvancedSetting setting) => (option, setting) switch
    {
        (AdvancedOption.Mpo or AdvancedOption.Hags or AdvancedOption.GameMode, AdvancedSetting.Default) => Absent,
        (AdvancedOption.Mpo, AdvancedSetting.Off) => Dword(5),
        (AdvancedOption.Hags, AdvancedSetting.On) => Dword(2),
        (AdvancedOption.Hags, AdvancedSetting.Off) => Dword(1),
        (AdvancedOption.GameMode, AdvancedSetting.On) => Dword(1),
        (AdvancedOption.GameMode, AdvancedSetting.Off) => Dword(0),
        _ => throw new ArgumentOutOfRangeException(nameof(setting)),
    };
    /// <summary>지원하는 관측값만 의미를 붙입니다. 다른 형식·값은 확인 불가입니다.</summary>
    public static AdvancedSetting? Interpret(AdvancedOption option, RollbackValue value)
    {
        if (!value.Exists && value.NativeType == 0 && value.Data.Length == 0) { return AdvancedSetting.Default; }
        if (value.NativeType != 4 || value.Data.Length != 4) { return null; }
        var number = BitConverter.ToInt32(value.Data);
        return (option, number) switch
        {
            (AdvancedOption.Mpo, 5) => AdvancedSetting.Off,
            (AdvancedOption.Hags, 2) or (AdvancedOption.GameMode, 1) => AdvancedSetting.On,
            (AdvancedOption.Hags, 1) or (AdvancedOption.GameMode, 0) => AdvancedSetting.Off,
            _ => null,
        };
    }
}
