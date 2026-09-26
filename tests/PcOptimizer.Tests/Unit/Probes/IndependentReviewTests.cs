/**
 * @file    : IndependentReviewTests.cs
 * @author  : rudals252
 * @brief   : 독립 검토 원장(docs/reviews/REVIEW_LEDGER.md) REV-001·REV-002 재현 테스트를 정식 회귀 테스트로 편입(드라이브 루트 Known Folder 보호, 단일 폴더 안 시간 예산·취소 확인)
 */

// 사용자 패키지
using PcOptimizer.Core.Cleaning;
using PcOptimizer.Probes.Storage;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>
/// 독립 검토 재현 테스트입니다(docs/reviews/repro/IndependentReviewTests.cs에서 가져옴, 단언은 그대로 유지).
/// </summary>
public sealed class IndependentReviewTests
{
    /// <summary>REV-002: 하위 폴더 없는 단일 폴더를 열거하는 동안 시간 예산을 넘기면 시간 초과로 보고한다.</summary>
    [Fact]
    public async Task BudgetExceededInsideOnlyDirectoryIsReported()
    {
        var time = new ManualTimeProvider();
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root", FakeDirectoryEntrySource.File("a.bin", 100));
        source.OnEnumerate = _ => time.Advance(TimeSpan.FromSeconds(121));
        var scanner = new FileSystemScanner(source, new FakeFileIdentityReader(), time);
        var result = await scanner.ScanAsync([new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [], TimeSpan.FromSeconds(120), CancellationToken.None);
        Assert.True(Assert.Single(result.Roots).TimedOut);
    }

    /// <summary>REV-002: 단일 폴더를 열거하는 동안 취소하면 OperationCanceledException으로 끝난다.</summary>
    [Fact]
    public async Task CancellationInsideOnlyDirectoryIsObserved()
    {
        using var cts = new CancellationTokenSource();
        var source = new FakeDirectoryEntrySource().Dir(@"X:\root", FakeDirectoryEntrySource.File("a.bin", 100));
        source.OnEnumerate = _ => cts.Cancel();
        var scanner = new FileSystemScanner(source, new FakeFileIdentityReader(), new ManualTimeProvider());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanner.ScanAsync([new ScanTarget("root", @"X:\root")], new ResolvedProtection([]), [], TimeSpan.FromSeconds(120), cts.Token));
    }

    /// <summary>REV-001: 드라이브 루트로 지정된 Known Folder도 보호 루트로 인정해 그 하위 전체를 보호한다.</summary>
    [Fact]
    public void KnownFolderAtVolumeRootStillProtectsDescendants()
    {
        var environment = new FakePathEnvironment().WithKnownFolder(ProtectedKnownFolder.Documents, @"D:\");
        var policy = new ProtectionPolicy(ProtectionPolicyParser.SUPPORTED_SCHEMA_VERSION, [new KnownFolderRootSpec(ProtectedKnownFolder.Documents)]);
        var protection = new ProtectionPolicyResolver(environment, new FakeRegistryReader()).Resolve(policy);
        Assert.True(protection.IsProtected(@"D:\Temp\private"));
    }
}
