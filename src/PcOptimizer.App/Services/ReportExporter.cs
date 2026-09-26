/**
 * @file    : ReportExporter.cs
 * @author  : rudals252
 * @brief   : 검사 리포트를 스키마 버전이 있는 JSON으로 내보내며, 기본 내보내기는 모든 문자열 값의 개인 경로·사용자명·PC명을 치환
 */

// 기본 패키지
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.Services;

/// <summary>
/// 검사 리포트 JSON 내보내기입니다. 자동 업로드는 없고 사용자가 고른 경로에만 저장합니다.
/// 기본(익명화) 내보내기는 직렬화한 JSON 트리의 모든 문자열 값을 <see cref="PersonalDataScrubber"/>로 치환하므로
/// 새 필드가 생겨도 치환에서 빠지지 않습니다. 모델에는 SID·장치 일련번호 필드가 없습니다.
/// </summary>
public sealed class ReportExporter
{
    /// <summary>익명화 내보내기 종류 값.</summary>
    public const string EXPORT_KIND_ANONYMIZED = "anonymized";

    /// <summary>내보내기 종류 속성 이름.</summary>
    public const string EXPORT_KIND_PROPERTY = "exportKind";

    private const string FILE_NAME_FORMAT = "pcoptimizer-report-{0:yyyyMMdd-HHmmss}.json";

    private static readonly JsonSerializerOptions SERIALIZER_OPTIONS = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },

        // 한국어 문장을 \uXXXX로 바꾸지 않아 사람이 읽을 수 있게 한다(HTML에 삽입하지 않는 파일 저장 용도).
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly PersonalDataScrubber _scrubber;

    /// <summary>
    /// 내보내기를 만듭니다.
    /// </summary>
    /// <param name="scrubber">개인정보 치환기.</param>
    public ReportExporter(PersonalDataScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(scrubber);
        _scrubber = scrubber;
    }

    /// <summary>
    /// 저장 대화상자에 제안할 파일 이름을 만듭니다(사용자·PC 이름을 넣지 않음).
    /// </summary>
    /// <param name="report">리포트.</param>
    /// <returns>파일 이름.</returns>
    public static string SuggestFileName(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, FILE_NAME_FORMAT, report.StartedAtUtc.UtcDateTime);
    }

    /// <summary>
    /// 리포트를 익명화한 JSON 문자열로 만듭니다.
    /// </summary>
    /// <param name="report">리포트.</param>
    /// <returns>JSON 문자열.</returns>
    public string SerializeAnonymized(ScanReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var root = JsonSerializer.SerializeToNode(report, SERIALIZER_OPTIONS)?.AsObject()
            ?? throw new InvalidOperationException("리포트를 JSON으로 바꾸지 못했습니다.");
        ScrubStrings(root);
        root[EXPORT_KIND_PROPERTY] = EXPORT_KIND_ANONYMIZED;
        return root.ToJsonString(SERIALIZER_OPTIONS);
    }

    /// <summary>
    /// 리포트를 익명화한 JSON 파일로 저장합니다(UTF-8).
    /// </summary>
    /// <param name="report">리포트.</param>
    /// <param name="path">사용자가 고른 저장 경로.</param>
    /// <param name="ct">취소 토큰.</param>
    /// <returns>저장 작업.</returns>
    public async Task ExportAnonymizedAsync(ScanReport report, string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var json = SerializeAnonymized(report);
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct).ConfigureAwait(false);
    }

    /// <summary>
    /// JSON 트리의 모든 문자열 값을 치환한다(속성 이름은 모델 고정 이름이므로 그대로 둔다).
    /// </summary>
    private void ScrubStrings(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(pair => pair.Key).ToList())
                {
                    if (obj[key] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        obj[key] = _scrubber.Scrub(text);
                    }
                    else
                    {
                        ScrubStrings(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue value && value.TryGetValue<string>(out var text))
                    {
                        array[index] = _scrubber.Scrub(text);
                    }
                    else
                    {
                        ScrubStrings(array[index]);
                    }
                }

                break;
        }
    }
}
