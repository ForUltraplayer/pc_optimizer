/**
 * @file    : ProtectedRootSpec.cs
 * @author  : rudals252
 * @brief   : 보호 정책(protect.json)의 보호 루트 항목 하나를 종류별(Known Folder·환경 경로·동기화 환경 변수·동기화 레지스트리·동기화 JSON 파일)로 구분하는 닫힌 레코드 계층
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 보호 정책 루트 항목입니다. 실제 경로 해석은 Probes(ProtectionPolicyResolver)가 하며 여기서는 값만 담습니다.
/// 외부 어셈블리에서 파생할 수 없는 닫힌 계층입니다.
/// </summary>
public abstract record ProtectedRootSpec
{
    /// <summary>
    /// 이 어셈블리 안의 봉인된 파생 형식만 만들 수 있도록 생성자를 제한합니다.
    /// </summary>
    private protected ProtectedRootSpec()
    {
    }
}

/// <summary>
/// Known Folder(문서·사진·바탕화면·동영상·음악). 리디렉션된 실제 위치로 해석합니다.
/// </summary>
/// <param name="Folder">Known Folder 종류.</param>
public sealed record KnownFolderRootSpec(ProtectedKnownFolder Folder) : ProtectedRootSpec;

/// <summary>
/// 환경 변수로 시작하는 시스템 경로(예: <c>%SystemRoot%\System32</c>).
/// </summary>
/// <param name="PathTemplate">%이름% 환경 변수를 포함할 수 있는 절대 경로 템플릿.</param>
public sealed record EnvironmentPathRootSpec(string PathTemplate) : ProtectedRootSpec;

/// <summary>
/// 환경 변수 값이 곧 동기화 루트인 경우(예: OneDrive의 <c>OneDrive</c> 변수).
/// </summary>
/// <param name="Provider">동기화 제공자 이름(예: "OneDrive").</param>
/// <param name="VariableName">환경 변수 이름.</param>
public sealed record CloudSyncEnvironmentRootSpec(string Provider, string VariableName) : ProtectedRootSpec;

/// <summary>
/// HKCU 키의 하위 키마다 들어 있는 문자열 값이 동기화 루트인 경우(예: OneDrive <c>Accounts\*\UserFolder</c>).
/// </summary>
/// <param name="Provider">동기화 제공자 이름.</param>
/// <param name="AccountsSubKey">하위 키를 열거할 HKCU 기준 키 경로.</param>
/// <param name="ValueName">각 하위 키에서 읽을 문자열 값 이름.</param>
public sealed record CloudSyncRegistryRootSpec(string Provider, string AccountsSubKey, string ValueName) : ProtectedRootSpec;

/// <summary>
/// 작은 JSON 설정 파일의 최상위 객체들에 있는 문자열 속성이 동기화 루트인 경우(예: Dropbox <c>info.json</c>의 <c>path</c>).
/// </summary>
/// <param name="Provider">동기화 제공자 이름.</param>
/// <param name="FilePathTemplate">%이름% 환경 변수를 포함할 수 있는 JSON 파일 경로 템플릿.</param>
/// <param name="PropertyName">최상위 객체마다 읽을 문자열 속성 이름.</param>
public sealed record CloudSyncJsonFileRootSpec(string Provider, string FilePathTemplate, string PropertyName) : ProtectedRootSpec;
