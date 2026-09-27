/**
 * @file    : ShaderCacheRule.cs
 * @author  : rudals252
 * @brief   : 라이브러리별·그래픽 캐시별 관측 범위와 크기를 분리하여 정보 카드로 표시
 */
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Core.Rules;

/// <summary>셰이더 캐시를 미사용 파일 또는 확보 가능 용량으로 판단하지 않습니다.</summary>
public sealed class ShaderCacheRule : IRule
{
    /// <summary>설치 라이브러리 한 곳과 VDF 최대 64개를 합한 상한입니다.</summary>
    public const int MaxRows = 65;
    /// <inheritdoc />
    public string Id => "shader-cache.locations";
    /// <summary>측정 이름 접두사입니다. 기존 기본 내보내기 경로 익명화에 포함됩니다.</summary>
    public static string Prefix(string group) => "appCache.shaderCache." + group + ".";
    /// <summary>위치 개수 측정값입니다.</summary>
    public static string CountField(string group) => Prefix(group) + "count";
    /// <summary>위치별 측정값 이름입니다.</summary>
    public static string Field(string group, int index, string key) => Prefix(group) + index + "." + key;
    /// <summary>새 상세 관측이 있는 그룹인지 확인합니다.</summary>
    public static bool HasGroup(ScanSnapshot snapshot, string group) => SnapshotValues.Integer(snapshot, AppCacheProbeContract.PROBE_ID, CountField(group)) is > 0 and <= MaxRows;
    /// <inheritdoc />
    public IReadOnlyList<Finding> Evaluate(ScanSnapshot snapshot)
    {
        var output = new List<Finding>();
        if (!snapshot.TryGetProbe(AppCacheProbeContract.PROBE_ID, out var probe) || probe.Status is not (ProbeStatus.Success or ProbeStatus.Partial)) { return output; }
        foreach (var group in new[] { "steam", "graphics" })
        {
            if (!HasGroup(snapshot, group)) { continue; }
            var count = (int)SnapshotValues.Integer(snapshot, AppCacheProbeContract.PROBE_ID, CountField(group))!;
            var lines = new List<string>(); var unknown = 0; var partial = false; var observed = 0; long total = 0;
            for (var i = 0; i < count; i++)
            {
                string? Text(string key) => SnapshotValues.Text(snapshot, AppCacheProbeContract.PROBE_ID, Field(group, i, key));
                var state = Text("state"); var bytes = SnapshotValues.Integer(snapshot, AppCacheProbeContract.PROBE_ID, Field(group, i, "bytes"));
                if (state is "Observed" or "Partial" && bytes is >= 0 && total <= long.MaxValue - bytes.Value)
                { total += bytes.Value; observed++; partial |= state == "Partial"; lines.Add($"{Text("label")}: {ByteSizeText.Format(bytes.Value)}" + (state == "Partial" ? " (일부 관측)" : "")); }
                else if (state == "Absent") { lines.Add($"{Text("label")}: 캐시 폴더 없음"); }
                else { unknown++; lines.Add($"{Text("label")}: 확인 불가 ({Reason(state)})"); }
            }
            var name = group == "steam" ? "Steam 셰이더 캐시" : "NVIDIA·Direct3D 셰이더 캐시";
            var title = observed > 0 ? $"{name}: {ByteSizeText.Format(total)} 관측" + (partial || unknown > 0 ? " (일부 위치)" : "")
                : unknown > 0 ? name + " 위치를 완전히 확인하지 못했습니다" : name + " — 확인한 위치에 캐시가 없습니다";
            var guide = group == "steam"
                ? "라이브러리 설정에 등록된 steamapps/shadercache만 확인했습니다. 설치 게임·워크숍·다운로드 폴더는 포함하지 않습니다. Steam의 '다운로드 캐시 지우기'와 이 셰이더 캐시는 서로 다른 대상입니다. 삭제 후 다시 받거나 컴파일하는 동안 첫 실행이 느려질 수 있습니다."
                : "현재 사용자 LocalAppData의 DXCache·GLCache·NV_Cache·D3DSCache 기본 위치만 확인했습니다. 사용자 지정 위치는 미확인입니다. 조치 목록에서 캐시별로 30일 이상 쓰이지 않은 파일만 부분 정리할 수 있습니다. 완전히 비우려면 NVIDIA 공식 절차(캐시 끄기·재부팅·정리·설정 복원)를 따르세요. Direct3D는 Windows 임시 파일 정리에서도 확인할 수 있습니다.";
            output.Add(new("shader-cache:" + group, FindingCategory.AppCache, title,
                probe.Measurements.Where(m => m.Name.StartsWith(Prefix(group), StringComparison.Ordinal)).ToArray(),
                "파일 내용 대신 메타데이터를 읽었습니다. 논리 크기이며 하드링크 중복이 있을 수 있습니다. 오래됐거나 크다는 이유로 불필요하다고 판단하지 않습니다.",
                observed == 0 && unknown > 0 ? Verdict.CannotVerify : Verdict.Info, observed == 0 && unknown > 0 ? CannotVerifyReason.PartialData : null,
                string.Join("\n", lines) + "\n" + guide, null,
                new Impact("어느 캐시에 공간이 쓰이는지 확인합니다.", group == "steam"
                    ? "조치 목록에서 라이브러리의 shadercache를 직접 선택하면 정리 조건을 확인합니다. 재생성으로 로딩·끊김이 늘 수 있습니다."
                    : "조치 목록에서 캐시별로 30일 이상 쓰이지 않은 파일의 정리 조건을 확인합니다. 재생성으로 로딩·끊김이 늘 수 있습니다."),
                group == "steam" ? [new ShowDetailsAction(), new OpenLinkAction(PcOptimizer.Core.Actions.CacheSupportLinks.SteamDownload, "별도 다운로드 캐시 안내")]
                    : [new ShowDetailsAction(), new OpenLinkAction(PcOptimizer.Core.Actions.CacheSupportLinks.NvidiaShader, "NVIDIA 공식 정리 절차"), new OpenSettingsAction("ms-settings:storagesense") ]));
        }
        return output;
    }
    private static string Reason(string? state) => state switch
    {
        "Protected" => "보호 폴더 또는 다른 사용자", "TimedOut" => "검사 시간 초과", "ReparsePoint" => "정션·링크 미추종",
        "Placeholder" => "온라인 전용 파일", "Unreadable" or "AccessDenied" => "읽기 실패", "Invalid" => "설정 형식 미지원",
        "Unsupported" => "경로 형식 미지원", "LimitExceeded" => "위치 개수 제한", _ => "관측 불완전",
    };
}
