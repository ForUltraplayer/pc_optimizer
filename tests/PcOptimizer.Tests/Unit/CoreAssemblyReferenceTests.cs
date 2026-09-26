/**
 * @file    : CoreAssemblyReferenceTests.cs
 * @author  : rudals252
 * @brief   : PcOptimizer.Core 어셈블리가 Windows 전용 어셈블리를 참조하지 않는지 검증하는 아키텍처 테스트
 */

// 기본 패키지
using System.Reflection;

namespace PcOptimizer.Tests.Unit;

/// <summary>
/// PcOptimizer.Core는 Windows API·파일 시스템·네트워크 I/O에 의존하지 않아야 한다는
/// 솔루션 아키텍처 규칙(공통 제약)을 검증하는 테스트입니다.
/// </summary>
public class CoreAssemblyReferenceTests
{
    /// <summary>
    /// Core가 참조해서는 안 되는 Windows 전용 어셈블리 이름 목록입니다.
    /// </summary>
    private static readonly string[] FORBIDDEN_ASSEMBLY_NAMES =
    [
        "System.Management",
        "WindowsBase",
        "PresentationFramework",
        "PresentationCore",
        "System.Windows.Forms",
        "Microsoft.Win32.Registry",
    ];

    /// <summary>
    /// PcOptimizer.Core.dll을 로드하여 참조 어셈블리 목록에 금지된 Windows 전용
    /// 어셈블리가 포함되어 있지 않은지 확인한다. P0 완료 조건인
    /// "Core에는 Windows 패키지 참조가 없다"를 직접 검증한다.
    /// </summary>
    [Fact]
    public void Core어셈블리는_Windows전용_어셈블리를_참조하지_않는다()
    {
        var coreAssembly = Assembly.Load("PcOptimizer.Core");

        var referencedAssemblyNames = coreAssembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .Where(name => name is not null)
            .ToArray();

        var forbiddenReferencesFound = referencedAssemblyNames
            .Where(name => FORBIDDEN_ASSEMBLY_NAMES.Contains(name))
            .ToArray();

        Assert.True(
            forbiddenReferencesFound.Length == 0,
            $"PcOptimizer.Core가 금지된 Windows 전용 어셈블리를 참조합니다: {string.Join(", ", forbiddenReferencesFound)}");
    }
}
