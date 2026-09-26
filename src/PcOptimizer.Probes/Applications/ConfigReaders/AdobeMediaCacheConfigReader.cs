/**
 * @file    : AdobeMediaCacheConfigReader.cs
 * @author  : rudals252
 * @brief   : Adobe 미디어 캐시 위치 리더. Premiere Pro·Media Encoder 환경설정 파일 형식을 검증할 수 없어 파일을 읽지 않고 항상 "형식 미검증(기본 위치만)"을 돌려준다
 */

namespace PcOptimizer.Probes.Applications.ConfigReaders;

/// <summary>
/// Adobe 설정 리더입니다(스펙 §5.1 "Adobe의 검증되지 않은 바이너리 환경설정은 추측하지 않는다").
/// </summary>
/// <remarks>
/// 환경설정 파일은 버전별 프로필 폴더(문서 폴더 아래, 보호 루트)에 있고, 이 작업에서 형식 문서를 대조해 검증하지 못했습니다.
/// 따라서 어떤 파일도 열지 않고 기본 위치(%AppData%\Adobe\Common)만 확인했다는 범위를 알립니다.
/// </remarks>
public sealed class AdobeMediaCacheConfigReader : IAppConfigReader
{
    /// <summary>리더 이름.</summary>
    public const string APP = "adobe";

    /// <inheritdoc />
    public string App => APP;

    /// <inheritdoc />
    public AppConfigReading Read()
    {
        return new AppConfigReading(APP, AppConfigReadState.CannotVerify, AppConfigValueOrigin.None, []);
    }
}
