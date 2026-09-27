/**
 * @file    : AmdVideoAbiTests.cs
 * @author  : rudals252
 * @brief   : AMD 장치 대신 소유한 네이티브 vtable로 슬롯·bool·인터페이스 수명 계약 검증
 */
using System.Runtime.InteropServices;
using PcOptimizer.Probes.Actions;
using PcOptimizer.Probes.Actions.Advanced;
using static PcOptimizer.Probes.Actions.Advanced.AmdVideoPlatform;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>ADLX DLL을 로드하지 않고 공식 C ABI 호출 경로를 검증합니다.</summary>
public sealed class AmdVideoAbiTests
{
    private sealed class Fixture : IDisposable
    {
        private readonly List<nint> _allocations = [];
        private readonly List<Delegate> _delegates = [];
        internal int Released, Terminated, Writes;
        internal byte Enabled = 1, Supported = 1;
        internal readonly nint System;
        internal Fixture(bool queryFails = false)
        {
            var pnp = Text("PCI\\VEN_1002&DEV_FIXTURE"); var name = Text("Fixture Radeon");
            var gpu = Object((7, new GetString((nint _, out nint value) => { value = name; return 0; })),
                (9, new GetString((nint _, out nint value) => { value = pnp; return 0; })));
            var video = Object((3, new GetBool((nint _, out byte value) => { value = Supported; return 0; })),
                (4, new GetBool((nint _, out byte value) => { value = Enabled; return 0; })),
                (7, new SetBool((_, value) => { Enabled = value; Writes++; return 0; })));
            var multimedia = Object((4, new GetForGpu((nint _, nint selected, out nint value) => { Assert.Equal(gpu, selected); value = video; return 0; })));
            var system2 = Object((4, new GetInterface((nint _, out nint value) => { value = multimedia; return 0; })));
            var list = Object((3, new CountItems(_ => 1)), (11, new GetAt((nint _, uint index, out nint value) => { Assert.Equal(0u, index); value = gpu; return 0; })));
            System = Object((1, new GetInterface((nint _, out nint value) => { value = list; return 0; })),
                (2, new Query((nint _, string id, out nint value) => { Assert.Equal("IADLXSystem2", id); value = queryFails ? 0 : system2; return queryFails ? -1 : 0; })));
        }
        private nint Allocate(int bytes) { var p = Marshal.AllocHGlobal(bytes); _allocations.Add(p); return p; }
        private nint Text(string text) { var p = Marshal.StringToHGlobalAnsi(text); _allocations.Add(p); return p; }
        private nint Object(params (int Slot, Delegate Function)[] functions)
        {
            var table = Allocate(20 * IntPtr.Size); var pointer = Allocate(IntPtr.Size);
            for (var i = 0; i < 20; i++) { Marshal.WriteIntPtr(table, i * IntPtr.Size, 0); }
            Add(1, new Release(_ => { Released++; return 0; }));
            foreach (var (slot, function) in functions) { Add(slot, function); }
            Marshal.WriteIntPtr(pointer, table); return pointer;
            void Add(int slot, Delegate function) { _delegates.Add(function); Marshal.WriteIntPtr(table, slot * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(function)); }
        }
        internal Adlx Open() => new(System, () => Terminated++, pnp => { Assert.Equal("PCI\\VEN_1002&DEV_FIXTURE", pnp); return "32.0.1.2"; });
        public void Dispose() { foreach (var p in _allocations) { Marshal.FreeHGlobal(p); } GC.KeepAlive(_delegates); }
    }
    /// <summary>GPU 열거·영상 bool 설정·다섯 획득 인터페이스 해제 후 Terminate까지 확인합니다.</summary>
    [Fact]
    public void NativeVtablesReadSetAndReleaseOwnedInterfaces()
    {
        using var fixture = new Fixture(); var api = fixture.Open();
        Assert.Equal(1u, api.Count); var gpu = api.Gpu(0); Assert.Equal("Fixture Radeon", api.Name(gpu)); Assert.Equal(64, api.Key(gpu).Length);
        var video = api.Video(gpu); var before = api.Read(video); Assert.True(before.Enabled); Assert.Contains("32.0.1.2", before.Stamp);
        api.Set(video, false); Assert.False(api.Read(video).Enabled); Assert.Equal(1, fixture.Writes);
        api.Set(video, true); Assert.Equal(before, api.Read(video)); Assert.Equal(2, fixture.Writes);
        api.Dispose(); api.Dispose(); Assert.Equal(5, fixture.Released); Assert.Equal(1, fixture.Terminated);
    }
    /// <summary>공식 bool 이외의 응답·지원 없음은 실행 입력으로 만들지 않습니다.</summary>
    [Theory]
    [InlineData(0, 1)][InlineData(2, 1)][InlineData(1, 2)]
    public void InvalidNativeStatesAreRejected(byte supported, byte enabled)
    {
        using var fixture = new Fixture { Supported = supported, Enabled = enabled }; using var api = fixture.Open();
        var video = api.Video(api.Gpu(0)); Assert.Throws<ActionUnavailableException>(() => api.Read(video)); Assert.Equal(0, fixture.Writes);
    }
    /// <summary>초기화 중간 실패도 이미 획득한 목록을 해제하고 한 번 종료합니다.</summary>
    [Fact]
    public void PartialInitializationReleasesAcquiredList()
    {
        using var fixture = new Fixture(queryFails: true); Assert.Throws<ActionUnavailableException>(() => fixture.Open());
        Assert.Equal(1, fixture.Released); Assert.Equal(1, fixture.Terminated);
    }
}
