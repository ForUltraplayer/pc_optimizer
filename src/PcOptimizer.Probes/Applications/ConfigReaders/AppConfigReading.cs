/**
 * @file    : AppConfigReading.cs
 * @author  : rudals252
 * @brief   : 앱 설정 리더 계약과 결과(설정 없음·설정 경로·해석 불가·읽기 실패·형식 미검증 상태, 값 출처, 설정 경로 목록). 결과에는 지정한 키의 경로 값만 담고 설정 파일의 다른 내용(인증 토큰 등)은 담지 않는다
 */

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// 앱 설정 리더가 읽은 상태입니다.
/// </summary>
public enum AppConfigReadState
{
    /// <summary>환경 변수·사용자 설정에 캐시 위치 설정이 없음(기본 위치만).</summary>
    NotConfigured,

    /// <summary>캐시 위치 설정을 읽음.</summary>
    Configured,

    /// <summary>설정 값을 해석하지 못함(상대 경로·변수 참조·형식 오류).</summary>
    Invalid,

    /// <summary>설정 파일이 있지만 읽지 못함.</summary>
    Unreadable,

    /// <summary>설정 형식을 검증할 수 없어 읽지 않음(Adobe).</summary>
    CannotVerify,
}

/// <summary>
/// 앱 설정 값의 출처입니다.
/// </summary>
public enum AppConfigValueOrigin
{
    /// <summary>없음.</summary>
    None,

    /// <summary>환경 변수.</summary>
    Environment,

    /// <summary>사용자 설정 파일.</summary>
    UserFile,

    /// <summary>레지스트리.</summary>
    Registry,
}

/// <summary>
/// 앱 설정 리더 결과입니다. 경로는 관측할 폴더 절대 경로이며 설정 파일의 원문·다른 키 값은 담지 않습니다.
/// </summary>
/// <param name="App">리더 이름(npm·pip·nuget·steam·adobe).</param>
/// <param name="State">읽기 상태.</param>
/// <param name="Origin">값 출처.</param>
/// <param name="Paths">설정에서 얻은 관측 폴더 경로(Configured일 때만).</param>
/// <param name="LibraryCount">Steam 라이브러리 수(Steam만).</param>
public sealed record AppConfigReading(string App, AppConfigReadState State, AppConfigValueOrigin Origin, IReadOnlyList<string> Paths, int? LibraryCount = null)
{
    /// <summary>
    /// 설정 없음 결과를 만듭니다.
    /// </summary>
    /// <param name="app">리더 이름.</param>
    /// <returns>결과.</returns>
    public static AppConfigReading NotConfigured(string app)
    {
        return new AppConfigReading(app, AppConfigReadState.NotConfigured, AppConfigValueOrigin.None, []);
    }
}

/// <summary>
/// 앱 설정 리더 계약입니다. 지정한 키만 읽고, 앱이나 스크립트를 실행하지 않습니다.
/// </summary>
public interface IAppConfigReader
{
    /// <summary>리더 이름(rule-metadata.json의 configReader 값).</summary>
    string App { get; }

    /// <summary>
    /// 설정을 읽습니다.
    /// </summary>
    /// <returns>결과.</returns>
    AppConfigReading Read();
}
