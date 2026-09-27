/**
 * @file    : TroubleshootingTests.cs
 * @author  : rudals252
 * @brief   : 문제 해결 도구함의 임베드 카탈로그 검증(링크·명령·내장 도구 참조), 파서 거절, 명령 시작 정보(셸 없음·System32·환경 정리), 서비스 대역 흐름, 화면 모델 회귀
 */
using System.Diagnostics;
using System.IO;
using PcOptimizer.App.ViewModels;
using PcOptimizer.Core.Actions;
using PcOptimizer.Core.Drivers;
using PcOptimizer.Core.Troubleshooting;
using PcOptimizer.Probes.Drivers;
using PcOptimizer.Probes.Troubleshooting;
using PcOptimizer.Tests.Unit.App.Fakes;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>실제 명령·WMI·프로세스는 실행하지 않습니다.</summary>
public sealed class TroubleshootingTests
{
    private static readonly VendorLinkCatalog Links = VendorLinkCatalogLoader.LoadEmbedded().Catalog!;
    private static readonly TroubleshootingCatalog Catalog = TroubleshootingCatalogLoader.LoadEmbedded(Links).Catalog!;

    /// <summary>임베드 카탈로그는 공식 링크 표와 닫힌 명령·내장 도구 목록에 대해 전부 검증을 통과합니다.</summary>
    [Fact]
    public void EmbeddedCatalogIsValidAgainstLinksAndCommands()
    {
        var result = TroubleshootingCatalogLoader.LoadEmbedded(Links);
        Assert.Empty(result.Errors); Assert.NotNull(result.Catalog);
        Assert.True(Catalog.Tools.Count >= 25); Assert.Equal(8, Catalog.Symptoms.Count);
        foreach (var tool in Catalog.Tools)
        {
            switch (tool.Mode)
            {
                case ToolMode.DirectCommand: Assert.True(RepairCommandCatalog.IsKnown(tool.Command)); break;
                case ToolMode.BuiltInTool: Assert.NotNull(BuiltInToolCatalog.Find(tool.OpenTarget)); break;
                default: Assert.Contains(Links.Entries, e => e.Id == tool.LinkId && Links.IsAllowedLink(e.Url)); break;
            }
            Assert.NotEmpty(tool.Steps);
            if (tool.Safety == PcOptimizer.Core.Models.SafetyLevel.Irreversible) { Assert.NotNull(tool.Warning); }
        }
        foreach (var step in Catalog.Symptoms.SelectMany(s => s.Steps)) { Assert.NotNull(Catalog.FindTool(step.ToolId)); }
    }

    /// <summary>공식 링크 표가 없으면 외부 도구 참조가 모두 오류가 되어 카탈로그를 쓰지 않습니다(부분 적용 없음).</summary>
    [Fact]
    public void MissingLinkTableRejectsWholeCatalog()
    {
        var result = TroubleshootingCatalogLoader.LoadEmbedded(null);
        Assert.Null(result.Catalog); Assert.Contains(result.Errors, e => e.EndsWith(".linkId:unknown", StringComparison.Ordinal));
    }

    /// <summary>알 수 없는 명령·내장 도구·증상 단계의 도구 참조를 거절합니다.</summary>
    [Theory]
    [InlineData("\"mode\": \"direct\", \"command\": \"format-c\"", ".command:unknown")]
    [InlineData("\"mode\": \"builtin\", \"openTarget\": \"regedit\"", ".openTarget:unknown")]
    [InlineData("\"mode\": \"external\", \"linkId\": \"not-in-table\"", ".linkId:unknown")]
    [InlineData("\"mode\": \"direct\", \"command\": \"flushdns\", \"linkId\": \"rufus\"", ":referenceCount")]
    public void ParserRejectsUnknownReferences(string modeFragment, string expectedError)
    {
        var json = "{\"schemaVersion\":1,\"tools\":[{\"id\":\"t\",\"category\":\"c\",\"name\":\"n\",\"when\":\"w\",\"what\":\"w\",\"caution\":\"c\"," + modeFragment
            + ",\"safety\":\"safe\",\"steps\":[\"s\"],\"rebootRequired\":false}],\"symptoms\":[{\"id\":\"s\",\"title\":\"t\",\"summary\":\"s\",\"steps\":[{\"tool\":\"t\",\"note\":\"n\"}]}]}";
        var result = TroubleshootingCatalogParser.Parse(json, id => id == "rufus");
        Assert.Null(result.Catalog); Assert.Contains(result.Errors, e => e.EndsWith(expectedError, StringComparison.Ordinal));
        var dangling = json.Replace("\"tool\":\"t\"", "\"tool\":\"missing\"", StringComparison.Ordinal).Replace("\"command\": \"format-c\"", "\"command\": \"flushdns\"", StringComparison.Ordinal);
        Assert.Contains(TroubleshootingCatalogParser.Parse(dangling, _ => true).Errors, e => e.EndsWith(".tool:unknown", StringComparison.Ordinal) || e.EndsWith(":referenceCount", StringComparison.Ordinal) || e.EndsWith(":unknown", StringComparison.Ordinal));
    }

