/**
 * @file    : GpuPlatform.cs
 * @author  : rudals252
 * @brief   : GPU 네이티브 조회·지원된 선택·동일 대상 재비교 쓰기 계약
 */
using PcOptimizer.Core.Actions;
namespace PcOptimizer.Probes.Actions.Advanced;

internal interface IGpuPlatform
{
    IReadOnlyList<GpuOptionTarget> Enumerate(CancellationToken ct);
    GpuSettingState Read(string key);
    GpuSettingState Desired(string key, GpuSettingState before, int choice);
    bool CompareExchange(string key, GpuSettingState expected, GpuSettingState desired, Action beforeCommit);
}

internal sealed class NvidiaRebarPlatform : IGpuPlatform
{
    internal const uint Modern = 0x000BFA21, Legacy = 0x000F00BA;
    private sealed class Drs : IDisposable
    {
        internal readonly NvidiaApi Api = new();
        internal readonly nint Session;
        internal Drs()
        {
            try { NvidiaApi.Check(Api.Function<NvidiaApi.Create>(0x0694D52E)(out Session)); NvidiaApi.Check(Api.Function<NvidiaApi.SessionCall>(0x375DBD6B)(Session)); }
            catch { Dispose(); throw; }
        }
        internal nint Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Any(char.IsControl)) { throw new ActionUnavailableException("TargetRejected"); }
            NvidiaApi.Check(Api.Function<NvidiaApi.Find>(0x7E4A9A0B)(Session, name, out var handle));
            var info = NvidiaApi.Profile.New(); NvidiaApi.Check(Api.Function<NvidiaApi.ProfileInfo>(0x61CD6FD6)(Session, handle, ref info));
            if (info.AppCount == 0 || info.Name != name) { throw new ActionUnavailableException("TargetRejected"); }
            return handle;
        }
        internal GpuSettingState Read(nint profile)
        {
            var value = NvidiaApi.Setting.New(Modern);
            var get = Api.Function<NvidiaApi.GetSetting>(0x73BF8338);
            var result = get(Session, profile, Modern, ref value);
            if (result == -160) { value = NvidiaApi.Setting.New(Legacy); result = get(Session, profile, Legacy, ref value); }
            NvidiaApi.Check(result);
            if (value.Type != 0 || value.Location > 3 || value.Predefined > 1 || value.PredefinedValid > 1
                || value.Value > (value.Id == Modern ? 2 : 1) || value.Id is not (Modern or Legacy)) { throw new ActionUnavailableException("GpuValueUnsupported"); }
            return new(Api.Stamp(), value.Id, (int)value.Value, true, value.Location, value.Predefined, value.PredefinedValid, value.PredefinedValue);
        }
        public void Dispose()
        { if (Session != 0) { Api.Function<NvidiaApi.SessionCall>(0xDAD9CFF8)(Session); } Api.Dispose(); }
    }
    public IReadOnlyList<GpuOptionTarget> Enumerate(CancellationToken ct)
    {
        using var drs = new Drs(); var items = new List<GpuOptionTarget>();
        var enumerate = drs.Api.Function<NvidiaApi.Enumerate>(0xBC371EE0);
        var getInfo = drs.Api.Function<NvidiaApi.ProfileInfo>(0x61CD6FD6);
        for (uint index = 0; index < 20000; index++)
        {
            ct.ThrowIfCancellationRequested(); var code = enumerate(drs.Session, index, out var handle);
            if (code == -7) { return items; } NvidiaApi.Check(code);
            var info = NvidiaApi.Profile.New(); NvidiaApi.Check(getInfo(drs.Session, handle, ref info));
            if (info.AppCount == 0 || info.Name.Length > 120 || info.Name.Any(char.IsControl)) { continue; }
            try
            {
                var state = drs.Read(handle);
                items.Add(new(GpuFeature.NvidiaRebar, info.Name, info.Name,
                    "게임 프로필 설정입니다. BIOS ReBAR 활성화를 대신하지 않으며 모든 게임의 성능 향상을 보장하지 않습니다.", Choices(state.SettingId)));
            }
            catch (ActionUnavailableException) { /* 지원한 설정 형식이 없는 프로필은 실행 목록에 넣지 않는다. */ }
        }
        throw new ActionUnavailableException("GpuEnumerationIncomplete");
    }
    internal static IReadOnlyList<GpuSettingChoice> Choices(uint id) => id == Modern
        ? [new(0, "끄기"), new(1, "자동"), new(2, "켜기")] : [new(0, "끄기"), new(1, "켜기")];
    public GpuSettingState Read(string key) { using var drs = new Drs(); return drs.Read(drs.Find(key)); }
    public GpuSettingState Desired(string key, GpuSettingState before, int choice)
    {
        if (!Choices(before.SettingId).Any(c => c.Value == choice)) { throw new ActionUnavailableException("TargetRejected"); }
        return before with { Value = choice, Enabled = true, Location = 0, Predefined = 0 };
    }
    public bool CompareExchange(string key, GpuSettingState expected, GpuSettingState desired, Action beforeCommit)
    {
        using var drs = new Drs(); var handle = drs.Find(key);
        if (drs.Read(handle) != expected || desired.Stamp != expected.Stamp || desired.SettingId != expected.SettingId) { return false; }
        if (desired.Location != 0 || desired.Predefined != 0)
        {
            // 상속·드라이버 기본 원본은 사용자 override를 삭제하여 복원한다.
            var id = desired.Location == 0 && desired.Predefined != 0 ? 0x53F0381Eu : 0xE4A26362u;
            NvidiaApi.Check(drs.Api.Function<NvidiaApi.RemoveSetting>(id)(drs.Session, handle, desired.SettingId));
        }
        else
        {
            var value = NvidiaApi.Setting.New(desired.SettingId); value.Value = (uint)desired.Value;
            NvidiaApi.Check(drs.Api.Function<NvidiaApi.SetSetting>(0x577DD202)(drs.Session, handle, ref value));
        }
        // 세션 내부 변경은 아직 저장되지 않았다. 외부 DB를 새 세션으로 다시 읽은 뒤 저장 경계를 통과한다.
        if (Read(key) != expected) { return false; }
        beforeCommit(); NvidiaApi.Check(drs.Api.Function<NvidiaApi.SessionCall>(0xFCBC7E14)(drs.Session));
        return true;
    }
}

