/**
 * @file    : Win32RegistryReader.cs
 * @author  : rudals252
 * @brief   : Microsoft.Win32.Registry로 레지스트리 값을 읽기 전용으로 여는 조회 구현(쓰기 권한 요청 없음, 실패를 상태로 변환)
 */

// 기본 패키지
using System.Security;
using Microsoft.Win32;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 레지스트리 읽기 구현입니다. 키는 항상 읽기 전용(writable: false)으로 열며 HKLM은 64비트 보기로 엽니다.
/// </summary>
public sealed class Win32RegistryReader : IRegistryReader
{
    /// <summary>공유 인스턴스.</summary>
    public static Win32RegistryReader Instance { get; } = new();

    /// <inheritdoc />
    public RegistryValueReading ReadValue(RegistryRoot root, string subKey, string valueName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subKey);
        ArgumentNullException.ThrowIfNull(valueName);

        try
        {
            using var baseKey = root == RegistryRoot.LocalMachine
                ? RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                : RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            if (key is null)
            {
                return new RegistryValueReading(RegistryReadStatus.KeyMissing, null, null, null);
            }

            var raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is null)
            {
                return new RegistryValueReading(RegistryReadStatus.ValueMissing, null, null, null);
            }

            var kind = key.GetValueKind(valueName);
            long? dword = kind == RegistryValueKind.DWord && raw is int value ? unchecked((uint)value) : null;
            return new RegistryValueReading(RegistryReadStatus.Found, kind.ToString(), dword, null);
        }
        catch (SecurityException ex)
        {
            return new RegistryValueReading(RegistryReadStatus.AccessDenied, null, null, ex.GetType().Name);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RegistryValueReading(RegistryReadStatus.AccessDenied, null, null, ex.GetType().Name);
        }
        catch (IOException ex)
        {
            return new RegistryValueReading(RegistryReadStatus.Error, null, null, ex.GetType().Name);
        }
    }
}
