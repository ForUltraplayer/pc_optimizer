/**
 * @file    : FakeRegistryReader.cs
 * @author  : rudals252
 * @brief   : 값 이름별 고정 읽기 결과(값·없음·접근 거부)와 루트·보기·키별 고정 키 읽기·하위 키 이름·문자열 값 결과를 돌려주고 키·문자열 값 읽기 호출을 기록하는 테스트용 레지스트리 읽기
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 레지스트리 읽기입니다. 등록하지 않은 값은 ValueMissing, 등록하지 않은 키는 KeyMissing입니다.
/// </summary>
internal sealed class FakeRegistryReader : IRegistryReader
{
    private readonly Dictionary<string, RegistryValueReading> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegistryKeyReading> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegistrySubKeyReading> _subKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RegistryStringReading> _strings = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>키 읽기 호출 기록("루트|보기|키").</summary>
    public List<string> KeyReads { get; } = [];

    /// <summary>문자열 값 읽기 호출 기록("루트|보기|키|값 이름").</summary>
    public List<string> StringReads { get; } = [];

    /// <summary>
    /// 문자열 값 하나를 등록한다(Found).
    /// </summary>
    public FakeRegistryReader WithString(RegistryRoot root, RegistryView view, string subKey, string valueName, string text)
    {
        _strings[KeyOf(root, view, subKey) + "|" + valueName] = new RegistryStringReading(RegistryReadStatus.Found, text, null);
        return this;
    }

    /// <inheritdoc />
    public RegistryStringReading ReadStringValue(RegistryRoot root, RegistryView view, string subKey, string valueName)
    {
        var key = KeyOf(root, view, subKey) + "|" + valueName;
        StringReads.Add(key);
        return _strings.TryGetValue(key, out var reading)
            ? reading
            : new RegistryStringReading(RegistryReadStatus.KeyMissing, null, null);
    }

    /// <summary>
    /// DWORD 값을 등록한다.
    /// </summary>
    public FakeRegistryReader WithDword(string valueName, long value)
    {
        _values[valueName] = new RegistryValueReading(RegistryReadStatus.Found, "DWord", value, null);
        return this;
    }

    /// <summary>
    /// 임의 읽기 결과를 등록한다.
    /// </summary>
    public FakeRegistryReader With(string valueName, RegistryValueReading reading)
    {
        _values[valueName] = reading;
        return this;
    }

    /// <summary>
    /// 키 전체 읽기 결과를 등록한다.
    /// </summary>
    public FakeRegistryReader WithKey(RegistryRoot root, RegistryView view, string subKey, RegistryKeyReading reading)
    {
        _keys[KeyOf(root, view, subKey)] = reading;
        return this;
    }

    /// <summary>
    /// 값 목록으로 키를 등록한다(Found).
    /// </summary>
    public FakeRegistryReader WithKeyValues(RegistryRoot root, RegistryView view, string subKey, params RegistryValueEntry[] values)
    {
        return WithKey(root, view, subKey, new RegistryKeyReading(RegistryReadStatus.Found, values, null));
    }

    /// <summary>
    /// 하위 키 이름 읽기 결과를 등록한다.
    /// </summary>
    public FakeRegistryReader WithSubKeys(RegistryRoot root, RegistryView view, string subKey, RegistrySubKeyReading reading)
    {
        _subKeys[KeyOf(root, view, subKey)] = reading;
        return this;
    }

    /// <inheritdoc />
    public RegistrySubKeyReading ReadSubKeyNames(RegistryRoot root, RegistryView view, string subKey)
    {
        return _subKeys.TryGetValue(KeyOf(root, view, subKey), out var reading)
            ? reading
            : new RegistrySubKeyReading(RegistryReadStatus.KeyMissing, [], null);
    }

    /// <inheritdoc />
    public RegistryValueReading ReadValue(RegistryRoot root, string subKey, string valueName)
    {
        return _values.TryGetValue(valueName, out var reading)
            ? reading
            : new RegistryValueReading(RegistryReadStatus.ValueMissing, null, null, null);
    }

    /// <inheritdoc />
    public RegistryKeyReading ReadKeyValues(RegistryRoot root, RegistryView view, string subKey)
    {
        var key = KeyOf(root, view, subKey);
        KeyReads.Add(key);
        return _keys.TryGetValue(key, out var reading)
            ? reading
            : new RegistryKeyReading(RegistryReadStatus.KeyMissing, [], null);
    }

    /// <summary>
    /// 키 조회용 문자열을 만든다.
    /// </summary>
    public static string KeyOf(RegistryRoot root, RegistryView view, string subKey)
    {
        return $"{root}|{view}|{subKey}";
    }
}