internal sealed class NvidiaVideoPlatform : IGpuPlatform
{
    private static nint FindDisplay(NvidiaApi api, string key)
    {
        var enumerate = api.Function<NvidiaApi.EnumDisplay>(0x9ABDD40D);
        for (uint i = 0; i < 64; i++)
        {
            var status = enumerate(i, out var display);
            if (status == -7) { break; } NvidiaApi.Check(status);
            if (Name(api, display) == key) { return display; }
        }
        throw new ActionUnavailableException("GpuTargetChanged");
    }
    private static string Name(NvidiaApi api, nint display)
    {
        var name = new System.Text.StringBuilder(64);
        NvidiaApi.Check(api.Function<NvidiaApi.DisplayName>(0x22A78B05)(display, name)); return name.ToString();
    }
    internal static NvidiaApi.VideoGet ReadRaw(NvidiaApi api, nint display)
    {
        var state = NvidiaApi.VideoGet.New(); NvidiaApi.Check(api.Function<NvidiaApi.GetVideo>(0x0B6EF8B9)(display, ref state));
        if ((state.Flags & 1) == 0 || state.Enabled > 1 || state.Minimum != 0 || state.Maximum is < 4 or > 5 || state.Value > state.Maximum)
        { throw new ActionUnavailableException("GpuValueUnsupported"); }
        return state;
    }
    private static GpuSettingState Read(NvidiaApi api, nint display)
    {
        var raw = ReadRaw(api, display);
        return new(api.DisplayStamp(display), 0x1D, (int)raw.Value, raw.Enabled == 1, PredefinedValue: raw.Maximum);
    }
    public IReadOnlyList<GpuOptionTarget> Enumerate(CancellationToken ct)
    {
        using var api = new NvidiaApi(); var list = new List<GpuOptionTarget>();
        _ = api.Function<NvidiaApi.SetVideo>(0x9321CA5B);
        for (uint i = 0; i < 64; i++)
        {
            ct.ThrowIfCancellationRequested(); var code = api.Function<NvidiaApi.EnumDisplay>(0x9ABDD40D)(i, out var display);
            if (code == -7) { return list; } NvidiaApi.Check(code);
            try
            {
                var state = Read(api, display);
                list.Add(new(GpuFeature.NvidiaVideo, Name(api, display), "RTX 영상 · " + Name(api, display),
                    "비공개 드라이버 인터페이스(베타)입니다. 지원 재생기에서만 작동하며 GPU 부하·전력이 늘 수 있고 MPO와 연동됩니다. 설정 켜짐과 영상 처리 중은 다릅니다.",
                    [new(0, "끄기"), .. Enumerable.Range(1, (int)state.PredefinedValue).Select(v => new GpuSettingChoice(v, v == 5 ? "켜기 · 자동 품질" : "켜기 · 품질 " + v))]));
            }
            catch (ActionUnavailableException) { }
        }
        throw new ActionUnavailableException("GpuEnumerationIncomplete");
    }
    public GpuSettingState Read(string key) { using var api = new NvidiaApi(); return Read(api, FindDisplay(api, key)); }
    public GpuSettingState Desired(string key, GpuSettingState before, int choice)
    {
        if (choice < 0 || choice > before.PredefinedValue) { throw new ActionUnavailableException("TargetRejected"); }
        // 끄기는 품질 값을 보존하여 원복 시 켜짐 상태와 품질을 각각 돌려놓는다.
        return before with { Enabled = choice != 0, Value = choice == 0 ? before.Value : choice };
    }
    public bool CompareExchange(string key, GpuSettingState expected, GpuSettingState desired, Action beforeCommit)
    {
        using var api = new NvidiaApi(); var display = FindDisplay(api, key);
        if (Read(api, display) != expected || desired.Stamp != expected.Stamp || desired.SettingId != 0x1D) { return false; }
        var set = WriteValue(desired);
        beforeCommit(); NvidiaApi.Check(api.Function<NvidiaApi.SetVideo>(0x9321CA5B)(display, ref set)); return true;
    }
    internal static NvidiaApi.VideoSet WriteValue(GpuSettingState desired) => new()
    { Version = 64 | 0x10000, Component = 0x1D, Flags = 0, Enabled = desired.Enabled ? 1u : 0, Value = (uint)desired.Value };
}
