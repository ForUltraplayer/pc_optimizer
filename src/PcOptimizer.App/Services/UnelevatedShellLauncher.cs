/**
 * @file    : UnelevatedShellLauncher.cs
 * @author  : rudals252
 * @brief   : 관리자 권한 앱에서 검증한 웹 주소를 이미 실행 중인 비승격 셸(explorer.exe)에 넘겨 기본 브라우저가 일반 권한으로 열리게 하는 실행기(시작 정보 생성과 프로세스 시작 분리)
 */

// 기본 패키지
using System.Diagnostics;
using System.IO;

namespace PcOptimizer.App.Services;

/// <summary>
/// 검증한 웹 주소를 비승격 셸로 엽니다.
/// </summary>
/// <remarks>
/// 앱은 매니페스트상 항상 관리자 권한이므로 <c>Process.Start(url) { UseShellExecute = true }</c>로 열면 Chrome·Firefox 같은 Win32 브라우저가
/// 관리자 토큰으로 시작됩니다(브라우저 취약점이 곧 관리자 권한 코드 실행, 받은 설치 파일도 UAC 없이 관리자로 실행). 그래서 URL을 실행 파일 이름으로 쓰지 않고
/// Windows 폴더의 explorer.exe를 셸 없이(UseShellExecute=false) 시작하며 주소는 인자 하나로만 넘깁니다. 새 explorer.exe는 이미 실행 중인
/// 사용자 셸(일반 권한)에 요청을 넘기고 끝나므로 기본 브라우저는 로그온 사용자의 일반 권한으로 열립니다.
/// </remarks>
public sealed class UnelevatedShellLauncher
{
    private const string EXPLORER_FILE_NAME = "explorer.exe";

    /// <summary>Windows 폴더의 explorer.exe 전체 경로(PATH·작업 폴더 탐색에 의존하지 않음).</summary>
    public static readonly string EXPLORER_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), EXPLORER_FILE_NAME);

    /// <summary>explorer.exe 작업 폴더(관리자 전용 Windows 폴더, 사용자 쓰기 가능 폴더를 쓰지 않음).</summary>
    private static readonly string EXPLORER_WORKING_DIRECTORY = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private readonly Func<ProcessStartInfo, bool> _processStarter;

    /// <summary>
    /// 실제 프로세스를 시작하는 실행기를 만듭니다.
    /// </summary>
    public UnelevatedShellLauncher()
        : this(StartProcess)
    {
    }

    /// <summary>
    /// 프로세스 시작기를 지정해 실행기를 만듭니다(테스트에서 시작 정보만 확인하기 위함).
    /// </summary>
    /// <param name="processStarter">시작 정보로 프로세스를 시작하고 시작했으면 true를 돌려주는 함수.</param>
    public UnelevatedShellLauncher(Func<ProcessStartInfo, bool> processStarter)
    {
        ArgumentNullException.ThrowIfNull(processStarter);
        _processStarter = processStarter;
    }

    /// <summary>
    /// 비승격 셸로 주소를 여는 시작 정보를 만듭니다(프로세스는 시작하지 않음). 실행 파일은 explorer.exe 전체 경로이고, 주소는 인자 하나로만 들어갑니다.
    /// </summary>
    /// <param name="absoluteUri">링크 정책이 검증한 <see cref="Uri.AbsoluteUri"/>.</param>
    /// <returns>셸 실행을 쓰지 않는 시작 정보.</returns>
    /// <exception cref="ArgumentException">주소가 비었거나 절대 HTTPS 주소가 아닌 경우.</exception>
    public static ProcessStartInfo CreateStartInfo(string absoluteUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absoluteUri);
        if (!Uri.TryCreate(absoluteUri, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("HTTPS 절대 주소만 열 수 있습니다.", nameof(absoluteUri));
        }

        var start = new ProcessStartInfo(EXPLORER_PATH)
        {
            UseShellExecute = false,
            WorkingDirectory = EXPLORER_WORKING_DIRECTORY,
        };
        start.ArgumentList.Add(absoluteUri);
        return start;
    }

    /// <summary>
    /// 검증한 주소를 비승격 셸로 엽니다.
    /// </summary>
    /// <param name="absoluteUri">링크 정책이 검증한 <see cref="Uri.AbsoluteUri"/>.</param>
    /// <exception cref="InvalidOperationException">셸 프로세스를 시작하지 못한 경우.</exception>
    public void Launch(string absoluteUri)
    {
        if (!_processStarter(CreateStartInfo(absoluteUri)))
        {
            throw new InvalidOperationException("비승격 셸을 시작하지 못했습니다.");
        }
    }

    /// <summary>
    /// 시작 정보로 프로세스를 시작한다(기다리지 않음).
    /// </summary>
    private static bool StartProcess(ProcessStartInfo start)
    {
        using var process = Process.Start(start);
        return process is not null;
    }
}
