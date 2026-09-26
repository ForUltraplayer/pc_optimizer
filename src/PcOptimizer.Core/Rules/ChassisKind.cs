/**
 * @file    : ChassisKind.cs
 * @author  : rudals252
 * @brief   : SMBIOS 섀시 코드 목록을 분류한 결과(알 수 없음/노트북형/데스크톱형/기타) 열거형
 */

namespace PcOptimizer.Core.Rules;

/// <summary>
/// 섀시 분류 결과입니다. 불명·모순은 <see cref="Unknown"/>이며 데스크톱으로 추정하지 않습니다.
/// </summary>
public enum ChassisKind
{
    /// <summary>알 수 없음(값 없음, 불명 코드만 있음, 노트북형과 데스크톱형이 섞임, 해석 실패).</summary>
    Unknown,

    /// <summary>노트북형(휴대형·노트북·태블릿·변환형 등).</summary>
    Laptop,

    /// <summary>데스크톱형(데스크톱·타워·미니 PC 등).</summary>
    Desktop,

    /// <summary>그 밖의 알려진 섀시(서버 랙 등).</summary>
    Other,
}
