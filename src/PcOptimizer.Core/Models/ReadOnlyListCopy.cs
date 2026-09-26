/**
 * @file    : ReadOnlyListCopy.cs
 * @author  : rudals252
 * @brief   : 모델이 넘겨받은 목록을 복사해 캐스팅으로도 바꿀 수 없는 읽기 전용 보기로 만드는 도우미
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 모델 목록 속성의 불변성을 보장하는 도우미입니다. 원본을 복사하므로 이후 원본을 바꿔도 영향이 없고,
/// 돌려주는 보기는 배열·List로 캐스팅할 수 없으며 IList로 캐스팅해도 변경 시 NotSupportedException이 납니다.
/// </summary>
internal static class ReadOnlyListCopy
{
    /// <summary>
    /// 목록을 복사해 읽기 전용 보기로 돌려준다.
    /// </summary>
    /// <param name="source">원본 목록(null 불가).</param>
    /// <param name="paramName">예외에 표시할 인자 이름.</param>
    /// <returns>복사된 읽기 전용 보기.</returns>
    /// <exception cref="ArgumentNullException">원본이 null인 경우.</exception>
    public static IReadOnlyList<T> Of<T>(IEnumerable<T> source, string paramName)
    {
        ArgumentNullException.ThrowIfNull(source, paramName);
        return Array.AsReadOnly(source.ToArray());
    }
}
