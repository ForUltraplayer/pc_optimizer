/**
 * @file    : ScanReportVersions.cs
 * @author  : rudals252
 * @brief   : 리포트에 기록할 앱 버전과 규칙 버전 레코드
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 리포트에 기록할 버전 정보입니다. App이 채웁니다.
/// </summary>
/// <param name="AppVersion">앱 버전.</param>
/// <param name="RulesVersion">규칙 데이터 버전.</param>
public sealed record ScanReportVersions(string AppVersion, string RulesVersion);
