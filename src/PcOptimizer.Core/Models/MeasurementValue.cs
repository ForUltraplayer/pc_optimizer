/**
 * @file    : MeasurementValue.cs
 * @author  : rudals252
 * @brief   : 측정값의 값 종류(정수/실수/불리언/문자열/문자열 목록)를 구분하는 닫힌 레코드 계층
 */

// 기본 패키지
using System.Text.Json.Serialization;

namespace PcOptimizer.Core.Models;

/// <summary>
/// 측정값의 값입니다. 숫자·불리언·문자열·목록을 형식으로 구분하며 object 값을 쓰지 않습니다.
/// 외부 어셈블리에서 파생할 수 없는 닫힌 계층입니다.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(IntegerValue), "integer")]
[JsonDerivedType(typeof(DecimalValue), "decimal")]
[JsonDerivedType(typeof(BooleanValue), "boolean")]
[JsonDerivedType(typeof(TextValue), "text")]
[JsonDerivedType(typeof(TextListValue), "textList")]
public abstract record MeasurementValue
{
    /// <summary>
    /// 이 어셈블리 안의 봉인된 파생 형식만 만들 수 있도록 생성자를 제한합니다.
    /// </summary>
    private protected MeasurementValue()
    {
    }
}

/// <summary>
/// 정수 측정값입니다.
/// </summary>
/// <param name="Value">정수 값.</param>
public sealed record IntegerValue(long Value) : MeasurementValue;

/// <summary>
/// 실수 측정값입니다.
/// </summary>
/// <param name="Value">실수 값.</param>
public sealed record DecimalValue(double Value) : MeasurementValue;

/// <summary>
/// 불리언 측정값입니다. false와 "값 없음"을 구분하기 위해 별도 형식으로 둡니다.
/// </summary>
/// <param name="Value">불리언 값.</param>
public sealed record BooleanValue(bool Value) : MeasurementValue;

/// <summary>
/// 문자열 측정값입니다.
/// </summary>
/// <param name="Value">문자열 값.</param>
public sealed record TextValue(string Value) : MeasurementValue;

/// <summary>
/// 문자열 목록 측정값입니다. 빈 목록은 "조회 성공, 항목 없음"을 뜻하며 조회 실패를 대신하지 않습니다.
/// 목록은 생성(및 with 식) 시 복사해 읽기 전용 보기로 보관합니다.
/// </summary>
/// <param name="Values">문자열 목록(null 불가).</param>
public sealed record TextListValue(IReadOnlyList<string> Values) : MeasurementValue
{
    private readonly IReadOnlyList<string> _values = ReadOnlyListCopy.Of(Values, nameof(Values));

    /// <summary>문자열 목록(읽기 전용 복사본).</summary>
    public IReadOnlyList<string> Values
    {
        get => _values;
        init => _values = ReadOnlyListCopy.Of(value, nameof(Values));
    }
}
