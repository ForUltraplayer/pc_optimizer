/**
 * @file    : NullAppLogger.cs
 * @author  : rudals252
 * @brief   : 아무것도 기록하지 않는 IAppLogger 구현
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 아무것도 기록하지 않는 로거입니다. 로거가 필요 없는 테스트·기본값에 씁니다.
/// </summary>
public sealed class NullAppLogger : IAppLogger
{
    /// <summary>
    /// 외부에서 인스턴스를 만들지 않도록 막습니다.
    /// </summary>
    private NullAppLogger()
    {
    }

    /// <summary>공유 인스턴스.</summary>
    public static NullAppLogger Instance { get; } = new();

    /// <inheritdoc />
    public void Debug(string category, string message, Exception? ex = null)
    {
    }

    /// <inheritdoc />
    public void Info(string category, string message, Exception? ex = null)
    {
    }

    /// <inheritdoc />
    public void Warn(string category, string message, Exception? ex = null)
    {
    }

    /// <inheritdoc />
    public void Error(string category, string message, Exception? ex = null)
    {
    }
}