    /// <summary>직접 실행 명령은 System32 절대 경로·고정 인자·셸 없음·System32 작업 폴더이며 주입 가능 환경 변수를 지웁니다.</summary>
    [Fact]
    public void StartInfoIsFixedAndShellFree()
    {
        var runner = new RepairCommandRunner(null, @"C:\Windows\System32", "C:");
        Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", "x");
        try
        {
            foreach (var command in RepairCommandCatalog.Commands)
            {
                var start = runner.CreateStartInfo(command);
                Assert.Equal(Path.Combine(@"C:\Windows\System32", command.Executable), start.FileName);
                Assert.False(start.UseShellExecute); Assert.True(start.RedirectStandardOutput); Assert.Equal(@"C:\Windows\System32", start.WorkingDirectory);
                Assert.DoesNotContain(RepairCommandCatalog.SYSTEM_DRIVE_TOKEN, start.ArgumentList);
                Assert.False(start.Environment.ContainsKey("DOTNET_STARTUP_HOOKS"));
            }
            var chk = runner.CreateStartInfo(RepairCommandCatalog.Find(RepairCommandCatalog.CHKDSK_SCHEDULE)!);
            Assert.Equal(["/C", "C:"], chk.ArgumentList);
        }
        finally { Environment.SetEnvironmentVariable("DOTNET_STARTUP_HOOKS", null); }
    }

    /// <summary>이 PC의 System32에 모든 직접 실행·내장 도구 실행 파일이 있습니다.</summary>
    [Fact]
    public void CatalogExecutablesExistOnThisWindows()
    {
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        foreach (var command in RepairCommandCatalog.Commands) { Assert.True(File.Exists(Path.Combine(system, command.Executable)), command.Executable); }
        foreach (var tool in BuiltInToolCatalog.Tools.Where(t => t.Executable is not null)) { Assert.True(File.Exists(Path.Combine(system, tool.Executable!)), tool.Executable); }
    }

    /// <summary>복원 지점 결과 코드를 구분하고, 내장 도구는 고정 인자로 열며, 알 수 없는 ID는 거절합니다. 검사 중이면 Busy입니다.</summary>
    [Fact]
    public async Task ServiceMapsRestorePointCodesAndOpensBuiltInsWithFixedArguments()
    {
        var operations = new OperationCoordinator();
        ProcessStartInfo? captured = null;
        var service = new TroubleshootingService(operations, null, new RepairCommandRunner(null, @"C:\Windows\System32", "C:"), () => TroubleshootingService.RESTORE_SERVICE_DISABLED,
            start => { captured = start; return true; }, Environment.GetFolderPath(Environment.SpecialFolder.System));
        var lines = new List<string>();
        var result = await service.RunAsync(RepairCommandCatalog.RESTORE_POINT, new SynchronousProgress(lines), default);
        Assert.Equal(TroubleshootingService.CODE_PROTECTION_DISABLED, result.Code); Assert.Contains(lines, l => l.Contains("시스템 보호"));
        Assert.Equal("Unsupported", (await service.RunAsync("rm-rf", null, default)).Code);
        Assert.True(service.OpenBuiltIn(BuiltInToolCatalog.EVENT_VIEWER, _ => false));
        Assert.Equal(["eventvwr.msc"], captured!.ArgumentList); Assert.False(captured.UseShellExecute); Assert.EndsWith("mmc.exe", captured.FileName, StringComparison.OrdinalIgnoreCase);
        var settingsOpened = false;
        Assert.True(service.OpenBuiltIn(BuiltInToolCatalog.NETWORK_TROUBLESHOOTER, uri => { settingsOpened = uri == BuiltInToolCatalog.TROUBLESHOOT_SETTINGS_URI; return true; }));
        Assert.True(settingsOpened); Assert.False(service.OpenBuiltIn("regedit", _ => true));
        using (operations.TryAcquire(OperationKind.Scan)) { Assert.Equal(RepairCommandRunner.CODE_BUSY, (await service.RunAsync(RepairCommandCatalog.FLUSH_DNS, null, default)).Code); }
    }

    /// <summary>증상 선택은 권장 순서 카드를 만들고, 외부 도구는 공식 URL만 열며, 내장 도구는 서비스로 엽니다.</summary>
    [Fact]
    public async Task ViewModelBuildsCardsAndRoutesActions()
    {
        var opened = new List<string>(); ProcessStartInfo? started = null;
        var service = new TroubleshootingService(new OperationCoordinator(), null, new RepairCommandRunner(null, @"C:\Windows\System32", "C:"), () => 0, s => { started = s; return true; }, Environment.GetFolderPath(Environment.SpecialFolder.System));
        using var vm = new TroubleshootingViewModel(Catalog, service, new ImmediateUiDispatcher(), url => { opened.Add(url); return true; }, _ => true, Links);
        Assert.Equal("slow", vm.SelectedSymptom!.Id); Assert.True(vm.Cards.Count >= 3); Assert.Equal("1단계", vm.Cards[0].OrderText);
        vm.SelectSymptomCommand.Execute(vm.Symptoms.Single(s => s.Id == "storage"));
        var external = vm.Cards.First(c => c.Tool.Mode == ToolMode.ExternalGuide);
        await external.ActCommand.ExecuteAsync(null);
        Assert.Single(opened); Assert.True(Links.IsAllowedLink(opened[0])); Assert.Contains("공식 사이트", vm.Status);
        var builtin = vm.Cards.First(c => c.Tool.Mode == ToolMode.BuiltInTool);
        await builtin.ActCommand.ExecuteAsync(null);
        Assert.NotNull(started); Assert.Contains("열었어요", vm.Status);
        vm.SelectSymptomCommand.Execute(vm.Symptoms.Single(s => s.Id == TroubleshootingViewModel.ALL_TOOLS_ID));
        Assert.Equal(Catalog.Tools.Count, vm.Cards.Count); Assert.All(vm.Cards, c => Assert.Equal(c.Tool.Category, c.OrderText));
        var card = vm.Cards[0]; Assert.False(card.ShowSteps); card.ToggleStepsCommand.Execute(null); Assert.True(card.ShowSteps); Assert.StartsWith("1. ", card.Steps[0]);
    }

    private sealed class SynchronousProgress(List<string> lines) : IProgress<string>
    {
        public void Report(string value) => lines.Add(value);
    }
}
