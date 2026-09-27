/**
 * @file    : GpuOptions.cs
 * @author  : rudals252
 * @brief   : GPU별 고급 설정 대상과 원본 값·드라이버 계약
 */
namespace PcOptimizer.Core.Actions;

/// <summary>실행기 코드에서 등록하는 GPU 기능입니다.</summary>
public enum GpuFeature { NvidiaRebar, NvidiaVideo, AmdVideo }
/// <summary>드라이버에 쓰는 값이 아닌 사용자 선택입니다.</summary>
public sealed record GpuSettingChoice(int Value, string Label);
/// <summary>재식별 가능한 대상과 지원된 선택 목록입니다.</summary>
public sealed record GpuOptionTarget(GpuFeature Feature, string Key, string Label, string Detail, IReadOnlyList<GpuSettingChoice> Choices);
/// <summary>값과 원본의 출처를 보존합니다. Stamp 변경은 드라이버/장치 변경으로 거절합니다.</summary>
public sealed record GpuSettingState(string Stamp, uint SettingId, int Value, bool Enabled, uint Location = 0,
    uint Predefined = 0, uint PredefinedValid = 0, uint PredefinedValue = 0);
/// <summary>벤더별 조회 실패를 빈 정상 목록과 구분합니다.</summary>
public sealed record GpuOptionsResult(IReadOnlyList<GpuOptionTarget> Targets, string Status);
/// <summary>GPU 기능과 기존 실행 관문의 닫힌 매핑입니다.</summary>
public static class GpuOptions
{
    /// <summary>기능에 맞는 실행 ID입니다.</summary>
    public static ActionId Action(GpuFeature feature) => feature switch
    {
        GpuFeature.NvidiaRebar => ActionId.NvidiaRebar, GpuFeature.NvidiaVideo => ActionId.NvidiaVideo,
        GpuFeature.AmdVideo => ActionId.AmdVideo, _ => throw new ArgumentOutOfRangeException(nameof(feature)),
    };
    /// <summary>표시 이름입니다.</summary>
    public static string Name(GpuFeature feature) => feature switch
    { GpuFeature.NvidiaRebar => "NVIDIA 게임별 ReBAR", GpuFeature.NvidiaVideo => "NVIDIA RTX 영상 초고해상도", _ => "AMD 동영상 업스케일링" };
}
