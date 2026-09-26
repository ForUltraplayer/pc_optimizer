/**
 * @file    : PathTemplate.cs
 * @author  : rudals252
 * @brief   : %이름% 환경 변수가 든 경로 템플릿을 IPathEnvironment로 펼쳐 정규화하는 도우미(하나라도 해석되지 않거나 드라이브 절대 경로가 아니면 null)
 */

// 기본 패키지
using System.Text;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;

namespace PcOptimizer.Probes.Storage;

/// <summary>
/// 경로 템플릿 해석 도우미입니다. 해석되지 않은 변수를 남긴 채 넓은 경로로 바뀌는 일을 막기 위해 실패를 null로 알립니다(스펙 §5.1).
/// </summary>
internal static class PathTemplate
{
    private const char ENVIRONMENT_MARK = '%';

    /// <summary>
    /// 템플릿의 %이름%을 펼치고 정규화합니다.
    /// </summary>
    /// <param name="template">경로 템플릿.</param>
    /// <param name="environment">환경.</param>
    /// <returns>정규화된 드라이브 절대 경로 또는 null.</returns>
    public static string? Resolve(string template, IPathEnvironment environment)
    {
        var expanded = Expand(template, environment);
        return expanded is null ? null : NormalizeAbsolute(expanded, environment);
    }

    /// <summary>
    /// 템플릿의 %이름%만 펼칩니다(정규화·파일 시스템 접근 없음).
    /// </summary>
    /// <param name="template">경로 템플릿.</param>
    /// <param name="environment">환경.</param>
    /// <returns>펼친 문자열 또는 null(해석되지 않은 변수).</returns>
    public static string? ExpandOnly(string template, IPathEnvironment environment)
    {
        return Expand(template, environment);
    }

    /// <summary>
    /// 드라이브 절대 경로만 정규화합니다(UNC·장치·상대 경로·드라이브 루트 전체는 null).
    /// </summary>
    /// <param name="path">경로.</param>
    /// <param name="environment">환경.</param>
    /// <returns>정규화 경로 또는 null.</returns>
    public static string? NormalizeAbsolute(string path, IPathEnvironment environment)
    {
        var trimmed = path.Trim().Trim('"');
        if (!PathScope.IsDriveAbsolute(trimmed) || PathScope.IsDriveRoot(trimmed))
        {
            return null;
        }

        var normalized = environment.NormalizePath(trimmed);
        return PathScope.IsDriveAbsolute(normalized) && !PathScope.IsDriveRoot(normalized) ? normalized : null;
    }

    /// <summary>
    /// %이름%을 환경 값으로 바꾼다. 닫히지 않은 %나 없는 변수가 있으면 null.
    /// </summary>
    private static string? Expand(string template, IPathEnvironment environment)
    {
        var builder = new StringBuilder();
        var index = 0;
        while (index < template.Length)
        {
            var start = template.IndexOf(ENVIRONMENT_MARK, index);
            if (start < 0)
            {
                builder.Append(template, index, template.Length - index);
                break;
            }

            var end = template.IndexOf(ENVIRONMENT_MARK, start + 1);
            if (end <= start + 1)
            {
                return null;
            }

            builder.Append(template, index, start - index);
            var value = environment.GetEnvironmentVariable(template[(start + 1)..end]);
            if (value is null)
            {
                return null;
            }

            builder.Append(value);
            index = end + 1;
        }

        return builder.ToString();
    }
}
