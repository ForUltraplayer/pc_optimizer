/**
 * @file    : FindingInvariantException.cs
 * @author  : rudals252
 * @brief   : Finding 데이터 모델 불변식 위반을 알리는 예외
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// Finding 생성 인자가 데이터 모델 불변식을 어겼을 때 던지는 예외입니다.
/// </summary>
public sealed class FindingInvariantException : ArgumentException
{
    /// <summary>
    /// 기본 메시지로 예외를 만듭니다.
    /// </summary>
    public FindingInvariantException()
    {
    }

    /// <summary>
    /// 위반 내용을 설명하는 메시지로 예외를 만듭니다.
    /// </summary>
    /// <param name="message">위반 내용.</param>
    public FindingInvariantException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 위반 내용과 원인 예외로 예외를 만듭니다.
    /// </summary>
    /// <param name="message">위반 내용.</param>
    /// <param name="innerException">원인 예외.</param>
    public FindingInvariantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 위반 내용과 위반한 인자 이름으로 예외를 만듭니다.
    /// </summary>
    /// <param name="message">위반 내용.</param>
    /// <param name="paramName">위반한 인자 이름.</param>
    public FindingInvariantException(string message, string paramName)
        : base(message, paramName)
    {
    }
}
