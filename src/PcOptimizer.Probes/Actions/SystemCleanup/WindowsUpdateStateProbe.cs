/**
 * @file    : WindowsUpdateStateProbe.cs
 * @author  : rudals252
 * @brief   : 업데이트 정리의 선행 관측값만 수집하며 서비스나 캐시를 변경하지 않음
 */
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Probes.Actions.SystemCleanup;

/// <summary>온라인 업데이트 검색 없이 설치 진행·재부팅·서비스의 현재 보고 상태를 읽습니다.</summary>
public sealed class WindowsUpdateStateProbe : IProbe
{
    private readonly UpdateActivityReader _reader = new();
    /// <inheritdoc />
    public string Id => WindowsUpdateStateRule.ProbeId;
    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Storage;
    /// <inheritdoc />
    public bool RequiresElevation => false;
    /// <inheritdoc />
    public bool RequiresNetwork => false;
    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;
    /// <inheritdoc />
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(15);
    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var observed = _reader.Read(ct);
        var values = new List<Measurement>();
        void Add(string key, MeasurementValue value) => values.Add(new(key, value, null, "로컬 WUA/서비스 상태 조회", now, MeasurementQuality.Reported));
        if (observed.Installing is { } busy) { Add("installing", new BooleanValue(busy)); }
        if (observed.RebootRequired is { } reboot) { Add("rebootRequired", new BooleanValue(reboot)); }
        if (observed.WindowsUpdate is { } wu) { Add("wuauserv", new TextValue(wu.State.ToString())); Add("wuauserv.acceptsStop", new BooleanValue(wu.AcceptsStop)); }
        if (observed.Bits is { } bits) { Add("bits", new TextValue(bits.State.ToString())); Add("bits.acceptsStop", new BooleanValue(bits.AcceptsStop)); }
        return Task.FromResult(new ProbeResult(Id, observed.Complete ? ProbeStatus.Success : ProbeStatus.Partial, values,
            observed.Complete ? [] : [new Issue(CannotVerifyReason.PartialData, "일부 업데이트 상태를 읽지 못했습니다. 확인된 값만 표시합니다.")], now, TimeSpan.Zero, context.UserContext));
    }
}
