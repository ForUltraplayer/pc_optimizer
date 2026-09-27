/**
 * @file    : VideoEditorCacheRule.cs
 * @author  : rudals252
 * @brief   : 영상 편집 캐시 후보의 관측 크기·불완전 범위·앱 내 정리 방법을 정보 카드로 표시
 */
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Rules;

/// <summary>관측 용량을 확보 가능 용량이나 삭제 승인으로 바꾸지 않습니다.</summary>
public sealed class VideoEditorCacheRule : IRule
{
    /// <summary>등록 규칙 식별자입니다.</summary>
    public string Id => "video-editor.cache";
    /// <summary>앱 캐시 프로브 안의 별도 측정 이름입니다.</summary>
    public static string Field(string app, string field) => AppCacheProbeContract.MEASUREMENT_PREFIX + "videoCache." + app + "." + field;
    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        var result = new List<Finding>();
        foreach (var app in new[] { "davinci", "capcut" })
        {
            string? Text(string field) => SnapshotValues.Text(snapshot, AppCacheProbeContract.PROBE_ID, Field(app, field));
            var state = Text("state"); if (state is null) { continue; }
            var name = app == "davinci" ? "DaVinci Resolve" : "CapCut";
            var bytes = snapshot.GetMeasurement(AppCacheProbeContract.PROBE_ID, Field(app, "bytes"))?.Value as IntegerValue;
            var observed = state is "Observed" or "Partial" && bytes is { Value: >= 0 };
            var summary = observed ? $"{name} 캐시 후보: {Size(bytes!.Value)}" + (state == "Partial" ? " (일부 관측)" : "")
                : state == "Absent" ? $"{name}의 확인한 위치에 캐시 폴더가 없습니다" : $"{name} 캐시 크기를 확인하지 못했습니다";
            var guide = app == "davinci"
                ? "Resolve에서 Project Settings > Master Settings > Working Folders의 Cache files location을 확인하세요. Playback > Delete Render Cache에서 Unused 또는 Selected Clips를 선택할 수 있습니다. 삭제하면 필요할 때 다시 렌더링합니다. 프로젝트별 경로와 원본 미디어가 사용 가능한지 먼저 확인하세요."
                : "CapCut에서 Settings > Draft > Clear Cache(캐시 크기 옆 휴지통)를 확인하세요. 메뉴는 버전에 따라 다릅니다. Cache 안에는 다운로드 리소스·미디어 사본이 포함될 수 있으므로 프로젝트와 원본을 확보하고 앱 자체 정리를 사용하세요.";
            var probe = snapshot.ProbeResults.FirstOrDefault(p => p.ProbeId == AppCacheProbeContract.PROBE_ID);
            var measured = probe?.Measurements.Where(m => m.Name.StartsWith(Field(app, ""), StringComparison.Ordinal)).ToArray() ?? [];
            result.Add(new("video-editor-cache:" + app, FindingCategory.AppCache, summary, measured,
                "캐시 파일의 내용은 읽지 않고 후보 폴더의 논리 크기만 셌습니다. 하드링크는 중복될 수 있고 삭제 가능량은 아닙니다.",
                observed || state == "Absent" ? Verdict.Info : Verdict.CannotVerify,
                observed || state == "Absent" ? null : state == "TimedOut" ? CannotVerifyReason.Timeout : state is "Unreadable" or "AccessDenied" ? CannotVerifyReason.AccessDenied : CannotVerifyReason.Unsupported,
                (Text("scope") ?? "") + "\n상태: " + state + "\n" + guide, null,
                new Impact("캐시가 차지하는 위치와 크기를 확인할 수 있습니다.", "자동 삭제 대상이 아닙니다. 앱 정리 뒤 캐시 재생성·재다운로드가 필요할 수 있습니다."), [new ShowDetailsAction()]));
        }
        return result;
    }

    private static string Size(long bytes) => bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024d * 1024 * 1024):0.##} GiB"
        : bytes >= 1024 * 1024 ? $"{bytes / (1024d * 1024):0.##} MiB"
        : bytes >= 1024 ? $"{bytes / 1024d:0.##} KiB" : $"{bytes} bytes";
}
