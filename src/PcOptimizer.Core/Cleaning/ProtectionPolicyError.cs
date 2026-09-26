/**
 * @file    : ProtectionPolicyError.cs
 * @author  : rudals252
 * @brief   : 보호 정책 파싱·검증 실패 사유 코드 열거형(정책이 무효이면 파일 순회를 시작하지 않는다)
 */

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 보호 정책 검증 결과 코드입니다. <see cref="None"/> 외에는 정책 전체가 무효이며 파일 순회를 하지 않습니다.
/// </summary>
public enum ProtectionPolicyError
{
    /// <summary>오류 없음(유효).</summary>
    None,

    /// <summary>정책 파일이 없거나 비어 있음.</summary>
    Missing,

    /// <summary>JSON 형식 오류.</summary>
    MalformedJson,

    /// <summary>최상위가 객체가 아님.</summary>
    NotAnObject,

    /// <summary>schemaVersion 없음.</summary>
    MissingSchemaVersion,

    /// <summary>지원하지 않는 schemaVersion(정수 1만 지원).</summary>
    UnsupportedSchemaVersion,

    /// <summary>protectedRoots 배열 없음.</summary>
    MissingRoots,

    /// <summary>protectedRoots 배열이 비어 있음.</summary>
    EmptyRoots,

    /// <summary>항목이 객체가 아님.</summary>
    InvalidEntry,

    /// <summary>알 수 없는 kind.</summary>
    UnknownKind,

    /// <summary>항목의 필수 문자열 값이 없거나 비어 있음.</summary>
    MissingField,

    /// <summary>알 수 없는 Known Folder 이름.</summary>
    UnknownKnownFolder,

    /// <summary>HKCU가 아닌 레지스트리 하이브.</summary>
    UnsupportedHive,

    /// <summary>절대 경로 템플릿이 아님(상대 경로·UNC·드라이브 루트 전체).</summary>
    InvalidPath,
}
