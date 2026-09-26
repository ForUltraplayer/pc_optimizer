/**
 * @file    : RegistrySettingReader.cs
 * @author  : rudals252
 * @brief   : 레지스트리 설정값 하나를 읽어 존재·형식·DWORD 값 측정값 또는 Issue(접근 거부·오류)로 바꾸는 그래픽 설정 프로브 공용 도우미
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Hardware;

/// <summary>
/// 설정값 하나를 <see cref="GraphicsSettingsProbeContract"/> 측정 이름으로 기록하는 도우미입니다.
/// 키·값이 없으면 존재 여부 false만 기록하고(0이나 꺼짐으로 채우지 않음), 읽기 실패는 측정값 없이 Issue로 알립니다.
/// </summary>
internal static class RegistrySettingReader
{
    /// <summary>
    /// 값 하나를 읽어 측정값 또는 Issue를 추가합니다.
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="setting">읽을 설정.</param>
    /// <param name="measurements">측정값 목록.</param>
    /// <param name="issues">Issue 목록.</param>
    /// <param name="observedAt">관측 시각.</param>
    /// <returns>읽기에 성공(값 없음 포함)하면 true, 실패하면 false.</returns>
    public static bool Read(
        IRegistryReader registry,
        RegistrySetting setting,
        List<Measurement> measurements,
        List<Issue> issues,
        DateTimeOffset observedAt)
    {
        var reading = registry.ReadValue(setting.Root, setting.SubKey, setting.ValueName);
        switch (reading.Status)
        {
            case RegistryReadStatus.AccessDenied:
                issues.Add(new Issue(CannotVerifyReason.AccessDenied, ProbeText.Format(ProbeStrings.Registry_AccessDenied, setting.ValueName)));
                return false;
            case RegistryReadStatus.Error:
                issues.Add(new Issue(
                    CannotVerifyReason.ProbeError, ProbeText.Format(ProbeStrings.Registry_Error, setting.ValueName, reading.ErrorCode ?? string.Empty)));
                return false;
            case RegistryReadStatus.KeyMissing:
            case RegistryReadStatus.ValueMissing:
                Add(measurements, setting, GraphicsSettingsProbeContract.FIELD_EXISTS, new BooleanValue(false), observedAt);
                return true;
            case RegistryReadStatus.Found:
            default:
                Add(measurements, setting, GraphicsSettingsProbeContract.FIELD_EXISTS, new BooleanValue(true), observedAt);
                if (reading.Kind is { } kind)
                {
                    Add(measurements, setting, GraphicsSettingsProbeContract.FIELD_KIND, new TextValue(kind), observedAt);
                }

                if (reading.DwordValue is { } value)
                {
                    Add(measurements, setting, GraphicsSettingsProbeContract.FIELD_VALUE, new IntegerValue(value), observedAt);
                }

                return true;
        }
    }

    /// <summary>
    /// 설정 필드 측정값을 추가한다.
    /// </summary>
    private static void Add(List<Measurement> measurements, RegistrySetting setting, string field, MeasurementValue value, DateTimeOffset observedAt)
    {
        measurements.Add(new Measurement(
            GraphicsSettingsProbeContract.MeasurementName(setting.Prefix, field), value, null, setting.Source, observedAt, MeasurementQuality.Observed));
    }
}

/// <summary>
/// 읽을 레지스트리 설정값 하나의 정의입니다.
/// </summary>
/// <param name="Root">레지스트리 루트.</param>
/// <param name="SubKey">하위 키 경로.</param>
/// <param name="ValueName">값 이름.</param>
/// <param name="Prefix">측정 이름 접두사(<see cref="GraphicsSettingsProbeContract"/>).</param>
/// <param name="Source">측정값 출처 문자열.</param>
internal sealed record RegistrySetting(RegistryRoot Root, string SubKey, string ValueName, string Prefix, string Source);
