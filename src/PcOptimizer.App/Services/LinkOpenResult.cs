/**
 * @file    : LinkOpenResult.cs
 * @author  : rudals252
 * @brief   : 외부 링크 열기 결과(열기 요청함/허용 목록에 없어 거부/셸 실행 실패) 열거형
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 링크 열기 결과입니다.
/// </summary>
public enum LinkOpenResult
{
    /// <summary>허용된 링크를 셸로 열도록 요청했음.</summary>
    Opened,

    /// <summary>허용 목록에 없어 열지 않았음.</summary>
    Refused,

    /// <summary>허용됐지만 셸 실행이 실패했음.</summary>
    Failed,
}
