/**
 * @file    : RuleDetector.cs
 * @author  : rudals252
 * @brief   : 지원 규칙의 Detect(레지스트리 키 존재: HKCU, HKLM 64/32비트, HKCR은 사용자·컴퓨터 Classes)와 DetectFile(파일·폴더 존재, 와일드카드는 제한된 열거)을 OR로 평가해 탐지됨/안 됨/확인 불가를 정하고 검사 ID별로 캐시하는 탐지기(앱 실행·쓰기 없음)
 */

// 기본 패키지
using Microsoft.Win32;

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Storage;

namespace PcOptimizer.Probes.Applications;

/// <summary>
/// 규칙 탐지 결과입니다.
/// </summary>
public enum DetectionState
{
    /// <summary>모든 탐지 조건을 확인했고 맞는 것이 없음(기본값이 탐지됨이 되지 않도록 첫 값).</summary>
    NotDetected,

    /// <summary>탐지 조건 중 하나 이상이 맞음.</summary>
    Detected,

    /// <summary>맞는 조건이 없고, 확인하지 못한 조건(접근 거부·보호 위치 와일드카드·한도 초과·해석 실패)이 있음.</summary>
    Unknown,
}

/// <summary>
/// 규칙 탐지기입니다(스펙 §5.1 "레지스트리·파일 탐지는 Probes가 수행한다", "안전한 탐지 근거가 없는 항목은 모든 사용자에게 적용하지 않는다").
/// </summary>
/// <remarks>
/// 레지스트리는 키 존재만 보고(하위 키 이름 조회), 값 내용은 읽지 않습니다. 파일은 속성만 확인하며 와일드카드는 부모 폴더를 한도 안에서 열거합니다.
/// 보호 루트 안의 와일드카드는 열거하지 않고(확인 불가), 와일드카드 없는 경로의 존재 확인은 속성 조회 한 번이므로 보호 루트 안에서도 합니다.
/// </remarks>
public sealed class RuleDetector
{
    private const string CLASSES_SUB_KEY = @"Software\Classes\";

    private readonly IRegistryReader _registry;
    private readonly IDirectoryEntrySource _source;
    private readonly Winapp2PathResolver _resolver;
    private readonly PathPatternExpander _expander;
    private readonly Lock _cacheLock = new();
    private Guid _cachedScanId;
    private IReadOnlyDictionary<string, DetectionState>? _cached;

