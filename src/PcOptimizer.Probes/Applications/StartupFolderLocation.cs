/**
 * @file    : StartupFolderLocation.cs
 * @author  : rudals252
 * @brief   : 시작 프로그램 프로브가 열거할 시작프로그램 폴더 하나(출처 코드·특수 폴더 이름·실제 경로·StartupApproved 루트) 정의 레코드
 */

// 사용자 패키지
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 열거할 시작프로그램 폴더 하나입니다.
/// </summary>
/// <param name="SourceCode">출처 코드(<c>StartupItemsProbeContract.SOURCE_*_FOLDER</c>).</param>
/// <param name="LocationName">특수 폴더 이름(예: "Startup"). 측정값·Issue에는 실제 경로 대신 이 이름을 씁니다.</param>
/// <param name="Path">폴더 경로. 알 수 없으면 null 또는 빈 문자열.</param>
/// <param name="ApprovedRoot">이 폴더 항목의 StartupApproved\StartupFolder 키가 있는 루트(사용자 폴더는 HKCU, 공용 폴더는 HKLM).</param>
public sealed record StartupFolderLocation(string SourceCode, string LocationName, string? Path, RegistryRoot ApprovedRoot);
