/**
 * @file    : IElevatedExecutor.cs
 * @author  : rudals252
 * @brief   : [예약] 관리자 권한 실행기 타입 계약(명시적 작업 계획만 받으며 명령 문자열을 받지 않음). 1차 미구현·미호출
 */

namespace PcOptimizer.Core.Abstractions;

/// <summary>
/// [예약·1차 미구현·미호출] 관리자 권한이 필요한 작업 계획을 받아 실행하고 결과를 반환하는 실행기 계약입니다.
/// 1차에서는 구현·등록·호출하지 않으며 별도 프로젝트도 만들지 않습니다.
/// 임의 명령 문자열 실행은 계약에 포함하지 않고, 명시적인 작업 종류와 검증된 매개변수 모델만 받습니다.
/// </summary>
public interface IElevatedExecutor
{
    /// <summary>
    /// [1차 미구현·미호출] 작업 계획을 실행합니다.
    /// </summary>
    /// <param name="plan">명시적인 작업 계획.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>실행 결과.</returns>
    Task<ElevatedExecutionResult> ExecuteAsync(ElevatedActionPlan plan, CancellationToken ct);
}

/// <summary>
/// [예약·1차 미구현·미호출] 관리자 권한 실행기에 넘길 작업 계획입니다.
/// </summary>
/// <param name="PlanId">계획 ID.</param>
/// <param name="Actions">실행할 작업 목록(명시적 작업 종류만).</param>
public sealed record ElevatedActionPlan(Guid PlanId, IReadOnlyList<ElevatedAction> Actions);

/// <summary>
/// [예약·1차 미구현·미호출] 관리자 권한 작업 하나입니다. 외부 어셈블리에서 파생할 수 없는 닫힌 계층이며,
/// 구체 작업 종류(검증된 매개변수 모델)는 2차에서 이 어셈블리 안에 정의합니다. 명령 문자열 작업은 두지 않습니다.
/// </summary>
public abstract record ElevatedAction
{
    /// <summary>
    /// 이 어셈블리 안의 파생 형식만 만들 수 있도록 생성자를 제한합니다.
    /// </summary>
    private protected ElevatedAction()
    {
    }
}

/// <summary>
/// [예약·1차 미구현·미호출] 작업 계획 실행 결과입니다.
/// </summary>
/// <param name="PlanId">실행한 계획 ID.</param>
/// <param name="Succeeded">모든 작업이 성공했는지 여부.</param>
/// <param name="Summary">결과 요약.</param>
public sealed record ElevatedExecutionResult(Guid PlanId, bool Succeeded, string Summary);