    /// <summary>
    /// 탐지기를 만듭니다.
    /// </summary>
    /// <param name="registry">레지스트리 읽기.</param>
    /// <param name="source">디렉터리 항목 열거.</param>
    /// <param name="resolver">경로 변수 해석기.</param>
    public RuleDetector(IRegistryReader registry, IDirectoryEntrySource source, Winapp2PathResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resolver);
        _registry = registry;
        _source = source;
        _resolver = resolver;
        _expander = new PathPatternExpander(source);
    }

    /// <summary>
    /// 지원 규칙을 탐지합니다. 같은 검사 ID로 다시 부르면 캐시한 결과를 돌려줍니다.
    /// </summary>
    /// <param name="scanId">검사 ID.</param>
    /// <param name="rules">규칙(미지원 규칙은 건너뜀).</param>
    /// <param name="isProtected">보호 루트 확인 함수.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>규칙 ID별 탐지 결과(지원 규칙만).</returns>
    public IReadOnlyDictionary<string, DetectionState> Detect(Guid scanId, IReadOnlyList<CleaningRule> rules, Func<string, bool> isProtected, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(isProtected);
        lock (_cacheLock)
        {
            if (_cached is not null && _cachedScanId == scanId)
            {
                return _cached;
            }
        }

        var result = new Dictionary<string, DetectionState>(StringComparer.Ordinal);
        foreach (var rule in rules.Where(rule => rule.IsSupported))
        {
            ct.ThrowIfCancellationRequested();
            result[rule.Id] = DetectRule(rule, isProtected, ct);
        }

        lock (_cacheLock)
        {
            _cachedScanId = scanId;
            _cached = result;
        }

        return result;
    }

    /// <summary>
    /// 규칙 하나: 조건 중 하나라도 맞으면 탐지됨, 아니면 확인 못한 조건이 있으면 확인 불가.
    /// </summary>
    private DetectionState DetectRule(CleaningRule rule, Func<string, bool> isProtected, CancellationToken ct)
    {
        var unknown = false;
        foreach (var key in rule.DetectKeys)
        {
            switch (KeyExists(key))
            {
                case DetectionState.Detected:
                    return DetectionState.Detected;
                case DetectionState.Unknown:
                    unknown = true;
                    break;
            }
        }

        foreach (var template in rule.DetectFiles)
        {
            switch (FileExists(template, isProtected, ct))
            {
                case DetectionState.Detected:
                    return DetectionState.Detected;
                case DetectionState.Unknown:
                    unknown = true;
                    break;
            }
        }

        return unknown ? DetectionState.Unknown : DetectionState.NotDetected;
    }

    /// <summary>
    /// 레지스트리 키 존재 확인. HKLM은 64·32비트 보기, HKCR은 사용자·컴퓨터 Software\Classes를 모두 본다.
    /// </summary>
    private DetectionState KeyExists(RegistryDetectSpec key)
    {
        (RegistryRoot Root, RegistryView View, string SubKey)[] locations = key.Hive switch
        {
            RuleRegistryHive.CurrentUser => [(RegistryRoot.CurrentUser, RegistryView.Default, key.SubKey)],
            RuleRegistryHive.LocalMachine => [(RegistryRoot.LocalMachine, RegistryView.Registry64, key.SubKey), (RegistryRoot.LocalMachine, RegistryView.Registry32, key.SubKey)],
            _ =>
            [
                (RegistryRoot.CurrentUser, RegistryView.Default, CLASSES_SUB_KEY + key.SubKey),
                (RegistryRoot.LocalMachine, RegistryView.Registry64, CLASSES_SUB_KEY + key.SubKey),
                (RegistryRoot.LocalMachine, RegistryView.Registry32, CLASSES_SUB_KEY + key.SubKey),
            ],
        };

        var unknown = false;
        foreach (var (root, view, subKey) in locations)
        {
            switch (_registry.ReadSubKeyNames(root, view, subKey).Status)
            {
                case RegistryReadStatus.Found:
                    return DetectionState.Detected;
                case RegistryReadStatus.KeyMissing:
                    break;
                default:
                    unknown = true;
                    break;
            }
        }

        return unknown ? DetectionState.Unknown : DetectionState.NotDetected;
    }

    /// <summary>
    /// 파일·폴더 존재 확인. 변수를 펼친 위치 중 하나라도 있으면 탐지됨.
    /// </summary>
    private DetectionState FileExists(string template, Func<string, bool> isProtected, CancellationToken ct)
    {
        var resolution = _resolver.Resolve(template);
        if (!resolution.IsResolved)
        {
            return DetectionState.Unknown;
        }

        var unknown = false;
        foreach (var path in resolution.Paths)
        {
            var expansion = _expander.Expand(path, directoriesOnly: false, isProtected, ct);
            unknown |= expansion.Exceeded || expansion.Incomplete || expansion.ProtectedSkipped;
            foreach (var match in expansion.Matches)
            {
                switch (_source.ProbeRoot(match))
                {
                    case RootPresence.Directory:
                    case RootPresence.NotDirectory:
                    case RootPresence.ReparsePoint:
                        return DetectionState.Detected;
                    case RootPresence.Missing:
                        break;
                    default:
                        unknown = true;
                        break;
                }
            }
        }

        return unknown ? DetectionState.Unknown : DetectionState.NotDetected;
    }
}
