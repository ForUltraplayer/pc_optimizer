/**
 * @file    : DisplayTrialSmokeTests.cs
 * @author  : rudals252
 * @brief   : 모드 조회·CDS_TEST만 실행하며 실제 화면 전환/프로필 저장은 하지 않는 실환경 점검
 */
using PcOptimizer.Probes.Actions.Display;
using Xunit.Abstractions;

namespace PcOptimizer.Tests.Smoke;

/// <summary>실제 Apply/SaveProfileOnly를 호출하지 않습니다.</summary>
public sealed class DisplayTrialSmokeTests(ITestOutputHelper output)
{
    /// <summary>지원 모드 준비와 CDS_TEST 이후 활성/저장 모드가 동일한지 확인합니다.</summary>
    [Fact] [Trait("Category", "Smoke")]
    public void EnumerateAndTestWithoutChangingDisplay()
    {
        var platform = new DisplaySettingsPlatform(); var choices = platform.Choices();
        output.WriteLine($"Candidate count: {choices.Count}");
        if (choices.Count == 0) { output.WriteLine("No eligible physical output; native CDS_TEST not exercised."); return; }
        var choice = choices[0]; var change = platform.Capture(choice.DeviceKey, choice.DesiredHz);
        var code = platform.Test(change.Identity, change.Desired);
        var after = platform.Observe(change.Identity);
        output.WriteLine($"CDS_TEST={code}; Hz {change.Before.Mode.RefreshHz} -> candidate {change.Desired.Mode.RefreshHz}; no Apply called.");
        Assert.True(after.Current.SameMode(change.Before)); Assert.True(after.Registered.SameMode(change.RegisteredBefore));
        Assert.Equal(0, code);
    }
}
