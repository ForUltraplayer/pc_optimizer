/**
 * @file    : CleaningRuleKinds.cs
 * @author  : rudals252
 * @brief   : winapp2 형식 정리 규칙의 출처(커뮤니티/검토한 보충), 레지스트리 탐지 루트, 제외 종류(FILE/PATH), 미지원 사유 코드 열거형
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 규칙의 출처입니다. 관측 우선순위(스펙 §5.1)는 검토한 보충 규칙·앱 설정 경로가 커뮤니티 기본 경로보다 높습니다.
/// </summary>
public enum RuleOrigin
{
    /// <summary>커뮤니티 규칙(winapp2 스냅샷). 영향 설명은 검토하지 않았습니다.</summary>
    Community,

    /// <summary>프로젝트가 검토한 보충 규칙(supplement.ini).</summary>
    Supplement,
}

/// <summary>
/// 레지스트리 탐지(Detect) 키의 루트입니다. 지원하지 않는 루트(HKU 등)를 쓰는 규칙은 미지원입니다.
/// </summary>
public enum RuleRegistryHive
{
    /// <summary>HKEY_CURRENT_USER.</summary>
    CurrentUser,

    /// <summary>HKEY_LOCAL_MACHINE(64비트·32비트 보기 모두 확인).</summary>
    LocalMachine,

    /// <summary>HKEY_CLASSES_ROOT(사용자·컴퓨터 Software\Classes 병합 보기로 확인).</summary>
    ClassesRoot,
}

/// <summary>
/// ExcludeKey 종류입니다.
/// </summary>
public enum ExcludeKind
{
    /// <summary>FILE: 지정 폴더에 직접 들어 있는 파일 중 패턴에 맞는 파일을 제외합니다.</summary>
    File,

    /// <summary>PATH: 지정 폴더와 그 하위 전체에서 패턴에 맞는 파일을 제외합니다(패턴이 모든 파일이면 하위 트리 전체 제외).</summary>
    Path,
}

/// <summary>
/// 규칙을 적용하지 않고 건너뛴 사유입니다. 미지원 규칙은 일부만 적용하지 않고 통째로 건너뜁니다(스펙 §5.1).
/// </summary>
public enum UnsupportedRuleReason
{
    /// <summary>알 수 없는 키.</summary>
    UnknownKey,

    /// <summary>운영체제 제약(DetectOS)은 해석하지 않습니다.</summary>
    DetectOs,

    /// <summary>특수 탐지(SpecialDetect)는 해석하지 않습니다.</summary>
    SpecialDetect,

    /// <summary>FILE/PATH가 아닌 제외 종류(REG 등)나 형식이 맞지 않는 제외.</summary>
    UnsupportedExclude,

    /// <summary>지원 표에 없거나 이 PC에서 해석되지 않는 환경 변수.</summary>
    UnresolvedVariable,

    /// <summary>볼륨 루트 전체로 확장되는 경로.</summary>
    VolumeRootPath,

    /// <summary>UNC·장치 경로.</summary>
    UncOrDevicePath,

    /// <summary>형식이 맞지 않는 줄·FileKey·섹션.</summary>
    MalformedEntry,

    /// <summary>Detect·DetectFile이 없어 안전한 탐지 근거가 없음(모든 사용자에게 적용하지 않음).</summary>
    NoSafeDetection,

    /// <summary>레지스트리 효과(RegKey)만 있고 관측할 파일 효과가 없음.</summary>
    RegistryOnly,

    /// <summary>지원하지 않는 레지스트리 루트(HKU 등)의 탐지.</summary>
    UnsupportedDetectRoot,

    /// <summary>삭제 의미의 특수 지시어(예: RemoveEmptyFoldersOnly) 등 해석하지 않는 FileKey 토큰.</summary>
    UnsupportedDirective,

    /// <summary>와일드카드 경로 구성 요소가 너무 많거나(정적) 일치 항목이 한도를 넘음(실행 시).</summary>
    WildcardBoundExceeded,
}
