/**
 * @file    : Win32RegistryReader.cs
 * @author  : rudals252
 * @brief   : Microsoft.Win32.Registry로 레지스트리 값·키의 값 목록·하위 키 이름을 읽기 전용으로 여는 조회 구현(쓰기 권한 요청 없음, 실패를 상태로 변환)
 */

// 기본 패키지
using System.Security;
using Microsoft.Win32;

namespace PcOptimizer.Probes.Platform;

/// <summary>
/// 레지스트리 읽기 구현입니다. 키는 항상 읽기 전용(writable: false)으로 엽니다.
/// 값 하나 읽기에서 HKLM은 64비트 보기로 열고, 키 전체 읽기는 호출자가 지정한 보기로 엽니다.
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

    /// <inheritdoc />
    public RegistryKeyReading ReadKeyValues(RegistryRoot root, RegistryView view, string subKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subKey);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(root == RegistryRoot.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            if (key is null)
            {
                return new RegistryKeyReading(RegistryReadStatus.KeyMissing, [], null);
            }

            var values = new List<RegistryValueEntry>();
            foreach (var name in key.GetValueNames())
            {
                var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (raw is null)
                {
                    // 열거 뒤 사라진 값은 건너뛴다(목록 시점 차이).
                    continue;
                }

                var kind = key.GetValueKind(name);
                values.Add(new RegistryValueEntry(name, kind.ToString(), raw as string, raw as byte[]));
            }

            return new RegistryKeyReading(RegistryReadStatus.Found, values, null);
        }
        catch (SecurityException ex)
        {
            return new RegistryKeyReading(RegistryReadStatus.AccessDenied, [], ex.GetType().Name);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RegistryKeyReading(RegistryReadStatus.AccessDenied, [], ex.GetType().Name);
        }
        catch (IOException ex)
        {
            return new RegistryKeyReading(RegistryReadStatus.Error, [], ex.GetType().Name);
        }
    }

    /// <inheritdoc />
    public RegistrySubKeyReading ReadSubKeyNames(RegistryRoot root, RegistryView view, string subKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subKey);

        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(root == RegistryRoot.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            return key is null
                ? new RegistrySubKeyReading(RegistryReadStatus.KeyMissing, [], null)
                : new RegistrySubKeyReading(RegistryReadStatus.Found, key.GetSubKeyNames(), null);
        }
        catch (SecurityException ex)
        {
            return new RegistrySubKeyReading(RegistryReadStatus.AccessDenied, [], ex.GetType().Name);
        }
        catch (UnauthorizedAccessException ex)
        {
            return new RegistrySubKeyReading(RegistryReadStatus.AccessDenied, [], ex.GetType().Name);
        }
        catch (IOException ex)
        {
            return new RegistrySubKeyReading(RegistryReadStatus.Error, [], ex.GetType().Name);
        }
    }
}
