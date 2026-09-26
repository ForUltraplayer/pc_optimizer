/**
 * @file    : Winapp2RuleBuilder.cs
 * @author  : rudals252
 * @brief   : winapp2 섹션 하나의 키 줄을 모아 정리 규칙으로 만드는 내부 빌더(키별 해석, 첫 미지원 사유 기록 후 나머지 무시, 탐지 없음·레지스트리 전용 최종 검사, 미지원이면 관측 대상 비움)
 */

// 기본 패키지
using System.Text.RegularExpressions;

namespace PcOptimizer.Core.Cleaning;

/// <summary>
/// 섹션 하나의 규칙 빌더입니다. 첫 번째 미지원 사유만 기록하고 이후 줄은 무시합니다.
/// </summary>
internal sealed partial class Winapp2RuleBuilder(string name, RuleOrigin origin)
{
    private const string KEY_LANG_SEC_REF = "LangSecRef";
    private const string KEY_SECTION = "Section";
    private const string KEY_DETECT = "Detect";
    private const string KEY_DETECT_FILE = "DetectFile";
    private const string KEY_DETECT_OS = "DetectOS";
    private const string KEY_SPECIAL_DETECT = "SpecialDetect";
    private const string KEY_DEFAULT = "Default";
    private const string KEY_WARNING = "Warning";
    private const string KEY_FILE_KEY = "FileKey";
    private const string KEY_EXCLUDE_KEY = "ExcludeKey";
    private const string KEY_REG_KEY = "RegKey";
    private const string FLAG_RECURSE = "RECURSE";
    private const string FLAG_REMOVE_SELF = "REMOVESELF";
    private const string EXCLUDE_FILE = "FILE";
    private const string EXCLUDE_PATH = "PATH";
    private const char KEY_SEPARATOR = '=';
    private const char FIELD_SEPARATOR = '|';
    private const char PATTERN_SEPARATOR = ';';
    private const char REGISTRY_SEPARATOR = '\\';
    private const int FILE_KEY_MIN_FIELDS = 2;
    private const int FILE_KEY_MAX_FIELDS = 3;
    private const int EXCLUDE_KEY_FIELDS = 3;
    private const int FLAG_FIELD_INDEX = 2;
    private const int REGEX_TIMEOUT_MILLISECONDS = 1000;
    private const string GROUP_NAME = "name";

    /// <summary>삭제 의미의 특수 지시어(패턴 자리에 오는 토큰). 조회로 옮길 수 없어 규칙을 건너뜁니다.</summary>
    private static readonly HashSet<string> DIRECTIVES = new(["RemoveEmptyFoldersOnly"], StringComparer.OrdinalIgnoreCase);

