/**
 * @file    : IAppLogger.cs
 * @author  : rudals252
 * @brief   : 프로젝트 공용 로거 래퍼 계약(Debug/Info/Warn/Error)
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// 프로젝트 공용 로거 계약입니다. 모든 로깅은 이 계약을 거치며 Console을 직접 쓰지 않습니다.
/// 파일 롤링 등 실제 구현은 App이 제공합니다.
/// </summary>
public interface IAppLogger
{
    /// <summary>디버그 로그를 남깁니다.</summary>
    /// <param name="category">로그 분류(보통 발신 형식 이름).</param>
    /// <param name="message">메시지. 개인 경로·원시 덤프를 담지 않는다.</param>
    /// <param name="ex">관련 예외(선택).</param>
    void Debug(string category, string message, Exception? ex = null);

    /// <summary>정보 로그를 남깁니다.</summary>
    /// <param name="category">로그 분류.</param>
    /// <param name="message">메시지.</param>
    /// <param name="ex">관련 예외(선택).</param>
    void Info(string category, string message, Exception? ex = null);

    /// <summary>경고 로그를 남깁니다.</summary>
    /// <param name="category">로그 분류.</param>
    /// <param name="message">메시지.</param>
    /// <param name="ex">관련 예외(선택).</param>
    void Warn(string category, string message, Exception? ex = null);

    /// <summary>오류 로그를 남깁니다.</summary>
    /// <param name="category">로그 분류.</param>
    /// <param name="message">메시지.</param>
    /// <param name="ex">관련 예외(선택).</param>
    void Error(string category, string message, Exception? ex = null);
}
