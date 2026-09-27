/**
 * @file    : ActionUnavailableException.cs
 * @author  : rudals252
 * @brief   : 코드 어댑터가 확인한 실행 거절 사유를 원문 예외 없이 전달
 */
namespace PcOptimizer.Probes.Actions;

// 외부 입력을 오류 메시지로 노출하지 않는 내부 코드 계약이다.
internal sealed class ActionUnavailableException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}
