/**
 * @file    : VendorLinkParseResult.cs
 * @author  : rudals252
 * @brief   : 공식 링크 표 해석 결과(검증을 통과한 표 또는 null과 오류 코드 목록) 레코드
 */

namespace PcOptimizer.Core.Drivers;

/// <summary>
/// 공식 링크 표 해석 결과입니다. 오류가 하나라도 있으면 <see cref="Catalog"/>는 null입니다.
/// </summary>
/// <param name="Catalog">검증을 통과한 표(오류가 있으면 null).</param>
/// <param name="Errors">오류 코드 목록(영문 코드, 사용자 문장 아님).</param>
public sealed record VendorLinkParseResult(VendorLinkCatalog? Catalog, IReadOnlyList<string> Errors);