    /// <summary>지원 레지스트리 루트 이름(짧은 이름·긴 이름).</summary>
    private static readonly Dictionary<string, RuleRegistryHive> HIVES = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HKCU"] = RuleRegistryHive.CurrentUser,
        ["HKEY_CURRENT_USER"] = RuleRegistryHive.CurrentUser,
        ["HKLM"] = RuleRegistryHive.LocalMachine,
        ["HKEY_LOCAL_MACHINE"] = RuleRegistryHive.LocalMachine,
        ["HKCR"] = RuleRegistryHive.ClassesRoot,
        ["HKEY_CLASSES_ROOT"] = RuleRegistryHive.ClassesRoot,
    };

    private static readonly char[] PATH_SEPARATORS = ['\\', '/'];

    private readonly List<RegistryDetectSpec> _detectKeys = [];
    private readonly List<string> _detectFiles = [];
    private readonly List<FileKeySpec> _fileKeys = [];
    private readonly List<ExcludeKeySpec> _excludeKeys = [];
    private string? _section;
    private string? _langSecRef;
    private int _regKeyCount;
    private bool _hasWarning;
    private UnsupportedRuleReason? _reason;
    private string? _detail;

    /// <summary>
    /// 미지원 사유를 기록한다(처음 한 번만).
    /// </summary>
    public void Fail(UnsupportedRuleReason reason, string? detail)
    {
        if (_reason is null)
        {
            _reason = reason;
            _detail = detail;
        }
    }

    /// <summary>
    /// "키=값" 줄 하나를 해석한다. 이미 미지원이면 RegKey 개수만 세고 나머지는 무시한다.
    /// </summary>
    public void AddLine(string line)
    {
        var separator = line.IndexOf(KEY_SEPARATOR);
        var match = separator > 0 ? KeyPattern().Match(line[..separator].Trim()) : Match.Empty;
        if (_reason is not null)
        {
            if (match.Success && Is(match.Groups[GROUP_NAME].Value, KEY_REG_KEY))
            {
                _regKeyCount++;
            }

            return;
        }

        if (separator <= 0)
        {
            Fail(UnsupportedRuleReason.MalformedEntry, null);
            return;
        }

        var value = line[(separator + 1)..].Trim();
        if (!match.Success)
        {
            Fail(UnsupportedRuleReason.UnknownKey, null);
            return;
        }

        AddKey(match.Groups[GROUP_NAME].Value, value);
    }

    /// <summary>
    /// 규칙을 만든다. 미지원이면 관측 대상을 모두 비운다(일부 적용 방지).
    /// </summary>
    public CleaningRule Build()
    {
        if (_reason is null && _detectKeys.Count == 0 && _detectFiles.Count == 0)
        {
            Fail(UnsupportedRuleReason.NoSafeDetection, null);
        }

        if (_reason is null && _fileKeys.Count == 0)
        {
            Fail(_regKeyCount > 0 ? UnsupportedRuleReason.RegistryOnly : UnsupportedRuleReason.MalformedEntry, _regKeyCount > 0 ? null : KEY_FILE_KEY);
        }

        var supported = _reason is null;
        return new CleaningRule(
            CleaningRule.CreateId(origin, name),
            name,
            origin,
            _section ?? _langSecRef,
            supported ? _detectKeys.AsReadOnly() : [],
            supported ? _detectFiles.AsReadOnly() : [],
            supported ? _fileKeys.AsReadOnly() : [],
            supported ? _excludeKeys.AsReadOnly() : [],
            _regKeyCount,
            _hasWarning,
            _reason,
            _detail);
    }

    /// <summary>
    /// 번호를 뗀 키 이름별로 값을 해석한다.
    /// </summary>
    private void AddKey(string key, string value)
    {
        if (value.Length == 0)
        {
            Fail(UnsupportedRuleReason.MalformedEntry, key);
            return;
        }

        switch (key)
        {
            case var k when Is(k, KEY_LANG_SEC_REF):
                _langSecRef ??= value;
                break;
            case var k when Is(k, KEY_SECTION):
                _section ??= value;
                break;
            case var k when Is(k, KEY_DEFAULT):
                break;
            case var k when Is(k, KEY_WARNING):
                _hasWarning = true;
                break;
            case var k when Is(k, KEY_DETECT):
                AddDetect(value);
                break;
            case var k when Is(k, KEY_DETECT_FILE):
                AddDetectFile(value);
                break;
            case var k when Is(k, KEY_DETECT_OS):
                Fail(UnsupportedRuleReason.DetectOs, KEY_DETECT_OS);
                break;
            case var k when Is(k, KEY_SPECIAL_DETECT):
                Fail(UnsupportedRuleReason.SpecialDetect, KEY_SPECIAL_DETECT);
                break;
            case var k when Is(k, KEY_FILE_KEY):
                AddFileKey(value);
                break;
            case var k when Is(k, KEY_EXCLUDE_KEY):
                AddExcludeKey(value);
                break;
            case var k when Is(k, KEY_REG_KEY):
                _regKeyCount++;
                break;
            default:
                Fail(UnsupportedRuleReason.UnknownKey, key);
                break;
        }
    }

    /// <summary>
    /// 레지스트리 탐지 키: 지원 루트만 받는다.
    /// </summary>
    private void AddDetect(string value)
    {
        var separator = value.IndexOf(REGISTRY_SEPARATOR);
        var root = separator < 0 ? value : value[..separator];
        var subKey = separator < 0 ? string.Empty : value[(separator + 1)..].Trim(REGISTRY_SEPARATOR);
        if (!HIVES.TryGetValue(root, out var hive))
        {
            Fail(UnsupportedRuleReason.UnsupportedDetectRoot, root);
            return;
        }

        if (subKey.Length == 0)
        {
            Fail(UnsupportedRuleReason.MalformedEntry, KEY_DETECT);
            return;
        }

        _detectKeys.Add(new RegistryDetectSpec(hive, subKey));
    }

    /// <summary>
    /// 파일·폴더 탐지 경로.
    /// </summary>
    private void AddDetectFile(string value)
    {
        var template = Winapp2PathSyntax.TrimTrailingSeparators(value);
        if (Check(template, KEY_DETECT_FILE))
        {
            _detectFiles.Add(template);
        }
    }

    /// <summary>
    /// FileKey: 경로|패턴[;패턴…][|RECURSE|REMOVESELF].
    /// </summary>
    private void AddFileKey(string value)
    {
        var fields = value.Split(FIELD_SEPARATOR);
        if (fields.Length < FILE_KEY_MIN_FIELDS || fields.Length > FILE_KEY_MAX_FIELDS)
        {
            Fail(UnsupportedRuleReason.MalformedEntry, KEY_FILE_KEY);
            return;
        }

        var flag = fields.Length == FILE_KEY_MAX_FIELDS ? fields[FLAG_FIELD_INDEX].Trim() : string.Empty;
        var removeSelf = string.Equals(flag, FLAG_REMOVE_SELF, StringComparison.OrdinalIgnoreCase);
        var recurse = removeSelf || string.Equals(flag, FLAG_RECURSE, StringComparison.OrdinalIgnoreCase);
        if (flag.Length > 0 && !recurse)
        {
            Fail(UnsupportedRuleReason.UnsupportedDirective, flag);
            return;
        }

        var template = Winapp2PathSyntax.TrimTrailingSeparators(fields[0]);
        if (!Check(template, KEY_FILE_KEY) || !TryPatterns(fields[1], KEY_FILE_KEY, out var patterns))
        {
            return;
        }

        _fileKeys.Add(new FileKeySpec(template, patterns, recurse, removeSelf));
    }

    /// <summary>
    /// ExcludeKey: FILE|경로|패턴 또는 PATH|경로|패턴. 그 밖의 종류(REG 등)는 규칙을 건너뛴다.
    /// </summary>
    private void AddExcludeKey(string value)
    {
        var fields = value.Split(FIELD_SEPARATOR);
        var kindText = fields[0].Trim();
        ExcludeKind? kind = kindText switch
        {
            var k when string.Equals(k, EXCLUDE_FILE, StringComparison.OrdinalIgnoreCase) => ExcludeKind.File,
            var k when string.Equals(k, EXCLUDE_PATH, StringComparison.OrdinalIgnoreCase) => ExcludeKind.Path,
            _ => null,
        };
        if (kind is null || fields.Length != EXCLUDE_KEY_FIELDS)
        {
            Fail(UnsupportedRuleReason.UnsupportedExclude, kindText);
            return;
        }

        var template = Winapp2PathSyntax.TrimTrailingSeparators(fields[1]);
        if (!Check(template, KEY_EXCLUDE_KEY) || !TryPatterns(fields[2], KEY_EXCLUDE_KEY, out var patterns))
        {
            return;
        }

        _excludeKeys.Add(new ExcludeKeySpec(kind.Value, template, patterns));
    }

    /// <summary>
    /// 경로 템플릿을 정적으로 검사하고 문제가 있으면 기록한다.
    /// </summary>
    private bool Check(string template, string keyName)
    {
        var check = Winapp2PathSyntax.Check(template, keyName);
        if (!check.IsOk)
        {
            Fail(check.Reason!.Value, check.Detail);
        }

        return check.IsOk;
    }

    /// <summary>
    /// 세미콜론으로 나눈 파일 이름 패턴을 검사한다(비어 있거나 경로 구분자가 있으면 형식 오류, 삭제 지시어는 미지원).
    /// </summary>
    private bool TryPatterns(string field, string keyName, out IReadOnlyList<string> patterns)
    {
        var list = field.Split(PATTERN_SEPARATOR, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        patterns = list;
        if (list.Length == 0 || list.Any(pattern => pattern.IndexOfAny(PATH_SEPARATORS) >= 0))
        {
            Fail(UnsupportedRuleReason.MalformedEntry, keyName);
            return false;
        }

        var directive = list.FirstOrDefault(DIRECTIVES.Contains);
        if (directive is not null)
        {
            Fail(UnsupportedRuleReason.UnsupportedDirective, directive);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 키 이름 비교(대소문자 무시).
    /// </summary>
    private static bool Is(string key, string expected)
    {
        return string.Equals(key, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 키 이름과 끝 번호(선택).
    /// </summary>
    [GeneratedRegex(@"^(?<name>[A-Za-z]+)[0-9]*$", RegexOptions.CultureInvariant, REGEX_TIMEOUT_MILLISECONDS)]
    private static partial Regex KeyPattern();
}
