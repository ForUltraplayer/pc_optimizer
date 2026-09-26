/**
 * @file    : FakeRegistryReader.cs
 * @author  : rudals252
 * @brief   : 값 이름별 고정 읽기 결과(값·없음·접근 거부)를 돌려주는 테스트용 레지스트리 읽기
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Tests.Unit.Probes.Fakes;

/// <summary>
/// 가짜 레지스트리 읽기입니다. 등록하지 않은 값은 ValueMissing입니다.
/// </summary>
internal sealed class FakeRegistryReader : IRegistryReader
{
    private readonly Dictionary<string, RegistryValueReading> _values = new(StringComparer.OrdinalIgnoreCase);

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

    /// <inheritdoc />
    public RegistryValueReading ReadValue(RegistryRoot root, string subKey, string valueName)
    {
        return _values.TryGetValue(valueName, out var reading)
            ? reading
            : new RegistryValueReading(RegistryReadStatus.ValueMissing, null, null, null);
    }
}
