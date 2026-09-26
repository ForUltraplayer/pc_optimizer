/**
 * @file    : IExportPathPicker.cs
 * @author  : rudals252
 * @brief   : 리포트 저장 경로를 사용자에게 고르게 하는 계약(테스트에서 가짜 경로로 바꿈)
 */

namespace PcOptimizer.App.Services;

/// <summary>
/// 리포트를 저장할 경로를 사용자에게 묻는 계약입니다.
/// </summary>
public interface IExportPathPicker
{
    /// <summary>
    /// 저장 경로를 묻습니다.
    /// </summary>
    /// <param name="suggestedFileName">제안할 파일 이름.</param>
    /// <returns>고른 경로. 사용자가 취소하면 null.</returns>
    string? PickSavePath(string suggestedFileName);
}
