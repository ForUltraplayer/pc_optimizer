# SP4+권한: 설명 형식·안전 배지·내 PC 사양·항상 관리자 권한 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 2차 개선의 첫 단계로, 모든 후보 카드에 "이게 뭔가요/효과/주의" 3줄과 안전 수준 배지를 붙이고, 요약 타일을 "바로 할 수 있는 것/직접 해야 하는 것"으로 바꾸며, 메인 화면에 "내 PC 사양" 섹션(캡처·공유 포함)을 넣고, 앱을 항상 관리자 권한으로 시작하도록 전환(승격 재실행 흐름 제거, 표준 계정 처리)한다.

**Architecture:** Core `Finding`에 선택 필드 `Explanation`(3줄)과 `SafetyLevel`을 추가하고 Candidate 규칙 8개가 채운 뒤 불변식으로 강제한다. 사양 섹션은 새 `SystemDetailsProbe`와 기존 하드웨어 프로브를 규칙 없이 직접 실행하는 `PcSpecService`가 `PcSpecSnapshot`을 만들고, `PcSpecViewModel`이 텍스트·PNG·클립보드로 내보낸다. 권한은 매니페스트 `requireAdministrator`로 바꾸고, 승격 재실행 코드를 제거한 뒤 "대화형 로그온 사용자 ≠ 프로세스 토큰 사용자"를 `UserScopeResolver`로 판정해 시스템 범위 제한을 결정한다.

**Tech Stack:** C# / .NET 10 / WPF(WPF-UI 4.3, CommunityToolkit.Mvvm 8.4) / xUnit. 기존 프로젝트 4개(Core, Probes, App, Tests)만 사용. 새 NuGet 패키지 없음.

**Spec:** `docs/superpowers/specs/2026-09-27-improvement-phase2-design.md` §0(항상 관리자 권한의 조건), §3(화면), §3.2(내 PC 사양), §7 1단계. 기존 설계 `docs/superpowers/specs/2026-09-26-pc-optimizer-design.md` §3(Finding 모델), §8(개인정보).

## Global Constraints

- 모든 C# 파일 서두 헤더: `/** @file : <파일명> / @author : rudals252 / @brief : <요약> */`. XAML은 `<!-- @file / @author: rudals252 / @brief -->`. 모든 공개 클래스·멤버에 한글 XML 문서 주석. 새 테스트 메서드에도 `<summary>`.
- 파일 구성 순서: 헤더 → using(기본 → 서드파티 → 사용자, 그룹 주석) → 상수/필드 → 생성자 → 속성 → 메서드. 상수 UPPER_SNAKE_CASE, 매직 넘버 금지.
- 사용자 문자열은 리소스: Core는 `src/PcOptimizer.Core/Resources/CoreStrings.resx`(접근자 `PcOptimizer.Core.Resources.CoreStrings`), App은 `src/PcOptimizer.App/Resources/Strings.resx`(접근자 `PcOptimizer.App.Resources.Strings`), Probes는 `src/PcOptimizer.Probes/Resources/ProbeStrings.resx`.
- 로깅은 `IAppLogger`만. `Console.WriteLine` 금지. 예외는 형식 이름만 기록.
- 설명 3줄 규칙: `What`·`Effect`·`Caution` 각각 한 문장, **60자 이내**(테스트로 강제). 근거 링크·난이도·용어 툴팁 없음.
- 안전 수준: `Safe`(안전) | `Caution`(주의) | `Irreversible`(되돌릴 수 없음). 배지는 색과 함께 반드시 텍스트를 표시한다.
- 기본 내보내기·사양 공유는 사용자명·PC 이름·일련번호·MAC·볼륨 GUID를 제외한다. "식별 정보 포함" 토글은 기본 꺼짐.
- 규칙·정책 파일은 어셈블리 포함 리소스만 읽는다(변경 없음).
- 검증 명령(Release):
  - `"C:\Program Files\dotnet\dotnet.exe" build PcOptimizer.sln --configuration Release` → 경고 0, 오류 0
  - `"C:\Program Files\dotnet\dotnet.exe" test PcOptimizer.sln --configuration Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"`
  - Smoke: `--filter "Category=Smoke"` (실제 PC 조회만, 파일 변경 없음)
- 커밋: `git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit`, 한국어 제목 `SP4:` 접두, 트레일러 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- 공유 리뷰 원장 `docs/reviews/REVIEW_LEDGER.md`를 작업 시작·완료 보고 전에 읽는다. 이 계획이 닿는 REV: REV-015(요약 타일·도구 카드 노출), REV-014(드라이버 타일 — Task 5에서 타일을 교체하므로 함께 닫힘 여부를 기록).
- 자동 테스트는 실제 사용자 데이터를 변경하지 않는다. 사양 섹션은 조회만 한다.

---

## 파일 구조

| 구분 | 파일 | 책임 |
|---|---|---|
| Core 모델 | `src/PcOptimizer.Core/Models/Explanation.cs` (새) | 설명 3줄 레코드 |
| Core 모델 | `src/PcOptimizer.Core/Models/SafetyLevel.cs` (새) | 안전 수준 enum |
| Core 모델 | `src/PcOptimizer.Core/Models/Finding.cs` (수정) | 선택 필드 2개 추가, Candidate 불변식 |
| Core 규칙 | `src/PcOptimizer.Core/Rules/{DiskHealth,DisplayRefresh,DriverUpdate,MemorySpeed,PowerPlan,StorageSpace,TrimPolicy,WindowsUpdateDriver}Rule.cs` (수정) | Candidate에 설명·안전 수준 채움 |
| Core 리소스 | `src/PcOptimizer.Core/Resources/CoreStrings.resx` (수정) | `*_Explain_What/Effect/Caution` 키 |
| Core 계약 | `src/PcOptimizer.Core/Rules/SystemDetailsProbeContract.cs` (새) | OS·CPU·BIOS 날짜·네트워크 어댑터 측정 이름 |
| Probes | `src/PcOptimizer.Probes/Hardware/SystemDetailsProbe.cs` (새) | WMI로 OS·CPU·BIOS 날짜·네트워크 어댑터 수집 |
| Probes | `src/PcOptimizer.Probes/Platform/InteractiveSessionUser.cs` (새) | WTS API로 대화형 로그온 사용자 읽기 |
| App 서비스 | `src/PcOptimizer.App/Services/UserScopeResolver.cs` (새) | 로그온 사용자 vs 토큰 사용자 → 범위 판정(순수) |
| App 서비스 | `src/PcOptimizer.App/Services/PcSpecService.cs` (새) | 하드웨어 프로브 직접 실행 → `PcSpecSnapshot` |
| App 모델 | `src/PcOptimizer.App/Models/PcSpecSnapshot.cs` (새) | 사양 섹션·항목 레코드 |
| App 서비스 | `src/PcOptimizer.App/Services/PcSpecTextFormatter.cs` (새) | 사양 → 공유용 텍스트(익명화 여부 포함) |
| App 서비스 | `src/PcOptimizer.App/Services/IActionAvailability.cs`, `CacheToolActionAvailability.cs` (새) | "바로 할 수 있는 것" 판정(SP1이 확장) |
| App VM | `src/PcOptimizer.App/ViewModels/PcSpecViewModel.cs` (새) | 사양 섹션·캡처·복사·저장 |
| App VM | `src/PcOptimizer.App/ViewModels/FindingCardViewModel.cs` (수정) | 설명 3줄·안전 배지 노출 |
| App VM | `src/PcOptimizer.App/ViewModels/MainViewModel.cs`, `MainViewModel.Overview.cs` (수정) | 요약 타일 교체, 사양 진입, 표준 계정 안내 |
| App VM | `src/PcOptimizer.App/ViewModels/MainViewModel.Elevation.cs` (삭제) | 승격 재실행 제거 |
| App 서비스 | `ElevationRelauncher.cs`, `ElevatedRescanArguments.cs`, `ElevationRelaunchResult.cs`, `ScanLaunchMode.cs`, `ScanLaunchModeResolver.cs` (삭제) | 승격 재실행 제거 |
| App 뷰 | `src/PcOptimizer.App/Views/MainWindow.xaml` (수정), `Views/PcSpecView.xaml(.cs)` (새) | 카드 템플릿·타일·사양 섹션 |
| App | `src/PcOptimizer.App/app.manifest`, `App.xaml.cs` (수정) | requireAdministrator, 조립 |
| 배포 | `src/PcOptimizer.App/Assets/app.ico`, `tools/make-icon.ps1`, `tools/package.ps1`, `tools/README-in-zip.txt`, `THIRD-PARTY-NOTICES.md` (새) | 단일 파일 포터블 zip(Task 12) |
| Tests | `tests/PcOptimizer.Tests/Unit/Models/ExplanationTests.cs`, `Unit/Rules/CandidateExplanationTests.cs`, `Unit/Probes/SystemDetailsProbeTests.cs`, `Unit/App/UserScopeResolverTests.cs`, `Unit/App/PcSpecTests.cs`, `Unit/App/ActionAvailabilityTests.cs` (새); `Unit/App/MainViewModelTests.cs`, `MainWindowLayoutTests.cs`, `FindingCardViewModelTests.cs` (수정); `ElevationRelauncherTests.cs`, `ElevatedRescanArgumentsTests.cs` (삭제) | |

---

### Task 1: Core 모델 — Explanation, SafetyLevel, Finding 선택 필드

**Files:**
- Create: `src/PcOptimizer.Core/Models/Explanation.cs`
- Create: `src/PcOptimizer.Core/Models/SafetyLevel.cs`
- Modify: `src/PcOptimizer.Core/Models/Finding.cs` (생성자 매개변수 추가, 속성 2개)
- Test: `tests/PcOptimizer.Tests/Unit/Models/ExplanationTests.cs`

**Interfaces:**
- Produces: `public sealed record Explanation(string What, string Effect, string Caution)` with `public const int MAX_LINE_LENGTH = 60` and validation; `public enum SafetyLevel { Safe, Caution, Irreversible }`; `Finding` 생성자 끝에 `Explanation? explanation = null, SafetyLevel? safety = null`, 속성 `Explanation? Explanation`, `SafetyLevel? Safety`. 기존 호출부는 변경 없이 컴파일된다.

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/PcOptimizer.Tests/Unit/Models/ExplanationTests.cs`:

```csharp
/**
 * @file    : ExplanationTests.cs
 * @author  : rudals252
 * @brief   : 설명 3줄 레코드의 길이·공백 검증과 Finding 선택 필드 보존을 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.Models;

/// <summary>설명 3줄 레코드와 Finding의 설명·안전 수준 필드를 검증합니다.</summary>
public sealed class ExplanationTests
{
    /// <summary>각 줄은 비어 있지 않고 60자 이내여야 한다.</summary>
    [Theory]
    [InlineData("", "효과", "주의")]
    [InlineData("무엇", "", "주의")]
    [InlineData("무엇", "효과", "")]
    [InlineData("가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마바사아자차카타파하가나다라마", "효과", "주의")]
    public void RejectsEmptyOrLongLines(string what, string effect, string caution)
    {
        Assert.Throws<ArgumentException>(() => new Explanation(what, effect, caution));
    }

    /// <summary>60자 정확히는 허용한다.</summary>
    [Fact]
    public void AllowsSixtyCharacterLine()
    {
        var sixty = new string('가', Explanation.MAX_LINE_LENGTH);
        var explanation = new Explanation(sixty, "효과", "주의");
        Assert.Equal(sixty, explanation.What);
    }

    /// <summary>Finding은 설명과 안전 수준을 선택적으로 보존한다.</summary>
    [Fact]
    public void FindingKeepsOptionalExplanationAndSafety()
    {
        var explanation = new Explanation("이게 뭔가요", "효과", "주의");
        var finding = new Finding(
            "test.id", FindingCategory.Power, "제목", [], "근거", Verdict.Info, null, null, null, null, [],
            explanation, SafetyLevel.Safe);
        Assert.Same(explanation, finding.Explanation);
        Assert.Equal(SafetyLevel.Safe, finding.Safety);

        var without = new Finding("test.id", FindingCategory.Power, "제목", [], "근거", Verdict.Info, null, null, null, null, []);
        Assert.Null(without.Explanation);
        Assert.Null(without.Safety);
    }
}
```

- [ ] **Step 2: 실패 확인**

Run: `"C:\Program Files\dotnet\dotnet.exe" test tests/PcOptimizer.Tests --configuration Release --filter "FullyQualifiedName~ExplanationTests"`
Expected: 컴파일 오류(`Explanation`, `SafetyLevel` 없음).

- [ ] **Step 3: 최소 구현**

`src/PcOptimizer.Core/Models/SafetyLevel.cs`:

```csharp
/**
 * @file    : SafetyLevel.cs
 * @author  : rudals252
 * @brief   : 조치·후보의 안전 수준(안전/주의/되돌릴 수 없음). 배지이자 실행 정책의 기준
 */

namespace PcOptimizer.Core.Models;

/// <summary>안전 수준입니다. 배지에 텍스트로 표시하며 SP1 실행기의 확인 정책 기준이 됩니다.</summary>
public enum SafetyLevel
{
    /// <summary>데이터 손실 없음. 되돌리기 가능하거나 지워도 자동 재생성되는 항목.</summary>
    Safe,

    /// <summary>되돌릴 수는 있으나 체감 비용(재생성 시간, 재로그인, 재부팅)이 있는 항목.</summary>
    Caution,

    /// <summary>되돌릴 수 없는 항목(영구 삭제 등). 별도 확인 문구가 붙는다.</summary>
    Irreversible,
}
```

`src/PcOptimizer.Core/Models/Explanation.cs`:

```csharp
/**
 * @file    : Explanation.cs
 * @author  : rudals252
 * @brief   : 초보자용 설명 3줄(이게 뭔가요/효과/주의). 각 줄은 한 문장 60자 이내
 */

namespace PcOptimizer.Core.Models;

/// <summary>
/// 카드에 표시하는 설명 3줄입니다. 근거 링크·난이도·용어 사전은 두지 않습니다.
/// </summary>
public sealed record Explanation
{
    /// <summary>한 줄 최대 길이(문자 수).</summary>
    public const int MAX_LINE_LENGTH = 60;

    /// <summary>설명 3줄을 만듭니다. 각 줄은 비어 있지 않고 60자 이내여야 합니다.</summary>
    /// <param name="what">"이게 뭔가요" 한 줄.</param>
    /// <param name="effect">효과 한 줄.</param>
    /// <param name="caution">주의 한 줄.</param>
    /// <exception cref="ArgumentException">비어 있거나 60자를 넘는 줄이 있는 경우.</exception>
    public Explanation(string what, string effect, string caution)
    {
        What = Validate(what, nameof(what));
        Effect = Validate(effect, nameof(effect));
        Caution = Validate(caution, nameof(caution));
    }

    /// <summary>"이게 뭔가요" 한 줄.</summary>
    public string What { get; }

    /// <summary>효과 한 줄.</summary>
    public string Effect { get; }

    /// <summary>주의 한 줄.</summary>
    public string Caution { get; }

    private static string Validate(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("설명 줄은 비어 있을 수 없습니다.", parameterName);
        }

        if (value.Length > MAX_LINE_LENGTH)
        {
            throw new ArgumentException($"설명 줄은 {MAX_LINE_LENGTH}자 이내여야 합니다.", parameterName);
        }

        return value;
    }
}
```

`src/PcOptimizer.Core/Models/Finding.cs` 수정 — 생성자 매개변수 목록 끝에 두 개를 추가하고 속성을 추가한다(기존 `ValidateInvariants` 호출은 그대로):

```csharp
    public Finding(
        string id,
        FindingCategory category,
        string title,
        IReadOnlyList<Measurement> measured,
        string evidence,
        Verdict verdict,
        CannotVerifyReason? cannotVerifyReason,
        string? detail,
        Recommendation? recommendation,
        Impact? impact,
        IReadOnlyList<FindingAction> actions,
        Explanation? explanation = null,
        SafetyLevel? safety = null)
    {
        ValidateInvariants(id, title, measured, evidence, verdict, cannotVerifyReason, recommendation, actions);

        Id = id;
        Category = category;
        Title = title;
        Measured = [.. measured];
        Evidence = evidence;
        Verdict = verdict;
        CannotVerifyReason = cannotVerifyReason;
        Detail = detail;
        Recommendation = recommendation;
        Impact = impact;
        Actions = [.. actions];
        Explanation = explanation;
        Safety = safety;
    }

    /// <summary>초보자용 설명 3줄. Task 3부터 Candidate이면 필수.</summary>
    public Explanation? Explanation { get; }

    /// <summary>안전 수준. Task 3부터 Candidate이면 필수.</summary>
    public SafetyLevel? Safety { get; }
```

Finding의 `@brief`와 클래스 XML 주석에 두 필드를 한 줄씩 추가한다.

- [ ] **Step 4: 통과 확인**

Run: `"C:\Program Files\dotnet\dotnet.exe" test tests/PcOptimizer.Tests --configuration Release --filter "FullyQualifiedName~ExplanationTests"`
Expected: PASS 6/6. 전체 기본 필터도 통과(기존 호출부는 선택 매개변수라 영향 없음).

- [ ] **Step 5: 커밋**

```bash
git add src/PcOptimizer.Core/Models tests/PcOptimizer.Tests/Unit/Models/ExplanationTests.cs
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: Finding에 설명 3줄(Explanation)과 안전 수준(SafetyLevel) 선택 필드 추가

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Candidate 규칙 8개에 설명 3줄·안전 수준 채우기

**Files:**
- Modify: `src/PcOptimizer.Core/Resources/CoreStrings.resx`
- Modify: `src/PcOptimizer.Core/Rules/DiskHealthRule.cs`, `DisplayRefreshRule.cs`, `DriverUpdateRule.cs`, `MemorySpeedRule.cs`, `PowerPlanRule.cs`, `StorageSpaceRule.cs`, `TrimPolicyRule.cs`, `WindowsUpdateDriverRule.cs`
- Test: `tests/PcOptimizer.Tests/Unit/Rules/CandidateExplanationTests.cs`

**Interfaces:**
- Consumes: Task 1의 `Explanation`, `SafetyLevel`, `Finding` 선택 매개변수.
- Produces: 8개 규칙의 모든 `Verdict.Candidate` Finding에 `Explanation`과 `Safety`가 채워진다. 안전 수준: Display=Safe, Memory=Caution(BIOS 변경), Power=Safe, DriverUpdate=Caution, WindowsUpdateDriver=Caution, StorageSpace=Safe(정리 안내), DiskHealth=Caution(백업 권고), TrimPolicy=Caution.

- [ ] **Step 1: 실패하는 테스트 작성** — 각 규칙의 Candidate fixture는 기존 규칙 테스트(`tests/PcOptimizer.Tests/Unit/Rules/*RuleTests.cs`)가 이미 만드는 스냅샷을 재사용한다. 공통 검사기를 만든다.

`tests/PcOptimizer.Tests/Unit/Rules/CandidateExplanationTests.cs`:

```csharp
/**
 * @file    : CandidateExplanationTests.cs
 * @author  : rudals252
 * @brief   : Candidate를 내는 규칙 8개가 설명 3줄과 안전 수준을 채우고, 각 줄이 60자 이내이며 금지 문구가 없는지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.Tests.Unit.Rules;

/// <summary>Candidate 규칙의 설명 3줄·안전 수준을 검증합니다.</summary>
public sealed class CandidateExplanationTests
{
    private static readonly string[] FORBIDDEN = ["근거:", "http", "난이도", "정격 미달", "XMP 꺼짐", "확보 가능"];

    /// <summary>규칙별 Candidate 스냅샷을 만드는 fixture. 각 규칙 테스트 파일의 헬퍼를 호출한다.</summary>
    public static TheoryData<string, IRule, ScanSnapshot> Candidates => new()
    {
        { "display", new DisplayRefreshRule(), DisplayRefreshRuleTests.CandidateSnapshot() },
        { "memory", new MemorySpeedRule(), MemorySpeedRuleTests.CandidateSnapshot() },
        { "power", new PowerPlanRule(), PowerPlanRuleTests.CandidateSnapshot() },
        { "storage", new StorageSpaceRule(), StorageSpaceRuleTests.CandidateSnapshot() },
        { "diskHealth", new DiskHealthRule(), DiskHealthRuleTests.CandidateSnapshot() },
        { "trim", new TrimPolicyRule(), TrimPolicyRuleTests.CandidateSnapshot() },
        { "driverUpdate", DriverUpdateRuleTests.CreateRule(), DriverUpdateRuleTests.CandidateSnapshot() },
        { "wuDriver", new WindowsUpdateDriverRule(), WindowsUpdateDriverRuleTests.CandidateSnapshot() },
    };

    /// <summary>Candidate마다 설명 3줄과 안전 수준이 있고 각 줄이 60자 이내이며 금지 문구가 없다.</summary>
    [Theory]
    [MemberData(nameof(Candidates))]
    public void CandidateHasExplanationAndSafety(string name, IRule rule, ScanSnapshot snapshot)
    {
        var candidates = rule.Evaluate(snapshot).Where(f => f.Verdict == Verdict.Candidate).ToList();
        Assert.NotEmpty(candidates);
        foreach (var finding in candidates)
        {
            Assert.NotNull(finding.Explanation);
            Assert.NotNull(finding.Safety);
            foreach (var line in new[] { finding.Explanation!.What, finding.Explanation.Effect, finding.Explanation.Caution })
            {
                Assert.InRange(line.Length, 1, Explanation.MAX_LINE_LENGTH);
                Assert.DoesNotContain(FORBIDDEN, forbidden => line.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
```

각 규칙 테스트 파일에 `public static ScanSnapshot CandidateSnapshot()` 헬퍼가 없으면 추가한다: 해당 파일에서 이미 Candidate를 만들어 단언하는 테스트의 스냅샷 생성 코드를 그대로 `public static` 메서드로 뽑아낸다(동작 변경 없음). `DriverUpdateRuleTests.CreateRule()`는 그 파일이 쓰는 `VendorLinkCatalog` fixture로 `new DriverUpdateRule(links)`를 돌려준다.

- [ ] **Step 2: 실패 확인**

Run: `"C:\Program Files\dotnet\dotnet.exe" test tests/PcOptimizer.Tests --configuration Release --filter "FullyQualifiedName~CandidateExplanationTests"`
Expected: 8건 모두 `Assert.NotNull(finding.Explanation)` 실패.

- [ ] **Step 3: 리소스 키 추가** — `CoreStrings.resx`에 규칙별 3키. 문구(각 60자 이내, 한 문장):

| 키 | 값 |
|---|---|
| `Display_Explain_What` | 화면이 1초에 몇 번 새로 그려지는지(주사율)를 정합니다. |
| `Display_Explain_Effect` | 마우스 이동과 스크롤이 눈에 띄게 부드러워집니다. |
| `Display_Explain_Caution` | 같은 해상도에서만 바꾸며, 화면이 안 나오면 15초 뒤 되돌아옵니다. |
| `Memory_Explain_What` | 메모리가 낼 수 있는 속도보다 낮게 설정돼 있는지 봅니다. |
| `Memory_Explain_Effect` | BIOS에서 메모리 프로필을 켜면 게임·작업 반응이 빨라질 수 있습니다. |
| `Memory_Explain_Caution` | BIOS 설정은 앱이 바꾸지 않으며, 구성에 따라 정상 제한일 수 있습니다. |
| `Power_Explain_What` | Windows가 성능과 절전 중 무엇을 우선할지 정하는 전원 계획입니다. |
| `Power_Explain_Effect` | 배터리 사용 시간이 늘거나 반응 속도가 빨라집니다. |
| `Power_Explain_Caution` | 언제든 이전 계획으로 되돌릴 수 있습니다. |
| `Storage_Explain_What` | 드라이브에 남은 공간이 얼마나 적은지 봅니다. |
| `Storage_Explain_Effect` | 여유 공간이 생기면 업데이트 실패와 느려짐이 줄어듭니다. |
| `Storage_Explain_Caution` | 정리 전 어떤 파일이 지워지는지 확인하세요. |
| `DiskHealth_Explain_What` | 저장장치가 Windows에 보고한 상태를 봅니다. |
| `DiskHealth_Explain_Effect` | 이상 징후를 미리 알면 데이터를 잃기 전에 백업할 수 있습니다. |
| `DiskHealth_Explain_Caution` | 정상 보고가 고장 없음을 뜻하지는 않습니다. |
| `Trim_Explain_What` | SSD가 지운 공간을 스스로 정리하도록 알려주는 설정입니다. |
| `Trim_Explain_Effect` | 켜져 있으면 SSD 쓰기 속도가 오래 유지됩니다. |
| `Trim_Explain_Caution` | 장치가 지원해야 효과가 있으며, 앱은 설정만 봅니다. |
| `DriverUpdate_Explain_What` | 그래픽 드라이버의 같은 계열 최신 버전이 있는지 봅니다. |
| `DriverUpdate_Explain_Effect` | 게임 호환성과 안정성이 좋아질 수 있습니다. |
| `DriverUpdate_Explain_Caution` | 설치 중 화면이 깜빡이고 재부팅이 필요할 수 있습니다. |
| `WuDriver_Explain_What` | Windows 업데이트가 제공하는 드라이버 후보를 봅니다. |
| `WuDriver_Explain_Effect` | 장치 인식 문제나 오류가 줄어들 수 있습니다. |
| `WuDriver_Explain_Caution` | 설치는 Windows 설정에서 하며 재부팅이 필요할 수 있습니다. |

- [ ] **Step 4: 규칙 수정** — 각 규칙에서 `Verdict.Candidate`인 `new Finding(...)` 호출에 두 인자를 추가한다. 예(`PowerPlanRule.cs`의 Candidate 생성 호출):

```csharp
            findings.Add(new Finding(
                id: ...,                       // 기존 그대로
                category: FindingCategory.Power,
                title: CoreStrings.Power_Title_BatteryHighPerformance,
                measured: ...,
                evidence: Format(CoreStrings.Power_Evidence_BatteryHighPerformance, planName),
                verdict: Verdict.Candidate,
                cannotVerifyReason: null,
                detail: null,
                recommendation: new Recommendation(CoreStrings.Power_Recommendation_Text, CoreStrings.Power_Recommendation_Condition),
                impact: new Impact(CoreStrings.Power_Impact_Benefit, CoreStrings.Power_Impact_SideEffect),
                actions: ...,
                explanation: new Explanation(CoreStrings.Power_Explain_What, CoreStrings.Power_Explain_Effect, CoreStrings.Power_Explain_Caution),
                safety: SafetyLevel.Safe));
```

규칙별 `Explanation` 키 접두와 `SafetyLevel`: Display→`Display_`/Safe, Memory→`Memory_`/Caution, Power→`Power_`/Safe, StorageSpace→`Storage_`/Safe, DiskHealth→`DiskHealth_`/Caution, TrimPolicy→`Trim_`/Caution, DriverUpdate→`DriverUpdate_`/Caution, WindowsUpdateDriver→`WuDriver_`/Caution. 각 규칙 파일에 상수 `private static readonly SafetyLevel CANDIDATE_SAFETY = SafetyLevel.Safe;` 형태로 두고 호출부에서 쓴다.

- [ ] **Step 5: 통과 확인**

Run: 위 필터. Expected: 8/8 PASS. 이어서 `--filter "FullyQualifiedName~RuleTests"`로 기존 규칙 테스트 전부 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/PcOptimizer.Core/Rules src/PcOptimizer.Core/Resources/CoreStrings.resx tests/PcOptimizer.Tests/Unit/Rules
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: Candidate 규칙 8개에 설명 3줄과 안전 수준 부여

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: 불변식 — Candidate는 설명·안전 수준 필수

**Files:**
- Modify: `src/PcOptimizer.Core/Models/Finding.cs` (`ValidateInvariants`)
- Modify: `tests/PcOptimizer.Tests/Unit/Models/FindingInvariantTests.cs` (기존 Candidate 생성 헬퍼에 설명·안전 추가) — 파일명이 다르면 `Finding` 불변식을 검증하는 기존 테스트 파일을 찾아 수정한다(`grep -l "FindingInvariantException" tests -r`).

**Interfaces:**
- Produces: `Verdict.Candidate`이면서 `explanation`이나 `safety`가 null이면 `FindingInvariantException`.

- [ ] **Step 1: 실패하는 테스트 작성** — 기존 불변식 테스트 파일에 추가:

```csharp
    /// <summary>Candidate는 설명 3줄과 안전 수준이 없으면 만들 수 없다.</summary>
    [Fact]
    public void CandidateRequiresExplanationAndSafety()
    {
        var recommendation = new Recommendation("권고", "조건");
        Assert.Throws<FindingInvariantException>(() => new Finding(
            "x", FindingCategory.Power, "제목", [], "근거", Verdict.Candidate, null, null, recommendation, null, [],
            explanation: null, safety: SafetyLevel.Safe));
        Assert.Throws<FindingInvariantException>(() => new Finding(
            "x", FindingCategory.Power, "제목", [], "근거", Verdict.Candidate, null, null, recommendation, null, [],
            explanation: new Explanation("무엇", "효과", "주의"), safety: null));
    }
```

- [ ] **Step 2: 실패 확인** — Run: `--filter "FullyQualifiedName~CandidateRequiresExplanationAndSafety"`. Expected: FAIL(예외 없음).

- [ ] **Step 3: 구현** — `Finding.ValidateInvariants`의 시그니처에 `Explanation? explanation, SafetyLevel? safety`를 추가하고 Candidate 검사 블록에 추가:

```csharp
        if (verdict == Verdict.Candidate)
        {
            // 기존: recommendation·evidence 필수 검사 ...
            if (explanation is null)
            {
                throw new FindingInvariantException("Candidate에는 설명 3줄(Explanation)이 필요합니다.");
            }

            if (safety is null)
            {
                throw new FindingInvariantException("Candidate에는 안전 수준(Safety)이 필요합니다.");
            }
        }
```

생성자의 `ValidateInvariants(...)` 호출에 두 인자를 넘긴다. 기존 테스트에서 Candidate를 만드는 헬퍼(예: `MainViewModelTests`, `FindingCardViewModelTests`, `ReportExporterTests`, `AppTestDoubles`의 `ImprovementRule`)가 있으면 `explanation: new Explanation("테스트 설명", "테스트 효과", "테스트 주의"), safety: SafetyLevel.Safe`를 추가한다. 찾는 명령: `grep -rn "Verdict.Candidate" tests/PcOptimizer.Tests --include=*.cs -l`.

- [ ] **Step 4: 통과 확인** — 기본 필터 전체 PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/PcOptimizer.Core/Models/Finding.cs tests
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: Candidate 불변식에 설명 3줄·안전 수준 필수 추가

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: 카드 — 설명 3줄·안전 배지 표시, 원시 값은 자세히로

**Files:**
- Modify: `src/PcOptimizer.App/ViewModels/FindingCardViewModel.cs`
- Modify: `src/PcOptimizer.App/Views/MainWindow.xaml` (카드 `DataTemplate`, `VerdictBadge` 옆 `SafetyBadge` 스타일)
- Modify: `src/PcOptimizer.App/Resources/Strings.resx`
- Test: `tests/PcOptimizer.Tests/Unit/App/FindingCardViewModelTests.cs`, `MainWindowLayoutTests.cs`

**Interfaces:**
- Produces(VM): `bool HasExplanation`, `string? ExplainWhat`, `string? ExplainEffect`, `string? ExplainCaution`, `bool HasSafety`, `string? SafetyText`, `SafetyLevel? Safety`.
- 리소스: `Safety_Safe`="안전", `Safety_Caution`="주의", `Safety_Irreversible`="되돌릴 수 없음", `Card_ExplainWhatHeader`="이게 뭔가요", `Card_ExplainEffectHeader`="효과", `Card_ExplainCautionHeader`="주의".

- [ ] **Step 1: 실패하는 테스트 작성** — `FindingCardViewModelTests.cs`에 추가:

```csharp
    /// <summary>설명 3줄과 안전 배지 텍스트를 노출하고, 없는 Finding은 숨긴다.</summary>
    [Fact]
    public void ExposesExplanationAndSafetyBadge()
    {
        var finding = new Finding("card.explain", FindingCategory.Power, "제목", [], "근거", Verdict.Candidate, null, null,
            new Recommendation("권고", "조건"), null, [],
            new Explanation("이게 뭔가요 줄", "효과 줄", "주의 줄"), SafetyLevel.Caution);
        var vm = new FindingCardViewModel(finding, new SettingsUriPolicy(NullAppLogger.Instance), new LinkPolicy(null, NullAppLogger.Instance));

        Assert.True(vm.HasExplanation);
        Assert.Equal("이게 뭔가요 줄", vm.ExplainWhat);
        Assert.Equal("효과 줄", vm.ExplainEffect);
        Assert.Equal("주의 줄", vm.ExplainCaution);
        Assert.True(vm.HasSafety);
        Assert.Equal(Strings.Safety_Caution, vm.SafetyText);

        var info = new Finding("card.info", FindingCategory.Power, "제목", [], "근거", Verdict.Info, null, null, null, null, []);
        var infoVm = new FindingCardViewModel(info, new SettingsUriPolicy(NullAppLogger.Instance), new LinkPolicy(null, NullAppLogger.Instance));
        Assert.False(infoVm.HasExplanation);
        Assert.False(infoVm.HasSafety);
    }
```

`MainWindowLayoutTests.cs`의 기존 "긴 경로 줄바꿈" 검사 옆에 추가:

```csharp
    /// <summary>카드에 안전 배지 텍스트와 설명 3줄이 렌더되고 가로로 넘치지 않는다.</summary>
    [Fact]
    public void CardRendersSafetyBadgeAndExplanationLines()
    {
        RunOnSta(() =>
        {
            var window = new MainWindow(CreateScannedViewModel(overview: true, rule: new ImprovementRule()));
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(720, 800));
            root.Arrange(new Rect(0, 0, 720, 800));
            root.UpdateLayout();
            var badge = FindTextBlocks(root).First(t => t.Text == Strings.Safety_Safe);
            Assert.True(badge.ActualWidth > 0);
            var lines = FindTextBlocks(root).Where(t => t.Text is "테스트 설명" or "테스트 효과" or "테스트 주의").ToList();
            Assert.Equal(3, lines.Count);
            Assert.All(lines, t => Assert.True(t.ActualWidth <= 720));
        });
    }
```

`ImprovementRule`(AppTestDoubles)이 만드는 Candidate에 Task 3에서 넣은 설명("테스트 설명"/"테스트 효과"/"테스트 주의")과 `SafetyLevel.Safe`가 있어야 한다. `FindTextBlocks`는 이 파일에 이미 있는 시각 트리 순회 헬퍼를 쓴다(없으면 `LogicalTreeHelper`/`VisualTreeHelper`로 `TextBlock`을 모으는 `private static IEnumerable<TextBlock> FindTextBlocks(DependencyObject root)`를 추가).

- [ ] **Step 2: 실패 확인** — Run: `--filter "FullyQualifiedName~ExposesExplanationAndSafetyBadge|FullyQualifiedName~CardRendersSafetyBadge"`. Expected: 컴파일 오류(속성 없음).

- [ ] **Step 3: VM 구현** — `FindingCardViewModel.cs`에 추가(속성 영역):

```csharp
    /// <summary>설명 3줄이 있는지 여부.</summary>
    public bool HasExplanation => Finding.Explanation is not null;

    /// <summary>"이게 뭔가요" 한 줄.</summary>
    public string? ExplainWhat => Finding.Explanation?.What;

    /// <summary>효과 한 줄.</summary>
    public string? ExplainEffect => Finding.Explanation?.Effect;

    /// <summary>주의 한 줄.</summary>
    public string? ExplainCaution => Finding.Explanation?.Caution;

    /// <summary>안전 수준(없으면 null).</summary>
    public SafetyLevel? Safety => Finding.Safety;

    /// <summary>안전 배지가 있는지 여부.</summary>
    public bool HasSafety => Finding.Safety is not null;

    /// <summary>안전 배지 텍스트(색과 무관하게 항상 표시).</summary>
    public string? SafetyText => Finding.Safety switch
    {
        SafetyLevel.Safe => Strings.Safety_Safe,
        SafetyLevel.Caution => Strings.Safety_Caution,
        SafetyLevel.Irreversible => Strings.Safety_Irreversible,
        _ => null,
    };
```

- [ ] **Step 4: XAML 수정** — `MainWindow.xaml` 리소스에 스타일 추가:

```xml
        <Style x:Key="SafetyBadge" TargetType="Border">
            <Setter Property="Background" Value="#DDF3E8" /><Setter Property="CornerRadius" Value="5" /><Setter Property="Padding" Value="8,3" /><Setter Property="Margin" Value="0,0,6,0" />
            <Style.Triggers>
                <DataTrigger Binding="{Binding Safety}" Value="{x:Static models:SafetyLevel.Caution}"><Setter Property="Background" Value="#FFF0CC" /></DataTrigger>
                <DataTrigger Binding="{Binding Safety}" Value="{x:Static models:SafetyLevel.Irreversible}"><Setter Property="Background" Value="#FFD9D9" /></DataTrigger>
            </Style.Triggers>
        </Style>
```

카드 템플릿의 `DockPanel`(분류·판정 배지 줄)에 안전 배지를 판정 배지 왼쪽에 넣는다:

```xml
                    <DockPanel Margin="0,0,0,10">
                        <Border DockPanel.Dock="Right" Style="{StaticResource VerdictBadge}">
                            <TextBlock TextWrapping="Wrap" Text="{Binding VerdictText}" Foreground="#263650" FontSize="12" />
                        </Border>
                        <Border DockPanel.Dock="Right" Style="{StaticResource SafetyBadge}" Visibility="{Binding HasSafety, Converter={StaticResource BoolToVisibility}}">
                            <TextBlock TextWrapping="Wrap" Text="{Binding SafetyText}" Foreground="#263650" FontSize="12" AutomationProperties.Name="{Binding SafetyText}" />
                        </Border>
                        <TextBlock TextWrapping="Wrap" Text="{Binding CategoryText}" Style="{StaticResource Muted}" FontSize="12" VerticalAlignment="Center" />
                    </DockPanel>
```

제목 아래, 기대 효과 상자 앞에 설명 3줄 블록을 넣는다:

```xml
                        <Grid Margin="0,10,0,0" Visibility="{Binding HasExplanation, Converter={StaticResource BoolToVisibility}}">
                            <Grid.ColumnDefinitions><ColumnDefinition Width="Auto" /><ColumnDefinition Width="*" /></Grid.ColumnDefinitions>
                            <Grid.RowDefinitions><RowDefinition /><RowDefinition /><RowDefinition /></Grid.RowDefinitions>
                            <TextBlock Grid.Row="0" Text="{x:Static res:Strings.Card_ExplainWhatHeader}" Style="{StaticResource Muted}" FontSize="12" Margin="0,0,10,2" />
                            <TextBlock Grid.Row="0" Grid.Column="1" TextWrapping="Wrap" Text="{Binding ExplainWhat}" FontSize="14" />
                            <TextBlock Grid.Row="1" Text="{x:Static res:Strings.Card_ExplainEffectHeader}" Style="{StaticResource Muted}" FontSize="12" Margin="0,0,10,2" />
                            <TextBlock Grid.Row="1" Grid.Column="1" TextWrapping="Wrap" Text="{Binding ExplainEffect}" FontSize="14" />
                            <TextBlock Grid.Row="2" Text="{x:Static res:Strings.Card_ExplainCautionHeader}" Style="{StaticResource Muted}" FontSize="12" Margin="0,0,10,2" />
                            <TextBlock Grid.Row="2" Grid.Column="1" TextWrapping="Wrap" Text="{Binding ExplainCaution}" FontSize="14" />
                        </Grid>
```

기존 `Evidence`(Candidate일 때 카드 본문에 보이던 줄)와 `SideEffectText`는 본문에서 제거하고 자세히 상자에만 남긴다(자세히 상자에는 이미 `Evidence`·`BenefitText`가 있으므로 `SideEffectText`를 그 아래 추가). `Strings.resx`에 위 6키를 추가한다.

- [ ] **Step 5: 통과 확인** — 위 필터 PASS, 이어서 `--filter "FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~FindingCardViewModelTests"` 전체 PASS.

- [ ] **Step 6: 커밋**

```bash
git add src/PcOptimizer.App tests/PcOptimizer.Tests/Unit/App
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 카드에 설명 3줄과 안전 배지 표시, 원시 근거·부작용은 자세히로 이동

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: 요약 타일 — "바로 할 수 있는 것 / 직접 해야 하는 것", 도구 카드 노출 조건

**Files:**
- Create: `src/PcOptimizer.App/Services/IActionAvailability.cs`
- Create: `src/PcOptimizer.App/Services/CacheToolActionAvailability.cs`
- Modify: `src/PcOptimizer.App/ViewModels/MainViewModel.cs`, `MainViewModel.Overview.cs`, `Views/MainWindow.xaml`(타일 3개 영역), `Resources/Strings.resx`, `App.xaml.cs`
- Test: `tests/PcOptimizer.Tests/Unit/App/ActionAvailabilityTests.cs`, `MainViewModelTests.cs`

**Interfaces:**
- Produces: `public interface IActionAvailability { bool CanExecuteInApp(Finding finding); }` — SP1이 실행기 등록으로 확장한다. `CacheToolActionAvailability(Func<bool> anyToolInProtectedLocation)`: Finding.Id가 `appCache.app:`로 시작하고 검토 규칙(supplement) 카드이며 도구가 보호 위치에 있을 때만 true(Task 5 시점의 유일한 앱 내 실행).
- VM: `int DoNowCount`, `int DoManuallyCount`, `string DoNowText`, `string DoManuallyText`, `bool HasDoNow`(0이면 타일 숨김), `bool CanOpenCacheTools`(기존)에 "도구가 보호 위치에 있음" 조건 추가. 기존 `SettingsCount`/`DriverCount` 타일은 제거하고 `DriverCount`의 온라인 상태 문구는 `LastOnlineCheckText`(옵션 영역)에만 남긴다 → REV-014의 표면(드라이버 타일)이 사라지므로 원장에 "타일 제거로 해소, 온라인 상태는 옵션 영역 문구로만" 기록.

- [ ] **Step 1: 실패하는 테스트 작성**

`tests/PcOptimizer.Tests/Unit/App/ActionAvailabilityTests.cs`:

```csharp
/**
 * @file    : ActionAvailabilityTests.cs
 * @author  : rudals252
 * @brief   : 앱 안에서 바로 실행할 수 있는 후보 판정(도구 캐시 카드·보호 위치 도구 조건)을 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;
using PcOptimizer.Core.Models;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>앱 내 실행 가능 판정을 검증합니다.</summary>
public sealed class ActionAvailabilityTests
{
    private static Finding Card(string id, Verdict verdict) => new(id, FindingCategory.AppCache, "제목", [], "근거", verdict, null, null,
        verdict == Verdict.Candidate ? new Recommendation("권고", "조건") : null, null, [],
        verdict == Verdict.Candidate ? new Explanation("무엇", "효과", "주의") : null, verdict == Verdict.Candidate ? SafetyLevel.Safe : null);

    /// <summary>도구가 보호 위치에 있고 검토 규칙 앱 카드일 때만 바로 실행 가능하다.</summary>
    [Theory]
    [InlineData("appCache.app:npm", true, true)]
    [InlineData("appCache.app:npm", false, false)]
    [InlineData("display.refresh:display-1", true, false)]
    public void OnlyReviewedCacheCardsWithProtectedToolsAreExecutable(string id, bool toolInstalled, bool expected)
    {
        var availability = new CacheToolActionAvailability(() => toolInstalled, reviewedAppIds: ["npm", "pip", "nuget"]);
        Assert.Equal(expected, availability.CanExecuteInApp(Card(id, Verdict.Candidate)));
    }
}
```

`MainViewModelTests.cs`에 추가(기존 `CreateViewModel` 헬퍼에 `IActionAvailability` 인자를 추가하고 기본은 `new FixedActionAvailability(false)` — AppTestDoubles에 `internal sealed class FixedActionAvailability(bool value) : IActionAvailability { public bool CanExecuteInApp(Finding f) => value; }`를 추가):

```csharp
    /// <summary>요약 타일은 바로 할 수 있는 후보와 직접 해야 하는 후보를 나눠 세고, 바로 할 수 있는 것이 0이면 그 타일을 숨긴다.</summary>
    [Fact]
    public async Task OverviewCountsDoNowAndDoManually()
    {
        var vm = CreateViewModel(rule: new ImprovementRule(), availability: new FixedActionAvailability(false));
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(0, vm.DoNowCount);
        Assert.False(vm.HasDoNow);
        Assert.Equal(vm.RecommendedCards.Count, vm.DoManuallyCount);

        var vm2 = CreateViewModel(rule: new ImprovementRule(), availability: new FixedActionAvailability(true));
        await vm2.StartScanCommand.ExecuteAsync(null);
        Assert.Equal(vm2.RecommendedCards.Count, vm2.DoNowCount);
        Assert.True(vm2.HasDoNow);
        Assert.Equal(0, vm2.DoManuallyCount);
    }
```

- [ ] **Step 2: 실패 확인** — Run: `--filter "FullyQualifiedName~ActionAvailabilityTests|FullyQualifiedName~OverviewCountsDoNowAndDoManually"`. Expected: 컴파일 오류.

- [ ] **Step 3: 구현**

`src/PcOptimizer.App/Services/IActionAvailability.cs`:

```csharp
/**
 * @file    : IActionAvailability.cs
 * @author  : rudals252
 * @brief   : 후보(Finding)를 앱 안에서 바로 실행할 수 있는지 판정하는 계약. SP1 실행기 등록으로 확장된다
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.Services;

/// <summary>후보를 앱 안에서 바로 실행할 수 있는지 판정합니다.</summary>
public interface IActionAvailability
{
    /// <summary>앱 안에서 바로 실행할 수 있으면 true, 사용자가 다른 곳에서 직접 해야 하면 false.</summary>
    /// <param name="finding">후보 Finding.</param>
    bool CanExecuteInApp(Finding finding);
}
```

`src/PcOptimizer.App/Services/CacheToolActionAvailability.cs`:

```csharp
/**
 * @file    : CacheToolActionAvailability.cs
 * @author  : rudals252
 * @brief   : SP4 시점의 유일한 앱 내 실행(npm·pip·NuGet 도구 캐시 정리) 판정. 검토 규칙 앱 카드이고 도구가 보호 위치에 있을 때만 true
 */

// 사용자 패키지
using PcOptimizer.Core.Models;

namespace PcOptimizer.App.Services;

/// <summary>도구 캐시 정리 카드의 앱 내 실행 가능 여부를 판정합니다.</summary>
public sealed class CacheToolActionAvailability : IActionAvailability
{
    /// <summary>앱 캐시 앱 카드 ID 접두.</summary>
    public const string APP_CARD_PREFIX = "appCache.app:";

    private readonly Func<bool> _anyToolInProtectedLocation;
    private readonly HashSet<string> _reviewedAppIds;

    /// <summary>판정기를 만듭니다.</summary>
    /// <param name="anyToolInProtectedLocation">보호 위치(Program Files)에 있는 npm/pip/dotnet 중 하나라도 찾았는지 돌려주는 함수(실행 시점마다 평가).</param>
    /// <param name="reviewedAppIds">검토 규칙 앱 식별자(예: npm, pip, nuget). 대소문자 무시.</param>
    public CacheToolActionAvailability(Func<bool> anyToolInProtectedLocation, IEnumerable<string> reviewedAppIds)
    {
        ArgumentNullException.ThrowIfNull(anyToolInProtectedLocation);
        ArgumentNullException.ThrowIfNull(reviewedAppIds);
        _anyToolInProtectedLocation = anyToolInProtectedLocation;
        _reviewedAppIds = new HashSet<string>(reviewedAppIds, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool CanExecuteInApp(Finding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        if (finding.Verdict != Verdict.Candidate || !finding.Id.StartsWith(APP_CARD_PREFIX, StringComparison.Ordinal))
        {
            return false;
        }

        var appId = finding.Id[APP_CARD_PREFIX.Length..];
        return _reviewedAppIds.Contains(appId) && _anyToolInProtectedLocation();
    }
}
```

`MainViewModel` 생성자에 `IActionAvailability actionAvailability` 매개변수를 추가(필드 `_actionAvailability`), `MainViewModel.Overview.cs`에:

```csharp
    /// <summary>앱 안에서 바로 실행할 수 있는 후보 수.</summary>
    public int DoNowCount => RecommendedCards.Count(card => _actionAvailability.CanExecuteInApp(card.Finding));

    /// <summary>사용자가 다른 곳에서 직접 해야 하는 후보 수.</summary>
    public int DoManuallyCount => RecommendedCards.Count - DoNowCount;

    /// <summary>바로 할 수 있는 것이 하나라도 있는지(0이면 타일을 숨김).</summary>
    public bool HasDoNow => DoNowCount > 0;

    /// <summary>바로 할 수 있는 것 타일 문구.</summary>
    public string DoNowText => DisplayText.Format(Strings.Overview_DoNowCount, DoNowCount);

    /// <summary>직접 해야 하는 것 타일 문구.</summary>
    public string DoManuallyText => DisplayText.Format(Strings.Overview_DoManuallyCount, DoManuallyCount);
```

결과 적용(`ApplyResult`)과 카드 목록 갱신 지점에서 `OnPropertyChanged(nameof(DoNowCount))` 등 5개 알림을 추가한다. `SettingsCount`·`DriverCount` 속성과 그 리소스 사용처를 제거한다. `MainWindow.xaml`의 타일 3개 영역을 "바로 할 수 있는 것"(Visibility=`HasDoNow`)과 "직접 해야 하는 것" 두 타일로 바꾸고 각 타일에 `Overview_DoNowHelp`("앱에서 확인 후 바로 실행합니다"), `Overview_DoManuallyHelp`("Windows 설정이나 공식 페이지에서 직접 합니다")를 붙인다. `CanOpenCacheTools`는 `&& _actionAvailability.CanExecuteInApp(...)`가 아니라 `CacheToolsAvailable`(생성 시 `anyToolInProtectedLocation()` 한 번 평가한 값)로 게이트한다. `App.xaml.cs`에서 `new CacheToolActionAvailability(() => SystemCacheToolBackend.AnyToolInProtectedLocation(), ["npm", "pip", "nuget"])`를 만들어 넘긴다 — `SystemCacheToolBackend`에 `public static bool AnyToolInProtectedLocation()`이 없으면 기존 `LocateAsync`의 보호 위치 탐색만 동기로 재사용하는 정적 메서드를 추가한다(경로 존재 확인만, 프로세스 실행 없음).

리소스 추가: `Overview_DoNowCount`="바로 할 수 있는 것 {0}건", `Overview_DoManuallyCount`="직접 해야 하는 것 {0}건", `Overview_DoNowHelp`, `Overview_DoManuallyHelp`.

- [ ] **Step 4: 통과 확인** — 위 필터 PASS, `--filter "FullyQualifiedName~MainViewModelTests|FullyQualifiedName~MainWindowLayoutTests"` PASS(제거된 `DriverCount` 관련 테스트는 삭제하고 `LastOnlineCheckText` 검증으로 대체).

- [ ] **Step 5: 원장 기록** — `docs/reviews/REVIEW_LEDGER.md` REV-014 대응 기록에 "드라이버 타일 제거(SP4 Task 5), 온라인 상태는 옵션 영역 `LastOnlineCheckText`만 사용, 로컬 CannotVerify가 온라인 완료 판정에 섞이는 코드 경로 삭제" 추가, 상태 `수정됨·재검증 대기`. REV-015 대응 기록에 "요약 타일 교체·도구 카드/정리 창 노출 조건 구현(커밋 SHA)" 추가.

- [ ] **Step 6: 커밋**

```bash
git add src/PcOptimizer.App tests/PcOptimizer.Tests/Unit/App docs/reviews/REVIEW_LEDGER.md
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 요약 타일을 바로 할 수 있는 것/직접 해야 하는 것으로 교체, 도구 정리 노출 조건

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: SystemDetailsProbe — OS·CPU·BIOS 날짜·네트워크 어댑터

**Files:**
- Create: `src/PcOptimizer.Core/Rules/SystemDetailsProbeContract.cs`
- Create: `src/PcOptimizer.Probes/Hardware/SystemDetailsProbe.cs`
- Modify: `src/PcOptimizer.Probes/Resources/ProbeStrings.resx` (`SystemDetails_OsMissing`, `SystemDetails_CpuMissing`)
- Test: `tests/PcOptimizer.Tests/Unit/Probes/SystemDetailsProbeTests.cs`

**Interfaces:**
- Produces: 계약 상수 `PROBE_ID="hardware.systemDetails"`, `OS_CAPTION="os.caption"`, `OS_VERSION="os.version"`, `OS_BUILD="os.buildNumber"`, `OS_INSTALL_DATE="os.installDate"`(ISO 8601 텍스트), `CPU_NAME="cpu.name"`, `CPU_CORES="cpu.cores"`, `CPU_THREADS="cpu.threads"`, `CPU_MAX_CLOCK_MHZ="cpu.maxClockMHz"`, `BIOS_RELEASE_DATE="system.biosReleaseDate"`, `BOARD_MANUFACTURER="board.manufacturer"`, `BOARD_PRODUCT="board.product"`, `BOARD_VERSION="board.version"`, `NIC_COUNT="network.adapterCount"`, `NIC_PREFIX="network.adapter"`, 필드 `name`, `adapterType`, `manufacturer`, `netEnabled`, `driverVersion`, 헬퍼 `AdapterMeasurementName(int index, string field)`. 프로브: `Category=FindingCategory.Driver`, `Scope=System`, `RequiresElevation=false`, `RequiresNetwork=false`.

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
/**
 * @file    : SystemDetailsProbeTests.cs
 * @author  : rudals252
 * @brief   : OS·CPU·BIOS 날짜·물리 네트워크 어댑터를 가짜 WMI 행으로 수집하고, 일부 실패 시 Partial, 전부 실패 시 Failed인지 검증
 */

// 사용자 패키지
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Hardware;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Tests.Unit.Probes.Fakes;

namespace PcOptimizer.Tests.Unit.Probes;

/// <summary>시스템 상세 프로브를 검증합니다.</summary>
public sealed class SystemDetailsProbeTests
{
    private static ScanContext Context() => new(Guid.NewGuid(), isElevated: true, onlineCheckRequested: false,
        new UserContext("anon", true), DateTimeOffset.UtcNow);

    /// <summary>네 클래스가 모두 있으면 Success이고 측정 이름이 계약대로다.</summary>
    [Fact]
    public async Task CollectsAllSections()
    {
        var wmi = new FakeWmiClient()
            .WithRows(SystemDetailsProbe.OS_CLASS, new Dictionary<string, object?> { ["Caption"] = "Microsoft Windows 11 Pro", ["Version"] = "10.0.26200", ["BuildNumber"] = "26200", ["InstallDate"] = "20260101120000.000000+540" })
            .WithRows(SystemDetailsProbe.CPU_CLASS, new Dictionary<string, object?> { ["Name"] = "AMD Ryzen 7 9800X3D", ["NumberOfCores"] = 8u, ["NumberOfLogicalProcessors"] = 16u, ["MaxClockSpeed"] = 4700u })
            .WithRows(SystemDetailsProbe.BIOS_CLASS, new Dictionary<string, object?> { ["ReleaseDate"] = "20260815000000.000000+000" })
            .WithRows(SystemDetailsProbe.BOARD_CLASS, new Dictionary<string, object?> { ["Manufacturer"] = "ASUSTeK COMPUTER INC.", ["Product"] = "ROG STRIX B650E-F GAMING WIFI", ["Version"] = "Rev 1.xx" })
            .WithRows(SystemDetailsProbe.NIC_CLASS,
                new Dictionary<string, object?> { ["Name"] = "Intel(R) Ethernet Controller I226-V", ["AdapterTypeId"] = 0u, ["Manufacturer"] = "Intel", ["NetEnabled"] = true, ["PhysicalAdapter"] = true, ["PNPDeviceID"] = "PCI\\VEN_8086&DEV_125C\\1" },
                new Dictionary<string, object?> { ["Name"] = "WAN Miniport (IP)", ["PhysicalAdapter"] = false })
            .WithRows(SystemDetailsProbe.DRIVER_CLASS, new Dictionary<string, object?> { ["DeviceID"] = "PCI\\VEN_8086&DEV_125C\\1", ["DriverVersion"] = "2.1.4.3", ["DeviceClass"] = "NET" });

        var result = await new SystemDetailsProbe(wmi, new ManualTimeProvider()).RunAsync(Context(), CancellationToken.None);

        Assert.Equal(ProbeStatus.Success, result.Status);
        Assert.Equal("Microsoft Windows 11 Pro", Text(result, SystemDetailsProbeContract.OS_CAPTION));
        Assert.Equal("2026-01-01T12:00:00+09:00", Text(result, SystemDetailsProbeContract.OS_INSTALL_DATE));
        Assert.Equal(8, Integer(result, SystemDetailsProbeContract.CPU_CORES));
        Assert.Equal("2026-08-15", Text(result, SystemDetailsProbeContract.BIOS_RELEASE_DATE));
        Assert.Equal("ROG STRIX B650E-F GAMING WIFI", Text(result, SystemDetailsProbeContract.BOARD_PRODUCT));
        Assert.Equal(1, Integer(result, SystemDetailsProbeContract.NIC_COUNT));
        Assert.Equal("2.1.4.3", Text(result, SystemDetailsProbeContract.AdapterMeasurementName(0, SystemDetailsProbeContract.FIELD_DRIVER_VERSION)));
    }

    /// <summary>CPU 조회만 실패하면 Partial이고 사유가 남는다. 모두 실패하면 Failed.</summary>
    [Fact]
    public async Task PartialAndFailed()
    {
        var partial = new FakeWmiClient()
            .WithRows(SystemDetailsProbe.OS_CLASS, new Dictionary<string, object?> { ["Caption"] = "W", ["Version"] = "10.0", ["BuildNumber"] = "1" })
            .WithFailure(SystemDetailsProbe.CPU_CLASS, WmiQueryStatus.ClassUnavailable)
            .WithRows(SystemDetailsProbe.BIOS_CLASS, new Dictionary<string, object?> { ["ReleaseDate"] = "20260815000000.000000+000" })
            .WithRows(SystemDetailsProbe.BOARD_CLASS)
            .WithRows(SystemDetailsProbe.NIC_CLASS)
            .WithRows(SystemDetailsProbe.DRIVER_CLASS);
        var result = await new SystemDetailsProbe(partial, new ManualTimeProvider()).RunAsync(Context(), CancellationToken.None);
        Assert.Equal(ProbeStatus.Partial, result.Status);
        Assert.Contains(result.Issues, i => i.Reason == CannotVerifyReason.ProbeError || i.Reason == CannotVerifyReason.Unsupported);

        var failed = new FakeWmiClient()
            .WithFailure(SystemDetailsProbe.OS_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.CPU_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.BIOS_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.BOARD_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.NIC_CLASS, WmiQueryStatus.AccessDenied)
            .WithFailure(SystemDetailsProbe.DRIVER_CLASS, WmiQueryStatus.AccessDenied);
        var failedResult = await new SystemDetailsProbe(failed, new ManualTimeProvider()).RunAsync(Context(), CancellationToken.None);
        Assert.Equal(ProbeStatus.Failed, failedResult.Status);
    }

    private static string? Text(ProbeResult result, string name) =>
        (result.Measurements.FirstOrDefault(m => m.Name == name)?.Value as TextValue)?.Value;

    private static long? Integer(ProbeResult result, string name) =>
        (result.Measurements.FirstOrDefault(m => m.Name == name)?.Value as IntegerValue)?.Value;
}
```

`ManualTimeProvider`가 `IClock`을 구현하지 않으면 기존 프로브 테스트가 쓰는 시계 페이크(`FixedClock` 등)를 쓴다(`grep -rn "IClock" tests/PcOptimizer.Tests/Unit/Probes/Fakes`). `ScanContext`·`UserContext` 생성자 시그니처는 `src/PcOptimizer.Core/Models/ScanContext.cs`를 확인해 맞춘다.

- [ ] **Step 2: 실패 확인** — Run: `--filter "FullyQualifiedName~SystemDetailsProbeTests"`. Expected: 컴파일 오류.

- [ ] **Step 3: 구현**

`src/PcOptimizer.Core/Rules/SystemDetailsProbeContract.cs`:

```csharp
/**
 * @file    : SystemDetailsProbeContract.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 섹션용 시스템 상세 프로브의 ID·측정 이름 계약(OS, CPU, BIOS 날짜, 물리 네트워크 어댑터)
 */

namespace PcOptimizer.Core.Rules;

/// <summary>시스템 상세 프로브의 측정 이름 계약입니다.</summary>
public static class SystemDetailsProbeContract
{
    /// <summary>프로브 ID.</summary>
    public const string PROBE_ID = "hardware.systemDetails";

    /// <summary>OS 이름(에디션 포함).</summary>
    public const string OS_CAPTION = "os.caption";

    /// <summary>OS 버전 문자열.</summary>
    public const string OS_VERSION = "os.version";

    /// <summary>OS 빌드 번호.</summary>
    public const string OS_BUILD = "os.buildNumber";

    /// <summary>OS 설치일(ISO 8601, 오프셋 포함).</summary>
    public const string OS_INSTALL_DATE = "os.installDate";

    /// <summary>CPU 모델명.</summary>
    public const string CPU_NAME = "cpu.name";

    /// <summary>물리 코어 수.</summary>
    public const string CPU_CORES = "cpu.cores";

    /// <summary>논리 프로세서 수.</summary>
    public const string CPU_THREADS = "cpu.threads";

    /// <summary>최대 클럭(MHz).</summary>
    public const string CPU_MAX_CLOCK_MHZ = "cpu.maxClockMHz";

    /// <summary>BIOS 배포일(yyyy-MM-dd).</summary>
    public const string BIOS_RELEASE_DATE = "system.biosReleaseDate";

    /// <summary>메인보드 제조사.</summary>
    public const string BOARD_MANUFACTURER = "board.manufacturer";

    /// <summary>메인보드 제품명(모델).</summary>
    public const string BOARD_PRODUCT = "board.product";

    /// <summary>메인보드 버전/리비전.</summary>
    public const string BOARD_VERSION = "board.version";

    /// <summary>물리 네트워크 어댑터 수.</summary>
    public const string NIC_COUNT = "network.adapterCount";

    /// <summary>어댑터 측정 이름 접두.</summary>
    public const string NIC_PREFIX = "network.adapter";

    /// <summary>어댑터 이름 필드.</summary>
    public const string FIELD_NAME = "name";

    /// <summary>어댑터 종류 필드(WMI AdapterTypeId 원시 값).</summary>
    public const string FIELD_ADAPTER_TYPE = "adapterType";

    /// <summary>제조사 필드.</summary>
    public const string FIELD_MANUFACTURER = "manufacturer";

    /// <summary>사용 중 여부 필드.</summary>
    public const string FIELD_NET_ENABLED = "netEnabled";

    /// <summary>드라이버 버전 필드.</summary>
    public const string FIELD_DRIVER_VERSION = "driverVersion";

    /// <summary>단위: MHz.</summary>
    public const string UNIT_MEGAHERTZ = "MHz";

    /// <summary>어댑터 인덱스·필드로 측정 이름을 만듭니다(예: network.adapter[0].name).</summary>
    public static string AdapterMeasurementName(int index, string field) => $"{NIC_PREFIX}[{index}].{field}";
}
```

`src/PcOptimizer.Probes/Hardware/SystemDetailsProbe.cs` — `SystemInfoProbe`와 같은 구조. 핵심:

```csharp
/**
 * @file    : SystemDetailsProbe.cs
 * @author  : rudals252
 * @brief   : Win32_OperatingSystem·Win32_Processor·Win32_BIOS(ReleaseDate)·Win32_NetworkAdapter(PhysicalAdapter)·Win32_PnPSignedDriver(NET)로 내 PC 사양 섹션의 상세 값을 수집. 일련번호·MAC은 수집하지 않음
 */

// 기본 패키지
using System.Globalization;

// 사용자 패키지
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;
using PcOptimizer.Probes.Platform;
using PcOptimizer.Probes.Resources;

namespace PcOptimizer.Probes.Hardware;

/// <summary>내 PC 사양 섹션용 시스템 상세 프로브입니다. 판정은 하지 않습니다.</summary>
public sealed class SystemDetailsProbe : IProbe
{
    /// <summary>OS 클래스.</summary>
    public const string OS_CLASS = "Win32_OperatingSystem";
    /// <summary>CPU 클래스.</summary>
    public const string CPU_CLASS = "Win32_Processor";
    /// <summary>BIOS 클래스.</summary>
    public const string BIOS_CLASS = "Win32_BIOS";
    /// <summary>메인보드 클래스.</summary>
    public const string BOARD_CLASS = "Win32_BaseBoard";
        /// <summary>네트워크 어댑터 클래스.</summary>
    public const string NIC_CLASS = "Win32_NetworkAdapter";
    /// <summary>서명 드라이버 클래스(NET 클래스 드라이버 버전).</summary>
    public const string DRIVER_CLASS = "Win32_PnPSignedDriver";

    /// <summary>WMI 제공자 타임아웃.</summary>
    public static readonly TimeSpan WMI_PROVIDER_TIMEOUT = TimeSpan.FromSeconds(4);

    private const string DRIVER_CLASS_NET = "NET";
    private const string WMI_DATETIME_FORMAT = "yyyyMMddHHmmss.ffffff";
    private const int WMI_DATETIME_LENGTH = 25;

    private static readonly string[] OS_PROPERTIES = ["Caption", "Version", "BuildNumber", "InstallDate"];
    private static readonly string[] CPU_PROPERTIES = ["Name", "NumberOfCores", "NumberOfLogicalProcessors", "MaxClockSpeed"];
    private static readonly string[] BIOS_PROPERTIES = ["ReleaseDate"];
    private static readonly string[] BOARD_PROPERTIES = ["Manufacturer", "Product", "Version"];
    private static readonly string[] NIC_PROPERTIES = ["Name", "AdapterTypeId", "Manufacturer", "NetEnabled", "PhysicalAdapter", "PNPDeviceID"];
    private static readonly string[] DRIVER_PROPERTIES = ["DeviceID", "DriverVersion", "DeviceClass"];

    private readonly IWmiClient _wmi;
    private readonly IClock _clock;

    /// <summary>실제 WMI와 시스템 시계를 쓰는 프로브.</summary>
    public SystemDetailsProbe() : this(WmiClient.Instance, SystemClock.Instance) { }

    /// <summary>WMI 클라이언트와 시계를 지정합니다(테스트용).</summary>
    public SystemDetailsProbe(IWmiClient wmi, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(clock);
        _wmi = wmi;
        _clock = clock;
    }

    /// <inheritdoc />
    public string Id => SystemDetailsProbeContract.PROBE_ID;
    /// <inheritdoc />
    public FindingCategory Category => FindingCategory.Driver;
    /// <inheritdoc />
    public bool RequiresElevation => false;
    /// <inheritdoc />
    public bool RequiresNetwork => false;
    /// <inheritdoc />
    public ProbeScope Scope => ProbeScope.System;
    /// <inheritdoc />
    public TimeSpan DefaultTimeout => ScanOptions.DEFAULT_LOCAL_TIMEOUT;

    /// <inheritdoc />
    public Task<ProbeResult> RunAsync(ScanContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var observedAt = _clock.UtcNow;
        var measurements = new List<Measurement>();
        var issues = new List<Issue>();
        var sections = 0;
        sections += ReadOs(measurements, issues, observedAt, ct) ? 1 : 0;
        sections += ReadCpu(measurements, issues, observedAt, ct) ? 1 : 0;
        sections += ReadBiosDate(measurements, issues, observedAt, ct) ? 1 : 0;
        sections += ReadBoard(measurements, issues, observedAt, ct) ? 1 : 0;
        sections += ReadAdapters(measurements, issues, observedAt, ct) ? 1 : 0;
        var status = sections == 0 ? ProbeStatus.Failed : issues.Count == 0 ? ProbeStatus.Success : ProbeStatus.Partial;
        return Task.FromResult(new ProbeResult(Id, status, measurements, issues, observedAt, TimeSpan.Zero, context.UserContext));
    }

    // ReadOs: Query(OS_CLASS) 실패/빈 행 → issues에 WmiResultInterpreter.ToIssue/EmptyIssue, false. 성공: Caption/Version/BuildNumber를 TextValue로,
    // InstallDate는 TryParseWmiDateTime으로 DateTimeOffset 변환 후 "o" 형식(ISO 8601)의 TextValue. 변환 실패 시 그 측정만 생략하고 PartialData Issue.
    // ReadCpu: 첫 행의 Name(Text), NumberOfCores/NumberOfLogicalProcessors/MaxClockSpeed(IntegerValue, uint→long; MaxClock은 Unit=UNIT_MEGAHERTZ).
    // ReadBiosDate: ReleaseDate를 파싱해 yyyy-MM-dd TextValue.
    // ReadBoard: BOARD_CLASS 첫 행의 Manufacturer/Product/Version을 TextValue로(BOARD_MANUFACTURER/BOARD_PRODUCT/BOARD_VERSION). 값이 "To be filled by O.E.M."·"Default string"·공백이면 생략하고 PartialData Issue.
    // ReadAdapters: NIC_CLASS 행 중 PhysicalAdapter == true만. DRIVER_CLASS 행을 DeviceClass == "NET"로 걸러 DeviceID→DriverVersion 사전을 만들고
    //   PNPDeviceID로 조인(OrdinalIgnoreCase). 어댑터마다 name/adapterType(AdapterTypeId 원시 정수)/manufacturer/netEnabled(BooleanValue)/driverVersion(있을 때만).
    //   NIC_COUNT = 물리 어댑터 수. DRIVER_CLASS 조회 실패는 Issue만 남기고 어댑터 자체는 기록(드라이버 버전 없이).
    // 모든 Source는 "WMI <클래스>.<속성>" 형식, Quality는 Reported.

    /// <summary>WMI CIM_DATETIME(yyyyMMddHHmmss.ffffff±UUU)을 DateTimeOffset으로 변환합니다.</summary>
    internal static bool TryParseWmiDateTime(string? text, out DateTimeOffset value)
    {
        value = default;
        if (text is null || text.Length != WMI_DATETIME_LENGTH) { return false; }
        var body = text[..WMI_DATETIME_FORMAT.Length];
        var offsetText = text[WMI_DATETIME_FORMAT.Length..];
        if (!DateTime.TryParseExact(body, WMI_DATETIME_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) { return false; }
        if (!int.TryParse(offsetText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offsetMinutes)) { return false; }
        value = new DateTimeOffset(local, TimeSpan.FromMinutes(offsetMinutes));
        return true;
    }
}
```

`ReadOs`/`ReadCpu`/`ReadBiosDate`/`ReadAdapters`는 주석에 적은 규칙대로 각각 구현한다(`SystemInfoProbe.ReadComputerSystem` 패턴 그대로: `_wmi.Query(class, props, WMI_PROVIDER_TIMEOUT, ct)`, `WmiResultInterpreter.GetText(row, prop)`, 정수는 `row[prop]`을 `Convert.ToInt64(value, CultureInfo.InvariantCulture)`로 변환하되 null이면 생략). `ProbeStrings.resx`에 `SystemDetails_OsMissing`="운영체제 정보를 읽지 못했습니다.", `SystemDetails_CpuMissing`="CPU 정보를 읽지 못했습니다." 추가. 이 프로브는 `ScanService.CreateDefault`의 프로브 목록에도 추가한다(진단 리포트에 사양이 포함되도록; 규칙은 없음).

- [ ] **Step 4: 통과 확인** — 위 필터 PASS. `ScanServiceTests`의 등록 프로브 범위 테스트가 있으면 `hardware.systemDetails`=System 항목을 기대 표에 추가한다.

- [ ] **Step 5: 커밋**

```bash
git add src/PcOptimizer.Core/Rules/SystemDetailsProbeContract.cs src/PcOptimizer.Probes tests/PcOptimizer.Tests/Unit/Probes/SystemDetailsProbeTests.cs tests/PcOptimizer.Tests/Unit/App/ScanServiceTests.cs src/PcOptimizer.App/Services/ScanService.cs
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: SystemDetailsProbe(OS·CPU·BIOS 날짜·네트워크 어댑터) 추가

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: PcSpecService·PcSpecSnapshot — 사양 스냅샷 구성

**Files:**
- Create: `src/PcOptimizer.App/Models/PcSpecSnapshot.cs`
- Create: `src/PcOptimizer.App/Services/PcSpecService.cs`
- Test: `tests/PcOptimizer.Tests/Unit/App/PcSpecTests.cs`

**Interfaces:**
- Produces: `public sealed record PcSpecItem(string Label, string? Value, bool IsIdentifying = false)`(Value null ⇒ "확인 불가"), `public sealed record PcSpecSection(string Title, IReadOnlyList<PcSpecItem> Items)`, `public sealed record PcSpecSnapshot(DateTimeOffset CapturedAtUtc, IReadOnlyList<PcSpecSection> Sections, IReadOnlyList<string> UnavailableSections)`. `PcSpecService(IReadOnlyList<IProbe> probes, IClock clock, IAppLogger logger, Func<ScanContext> contextFactory)` with `Task<PcSpecSnapshot> CaptureAsync(CancellationToken ct)`; 정적 `PcSpecService.BuildSnapshot(IReadOnlyDictionary<string, ProbeResult> results, DateTimeOffset at)`(순수, 테스트 대상). 섹션 순서: 운영체제, CPU, 메모리, 그래픽, 모니터, 저장장치, 메인보드·BIOS, 네트워크, 전원. 식별 정보(`IsIdentifying=true`): 없음(SP4는 사용자명·PC 이름을 수집하지 않으므로 토글은 PC 이름·사용자명을 텍스트 헤더에 추가하는 용도로만 씀).

- [ ] **Step 1: 실패하는 테스트 작성** — `PcSpecTests.cs`(가짜 ProbeResult 사전으로 `BuildSnapshot`만 검증):

```csharp
    /// <summary>프로브 결과에서 9개 섹션을 순서대로 만들고, 없는 프로브는 확인 불가 섹션으로 표시한다.</summary>
    [Fact]
    public void BuildsSectionsInOrderAndMarksUnavailable()
    {
        var at = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        var results = new Dictionary<string, ProbeResult>
        {
            [SystemDetailsProbeContract.PROBE_ID] = Result(SystemDetailsProbeContract.PROBE_ID,
                (SystemDetailsProbeContract.OS_CAPTION, new TextValue("Windows 11 Pro")),
                (SystemDetailsProbeContract.CPU_NAME, new TextValue("Ryzen 7")),
                (SystemDetailsProbeContract.CPU_CORES, new IntegerValue(8)),
                (SystemDetailsProbeContract.CPU_THREADS, new IntegerValue(16))),
            [MemoryProbeContract.PROBE_ID] = Result(MemoryProbeContract.PROBE_ID,
                (MemoryProbeContract.MODULE_COUNT, new IntegerValue(1)),
                (MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_CAPACITY), new IntegerValue(25_769_803_776)),
                (MemoryProbeContract.ModuleMeasurementName(0, MemoryProbeContract.FIELD_CONFIGURED_CLOCK_SPEED), new IntegerValue(6000))),
        };

        var snapshot = PcSpecService.BuildSnapshot(results, at);

        Assert.Equal(["운영체제", "CPU", "메모리", "그래픽", "모니터", "저장장치", "메인보드·BIOS", "네트워크", "전원"], snapshot.Sections.Select(s => s.Title));
        Assert.Contains(snapshot.Sections[1].Items, i => i.Label == Strings.Spec_CpuCores && i.Value == "8코어 / 16스레드");
        Assert.Contains(snapshot.Sections[2].Items, i => i.Value == "24 GB · 6000 MT/s");
        Assert.Contains("그래픽", snapshot.UnavailableSections);
        Assert.All(snapshot.Sections[3].Items, i => Assert.Null(i.Value));
    }
```

`Result(...)` 헬퍼는 `(string name, MeasurementValue value)` 튜플로 `Measurement`(Source "test", Quality Reported)를 만들어 `ProbeResult(id, Success, ...)`를 돌려준다.

- [ ] **Step 2: 실패 확인** — 컴파일 오류.

- [ ] **Step 3: 구현** — `PcSpecSnapshot.cs`(레코드 3개, 헤더·주석 포함). `PcSpecService.cs`:

```csharp
/**
 * @file    : PcSpecService.cs
 * @author  : rudals252
 * @brief   : 하드웨어 프로브를 규칙 없이 직접 실행해 내 PC 사양 스냅샷(9개 섹션)을 만든다. 검사와 무관하게 열 때마다 새로 읽는다
 */

// 사용자 패키지
using PcOptimizer.App.Models;
using PcOptimizer.App.Resources;
using PcOptimizer.Core.Abstractions;
using PcOptimizer.Core.Models;
using PcOptimizer.Core.Rules;

namespace PcOptimizer.App.Services;

/// <summary>내 PC 사양 스냅샷을 만드는 서비스입니다.</summary>
public sealed class PcSpecService
{
    /// <summary>사양 섹션에 쓰는 프로브 ID(실행 순서).</summary>
    public static readonly string[] PROBE_IDS =
    [
        SystemDetailsProbeContract.PROBE_ID, SystemInfoProbeContract.PROBE_ID, MemoryProbeContract.PROBE_ID, GpuProbeContract.PROBE_ID,
        DisplayProbeContract.PROBE_ID, PhysicalDiskProbeContract.PROBE_ID, VolumeProbeContract.PROBE_ID, PowerProbeContract.PROBE_ID,
    ];

    private const long BYTES_PER_GIB = 1024L * 1024 * 1024;
    private const string LOG_CATEGORY = nameof(PcSpecService);

    private readonly IReadOnlyList<IProbe> _probes;
    private readonly IClock _clock;
    private readonly IAppLogger _logger;
    private readonly Func<ScanContext> _contextFactory;

    /// <summary>서비스를 만듭니다.</summary>
    /// <param name="probes">등록된 프로브 전체(이 중 PROBE_IDS만 실행).</param>
    /// <param name="clock">UTC 시계.</param>
    /// <param name="logger">공용 로거.</param>
    /// <param name="contextFactory">검사 컨텍스트 생성기(새 ScanId).</param>
    public PcSpecService(IReadOnlyList<IProbe> probes, IClock clock, IAppLogger logger, Func<ScanContext> contextFactory) { /* null 검사 후 대입 */ }

    /// <summary>프로브를 순서대로 실행해 스냅샷을 만듭니다. 프로브 예외는 형식 이름만 기록하고 해당 섹션을 확인 불가로 둡니다.</summary>
    public async Task<PcSpecSnapshot> CaptureAsync(CancellationToken ct)
    {
        var context = _contextFactory();
        var results = new Dictionary<string, ProbeResult>(StringComparer.Ordinal);
        foreach (var id in PROBE_IDS)
        {
            var probe = _probes.FirstOrDefault(p => p.Id == id);
            if (probe is null) { continue; }
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(probe.DefaultTimeout);
                results[id] = await probe.RunAsync(context, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { _logger.Warn(LOG_CATEGORY, $"SpecProbeTimeout probe={id}"); }
            catch (Exception ex) { _logger.Error(LOG_CATEGORY, $"SpecProbeFailed probe={id}", ex); }
        }

        return BuildSnapshot(results, _clock.UtcNow);
    }

    /// <summary>프로브 결과에서 스냅샷을 만듭니다(순수). 없는 프로브나 실패 결과는 섹션 전체를 확인 불가로 둡니다.</summary>
    public static PcSpecSnapshot BuildSnapshot(IReadOnlyDictionary<string, ProbeResult> results, DateTimeOffset capturedAtUtc)
    {
        var sections = new List<PcSpecSection>();
        var unavailable = new List<string>();
        sections.Add(Section(Strings.Spec_Section_Os, unavailable, Os(results)));
        sections.Add(Section(Strings.Spec_Section_Cpu, unavailable, Cpu(results)));
        sections.Add(Section(Strings.Spec_Section_Memory, unavailable, Memory(results)));
        sections.Add(Section(Strings.Spec_Section_Gpu, unavailable, Gpu(results)));
        sections.Add(Section(Strings.Spec_Section_Display, unavailable, Displays(results)));
        sections.Add(Section(Strings.Spec_Section_Storage, unavailable, Storage(results)));
        sections.Add(Section(Strings.Spec_Section_Board, unavailable, Board(results)));
        sections.Add(Section(Strings.Spec_Section_Network, unavailable, Network(results)));
        sections.Add(Section(Strings.Spec_Section_Power, unavailable, Power(results)));
        return new PcSpecSnapshot(capturedAtUtc, sections, unavailable);
    }

    // Section(title, unavailable, items): items의 Value가 전부 null이면 unavailable에 title 추가.
    // 각 빌더는 results에서 프로브 결과를 찾고(없거나 Status==Failed면 라벨만 있고 Value=null인 항목 목록), 측정값을 라벨·값 문자열로 바꾼다.
    // 라벨 리소스: Spec_OsName, Spec_OsVersion(버전·빌드 합침 "10.0.26200 (빌드 26200)"), Spec_OsInstallDate(yyyy-MM-dd), Spec_CpuName, Spec_CpuCores("{0}코어 / {1}스레드"), Spec_CpuClock("{0} MHz"),
    // Spec_MemoryTotal("{0} GB · {1} MT/s" — 모듈 용량 합계를 GiB로 반올림, 속도는 configuredClockSpeed 최소값), Spec_MemoryModule("{0}: {1} GB {2} {3}" — 위치·용량·제조사·파트 번호),
    // Spec_GpuAdapter("{0} · 드라이버 {1} ({2})" — 이름·버전·날짜 yyyy-MM-dd; 가상 어댑터 제외 없이 모두), Spec_Display("{0}: {1}x{2} @ {3}Hz" — 모니터 이름·해상도·주사율),
    // 저장장치 섹션: 물리 디스크마다 한 줄. 라벨은 종류(Spec_LabelSsd="SSD", Spec_LabelHdd="HDD", 기타는 mediaType 원문)이고 값은 "{모델명} · {용량} GB · {인터페이스} · {상태}"(Spec_Disk). 볼륨은 그 아래 Spec_Volume("{0} {1} GB 중 {2} GB 남음").
    // 메인보드·BIOS 섹션: Spec_BoardModel = BOARD_PRODUCT(없으면 SystemInfo MODEL로 대체하고 Spec_BoardFromSystem 표기), Spec_BoardMaker = BOARD_MANUFACTURER(없으면 SystemInfo MANUFACTURER), Spec_BoardVersion, Spec_BiosVersion("{0} ({1})" 버전·배포일),
    // 모니터 섹션: 대상마다 라벨 "모니터 {n}", 값 "{모니터 이름} · {가로}x{세로} @ {Hz}Hz"(이름 없으면 Display_FallbackName). 그래픽 섹션: 어댑터마다 라벨 "GPU {n}". 메모리 섹션: 총량 줄 + 모듈마다 라벨 "슬롯 {위치}" 값 "{용량} GB {속도} MT/s {제조사} {파트 번호}". CPU 섹션: 모델 줄 + 코어/스레드 줄 + 클럭 줄.
    // 사용자 요구(2026-09-27): CPU·메인보드·GPU·RAM·SSD·HDD·모니터 이름과 해상도를 전부 이름으로 나열한다. 요약만 보여주고 개별 장치를 생략하지 않는다.
    // Spec_Nic("{0} · 드라이버 {1}" — 이름·버전, 버전 없으면 Spec_ValueUnknown), Spec_PowerKind(노트북/데스크톱, PowerSupplyClassifier 재사용), Spec_PowerPlan.
    // 값이 없으면 PcSpecItem.Value = null (뷰가 Spec_ValueUnknown="확인 불가"로 표시). 숫자 형식은 CultureInfo.CurrentCulture, 날짜는 "yyyy-MM-dd".
}
```

`App.xaml.cs`에서 `new PcSpecService(scanService.Probes, SystemClock.Instance, logger, () => scanService.CreateContext(onlineCheckRequested: false))`로 만든다. `ScanService`에 `public ScanContext CreateContext(bool onlineCheckRequested)`가 없으면 `RunScanAsync` 내부의 컨텍스트 생성 코드를 그 메서드로 뽑아 재사용한다(동작 변경 없음).

`Strings.resx`에 `Spec_*` 키(위 주석의 목록 + `Spec_Section_*` 9개 + `Spec_LabelSsd`/`Spec_LabelHdd` + `Spec_BoardFromSystem`="(시스템 모델로 대체)" + `Spec_ValueUnknown`="확인 불가" + `Spec_MoreDetails`="더 자세히(HWiNFO64·CPU-Z 안내)")를 추가한다.

- [ ] **Step 4: 통과 확인** — `--filter "FullyQualifiedName~PcSpecTests"` PASS.

- [ ] **Step 5: 커밋**

```bash
git add src/PcOptimizer.App tests/PcOptimizer.Tests/Unit/App/PcSpecTests.cs
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: PcSpecService·PcSpecSnapshot — 사양 9개 섹션 구성

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: 내 PC 사양 화면 — 뷰모델, 뷰, 텍스트 복사·TXT·PNG 저장

**Files:**
- Create: `src/PcOptimizer.App/Services/PcSpecTextFormatter.cs`
- Create: `src/PcOptimizer.App/Services/IClipboard.cs`, `WpfClipboard.cs`
- Create: `src/PcOptimizer.App/ViewModels/PcSpecViewModel.cs`
- Create: `src/PcOptimizer.App/Views/PcSpecView.xaml`, `PcSpecView.xaml.cs`
- Modify: `src/PcOptimizer.App/Views/MainWindow.xaml`(좌측 내비게이션에 "내 PC 사양" 진입, 본문 전환), `MainViewModel.cs`(`ShowSpec` 상태·명령), `App.xaml.cs`, `Strings.resx`
- Test: `tests/PcOptimizer.Tests/Unit/App/PcSpecTests.cs`(추가), `MainWindowLayoutTests.cs`(추가)

**Interfaces:**
- Produces: `PcSpecTextFormatter.Format(PcSpecSnapshot snapshot, bool includeIdentity, string? machineName, string? userName)` → 텍스트(헤더 "PC 사양 · 2026-09-27 · 익명화됨/식별 정보 포함", 섹션별 "라벨: 값" 줄, 확인 불가는 "확인 불가"). `IClipboard { void SetText(string text); }`. `PcSpecViewModel(PcSpecService service, PcSpecTextFormatter formatter, IClipboard clipboard, IExportPathPicker picker, Func<FrameworkElement?> captureTargetProvider, IUiDispatcher dispatcher, IAppLogger logger)` with `IsLoading`, `Sections`(ObservableCollection<PcSpecSectionViewModel>), `IncludeIdentity`(기본 false), `CapturedText`, `StatusMessage`, 명령 `RefreshCommand`, `CopyTextCommand`, `SaveTextCommand`, `SaveImageCommand`. `PcSpecSectionViewModel(string Title, IReadOnlyList<PcSpecItemViewModel> Items, bool IsUnavailable)`, `PcSpecItemViewModel(string Label, string ValueText)`(null ⇒ `Strings.Spec_ValueUnknown`).

- [ ] **Step 1: 실패하는 테스트 작성** — `PcSpecTests.cs`에 추가:

```csharp
    /// <summary>텍스트 형식은 익명화 기본이며 PC 이름·사용자명은 토글이 켜졌을 때만 헤더에 들어간다.</summary>
    [Fact]
    public void TextFormatterAnonymizesByDefault()
    {
        var snapshot = new PcSpecSnapshot(new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
            [new PcSpecSection("CPU", [new PcSpecItem("모델", "Ryzen 7"), new PcSpecItem("클럭", null)])], []);
        var formatter = new PcSpecTextFormatter();

        var anonymous = formatter.Format(snapshot, includeIdentity: false, machineName: "MY-PC", userName: "kim");
        Assert.Contains(Strings.Spec_Anonymized, anonymous);
        Assert.DoesNotContain("MY-PC", anonymous);
        Assert.DoesNotContain("kim", anonymous);
        Assert.Contains("모델: Ryzen 7", anonymous);
        Assert.Contains("클럭: " + Strings.Spec_ValueUnknown, anonymous);

        var identified = formatter.Format(snapshot, includeIdentity: true, machineName: "MY-PC", userName: "kim");
        Assert.Contains("MY-PC", identified);
        Assert.Contains(Strings.Spec_Identified, identified);
    }

    /// <summary>뷰모델은 새로 고침 시 섹션을 채우고, 복사·저장은 현재 토글의 텍스트를 쓴다.</summary>
    [Fact]
    public async Task ViewModelRefreshesAndCopies()
    {
        var service = new PcSpecService([new FixtureSystemDetailsProbe()], new FixedClock(), NullAppLogger.Instance, () => TestContexts.Normal());
        var clipboard = new RecordingClipboard();
        var vm = new PcSpecViewModel(service, new PcSpecTextFormatter(), clipboard, new FixedExportPathPicker(null), () => null,
            new ImmediateUiDispatcher(), NullAppLogger.Instance, machineName: "MY-PC", userName: "kim");

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(9, vm.Sections.Count);
        Assert.False(vm.IsLoading);
        await vm.CopyTextCommand.ExecuteAsync(null);
        Assert.DoesNotContain("MY-PC", clipboard.LastText);
        vm.IncludeIdentity = true;
        await vm.CopyTextCommand.ExecuteAsync(null);
        Assert.Contains("MY-PC", clipboard.LastText);
    }
```

`FixtureSystemDetailsProbe`(Task 6 계약으로 OS·CPU 측정을 내는 가짜 IProbe), `RecordingClipboard`(마지막 텍스트 보관), `FixedClock`, `TestContexts.Normal()`은 `AppTestDoubles.cs`에 추가한다(기존에 같은 역할의 페이크가 있으면 그것을 쓴다).

`MainWindowLayoutTests.cs`에 추가:

```csharp
    /// <summary>사양 화면이 720px 폭에서 가로로 넘치지 않고 익명화 표기가 렌더된다.</summary>
    [Fact]
    public void SpecViewRendersWithinWidth()
    {
        RunOnSta(() =>
        {
            var view = new PcSpecView { DataContext = CreateSpecViewModelWithSections() };
            view.Measure(new Size(720, 900));
            view.Arrange(new Rect(0, 0, 720, 900));
            view.UpdateLayout();
            Assert.True(view.DesiredSize.Width <= 720);
            Assert.Contains(FindTextBlocks(view), t => t.Text == Strings.Spec_Anonymized);
            // fastfetch 스타일: 섹션 제목 뒤에 항목 줄이 한 열로 이어지고 2열 Grid가 없다
            Assert.DoesNotContain(FindVisualChildren<Grid>(view), g => g.ColumnDefinitions.Count >= 2 && g.RowDefinitions.Count >= 2);
        });
    }
```

- [ ] **Step 2: 실패 확인** — 컴파일 오류.

- [ ] **Step 3: 구현**

`PcSpecTextFormatter.cs`:

```csharp
/**
 * @file    : PcSpecTextFormatter.cs
 * @author  : rudals252
 * @brief   : 내 PC 사양 스냅샷을 공유용 텍스트로 만든다. 기본 익명화이며 식별 정보 포함 토글이 켜졌을 때만 PC 이름·사용자명을 헤더에 넣는다
 */

// 기본 패키지
using System.Globalization;
using System.Text;

// 사용자 패키지
using PcOptimizer.App.Models;
using PcOptimizer.App.Resources;

namespace PcOptimizer.App.Services;

/// <summary>사양 스냅샷의 텍스트 형식기입니다.</summary>
public sealed class PcSpecTextFormatter
{
    private const string DATE_FORMAT = "yyyy-MM-dd HH:mm";
    private const string ITEM_FORMAT = "{0}: {1}";

    /// <summary>텍스트를 만듭니다.</summary>
    /// <param name="snapshot">사양 스냅샷.</param>
    /// <param name="includeIdentity">PC 이름·사용자명을 포함할지(기본 false).</param>
    /// <param name="machineName">PC 이름(포함 시).</param>
    /// <param name="userName">사용자명(포함 시).</param>
    public string Format(PcSpecSnapshot snapshot, bool includeIdentity, string? machineName, string? userName)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var builder = new StringBuilder();
        var marker = includeIdentity ? Strings.Spec_Identified : Strings.Spec_Anonymized;
        builder.AppendLine(string.Format(CultureInfo.CurrentCulture, Strings.Spec_TextHeader, snapshot.CapturedAtUtc.ToLocalTime().ToString(DATE_FORMAT, CultureInfo.CurrentCulture), marker));
        if (includeIdentity)
        {
            builder.AppendLine(string.Format(CultureInfo.CurrentCulture, ITEM_FORMAT, Strings.Spec_MachineName, machineName ?? Strings.Spec_ValueUnknown));
            builder.AppendLine(string.Format(CultureInfo.CurrentCulture, ITEM_FORMAT, Strings.Spec_UserName, userName ?? Strings.Spec_ValueUnknown));
        }

        foreach (var section in snapshot.Sections)
        {
            builder.AppendLine();
            builder.AppendLine($"[{section.Title}]");
            foreach (var item in section.Items)
            {
                builder.AppendLine(string.Format(CultureInfo.CurrentCulture, ITEM_FORMAT, item.Label, item.Value ?? Strings.Spec_ValueUnknown));
            }
        }

        return builder.ToString();
    }
}
```

`IClipboard.cs`/`WpfClipboard.cs`(`System.Windows.Clipboard.SetText`). `PcSpecViewModel.cs`: `RefreshAsync`가 `IsLoading=true` → `service.CaptureAsync` → 섹션 VM 구성 → `IsLoading=false`; `CopyText`는 `formatter.Format(_snapshot, IncludeIdentity, _machineName, _userName)`를 클립보드에; `SaveText`는 `picker.PickSavePath("pc-spec-yyyyMMdd.txt")` 후 UTF-8(BOM 없음) 저장; `SaveImage`는 `captureTargetProvider()`가 준 `FrameworkElement`를 `RenderTargetBitmap`(96 DPI, 요소 실제 크기)으로 렌더하고 `PngBitmapEncoder`로 저장 — 렌더 대상 요소 하단에 익명화 표기 `TextBlock`이 포함되도록 뷰에서 그 요소를 `x:Name="CaptureRoot"`로 감싼다. 저장 실패는 형식 이름만 로그·`StatusMessage`. `machineName`/`userName`은 생성자 인자(App에서 `Environment.MachineName`, `Environment.UserName` 전달; 테스트는 고정 문자열).

`PcSpecView.xaml`(**사용자 확정 2026-09-27: fastfetch처럼 한 열, 칸 나누지 않음**): 상단 버튼 줄([새로 고침] [텍스트 복사] [TXT 저장] [이미지 저장] [식별 정보 포함 토글]) + `StatusMessage` + 하나의 세로 `ItemsControl`. 섹션 제목은 굵은 한 줄(`[운영체제]`처럼), 그 아래 항목은 `라벨: 값` 한 줄씩(라벨은 회색·고정 폭 `MinWidth=120`, 값은 본문색), 확인 불가 항목은 값 자리에 `Spec_ValueUnknown`. 확인 불가 섹션은 제목 아래 "확인 불가 — dxdiag 또는 HWiNFO64로 확인" 한 줄. 타일·2열 `Grid` 없음. 화면 텍스트와 `PcSpecTextFormatter.Format` 출력이 같은 줄 구성이어야 한다(테스트: 화면의 `TextBlock` 줄 순서 == 텍스트 출력의 줄 순서). 하단 `TextBlock`(익명화/식별 포함 표기, `Spec_MoreDetails` 안내). 모든 `TextBlock`은 `TextWrapping="Wrap"`, 값이 길면 라벨 아래로 줄바꿈.

`MainWindow.xaml`: 상단 바에 "내 PC 사양" 토글 버튼(`ShowSpec` 명령), 본문 `ScrollViewer` 안에 `<views:PcSpecView DataContext="{Binding Spec}" Visibility="{Binding IsSpecVisible, ...}" />`를 추가하고 기존 결과 영역은 `IsSpecVisible`이 false일 때만 표시. `MainViewModel`에 `PcSpecViewModel Spec`, `bool IsSpecVisible`, `ShowSpecCommand`/`ShowResultsCommand`를 추가하고 `ShowSpec` 최초 진입 시 `Spec.RefreshCommand`를 실행한다.

- [ ] **Step 4: 통과 확인** — `--filter "FullyQualifiedName~PcSpecTests|FullyQualifiedName~MainWindowLayoutTests"` PASS. 실제 창 확인: `dotnet run`으로 앱을 띄워 "내 PC 사양"을 열고 이미지 저장을 한 번 실행해 PNG가 만들어지는지 확인(파일은 임시 폴더에, 커밋하지 않음).

- [ ] **Step 5: 커밋**

```bash
git add src/PcOptimizer.App tests/PcOptimizer.Tests/Unit/App
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 내 PC 사양 화면 — 섹션 표시, 텍스트 복사·TXT·PNG 저장, 기본 익명화

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: 항상 관리자 권한 — 매니페스트 전환, 대화형 사용자 판정, 승격 재실행 제거

**Files:**
- Modify: `src/PcOptimizer.App/app.manifest`(`requireAdministrator`)
- Create: `src/PcOptimizer.Probes/Platform/InteractiveSessionUser.cs`
- Create: `src/PcOptimizer.App/Services/UserScopeResolver.cs`, `UserScopeMode.cs`
- Delete: `src/PcOptimizer.App/Services/ElevationRelauncher.cs`, `ElevatedRescanArguments.cs`, `ElevationRelaunchResult.cs`, `ScanLaunchMode.cs`, `ScanLaunchModeResolver.cs`, `ViewModels/MainViewModel.Elevation.cs`; `tests/.../ElevationRelauncherTests.cs`, `ElevatedRescanArgumentsTests.cs`
- Modify: `App.xaml.cs`, `MainViewModel.cs`, `MainWindow.xaml`(승격 버튼·배너 제거, 표준 계정 안내 배너), `Strings.resx`, `MainViewModelTests.cs`, `MainWindowLayoutTests.cs`, `AppTestDoubles.cs`
- Test: `tests/PcOptimizer.Tests/Unit/App/UserScopeResolverTests.cs`

**Interfaces:**
- Produces: `public enum UserScopeMode { Full, SystemOnly }`; `UserScopeResolver.Resolve(string? tokenUserSid, string? interactiveUserSid)` → 둘 다 있고 같으면 `Full`, 둘 다 있고 다르면 `SystemOnly`, 어느 하나라도 모르면 `SystemOnly`(보수적). `InteractiveSessionUser.TryGetSid(out string? sid)`: `WTSQuerySessionInformationW(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSUserName/WTSDomainName)`로 대화형 사용자 계정명을 얻고 `new NTAccount(domain, user).Translate(typeof(SecurityIdentifier))`로 SID 문자열 변환. 실패 시 false. `IElevationState`는 유지(`IsElevated`, `CurrentUserSid`). `MainViewModel` 생성자에서 `ElevationRelauncher`·`ScanLaunchMode` 매개변수 제거, `UserScopeMode userScope` 추가; `bool IsSystemOnly`, `string? ScopeBannerText`(SystemOnly면 `Strings.Banner_SystemOnly`="다른 관리자 계정으로 실행 중이라 이 계정의 항목(시작 프로그램·임시 파일·앱 캐시)은 검사하지 않습니다. 원래 계정으로 로그인해 실행하세요.").

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
/**
 * @file    : UserScopeResolverTests.cs
 * @author  : rudals252
 * @brief   : 프로세스 토큰 사용자와 대화형 로그온 사용자 SID 비교로 검사 범위(전체/시스템만)를 정하는 판정을 검증
 */

// 사용자 패키지
using PcOptimizer.App.Services;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>사용자 범위 판정을 검증합니다.</summary>
public sealed class UserScopeResolverTests
{
    /// <summary>같은 SID면 전체, 다르거나 모르면 시스템만.</summary>
    [Theory]
    [InlineData("S-1-5-21-1-2-3-1001", "S-1-5-21-1-2-3-1001", UserScopeMode.Full)]
    [InlineData("S-1-5-21-1-2-3-1001", "s-1-5-21-1-2-3-1001", UserScopeMode.Full)]
    [InlineData("S-1-5-21-1-2-3-500", "S-1-5-21-1-2-3-1001", UserScopeMode.SystemOnly)]
    [InlineData(null, "S-1-5-21-1-2-3-1001", UserScopeMode.SystemOnly)]
    [InlineData("S-1-5-21-1-2-3-1001", null, UserScopeMode.SystemOnly)]
    public void ResolvesScope(string? tokenSid, string? interactiveSid, UserScopeMode expected)
    {
        Assert.Equal(expected, UserScopeResolver.Resolve(tokenSid, interactiveSid));
    }
}
```

`MainViewModelTests.cs`: 기존 승격 관련 테스트(UAC 취소 보존, 버튼 활성)를 삭제하고 추가:

```csharp
    /// <summary>시스템 범위 모드에서는 배너가 보이고 검사 서비스가 시스템 프로브만 실행한다.</summary>
    [Fact]
    public async Task SystemOnlyModeShowsBannerAndLimitsScope()
    {
        var vm = CreateViewModel(userScope: UserScopeMode.SystemOnly, limitToSystemScope: true);
        Assert.True(vm.IsSystemOnly);
        Assert.Equal(Strings.Banner_SystemOnly, vm.ScopeBannerText);
        await vm.StartScanCommand.ExecuteAsync(null);
        Assert.Contains(vm.LastResult!.ProbeSummaries, s => s.Status == ProbeStatus.Skipped);
    }
```

(`CreateViewModel`은 `ScanService`를 `limitToSystemScope`로 만들고 사용자 범위 가짜 프로브 하나를 포함해야 한다.)

- [ ] **Step 2: 실패 확인** — 컴파일 오류.

- [ ] **Step 3: 구현**

`app.manifest`: `<requestedExecutionLevel level="requireAdministrator" uiAccess="false"/>`, `@brief`를 "항상 관리자 권한(requireAdministrator)…"로 수정.

`InteractiveSessionUser.cs`(Probes/Platform, `NativeMethods` 파일 관례에 맞춰 P/Invoke는 `NativeMethods.Wts.cs`로 분리해도 된다):

```csharp
/**
 * @file    : InteractiveSessionUser.cs
 * @author  : rudals252
 * @brief   : WTS API로 현재 세션의 대화형 로그온 사용자(도메인·계정명)를 읽어 SID로 바꾼다. 표준 계정이 다른 관리자 자격 증명으로 승격한 경우를 판정하는 데 쓴다
 */

// 기본 패키지
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace PcOptimizer.Probes.Platform;

/// <summary>대화형 세션 사용자를 읽습니다.</summary>
public static class InteractiveSessionUser
{
    private const int WTS_CURRENT_SESSION = -1;
    private const int WTS_USER_NAME = 5;
    private const int WTS_DOMAIN_NAME = 7;
    private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WTSQuerySessionInformationW(IntPtr server, int sessionId, int infoClass, out IntPtr buffer, out int bytesReturned);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    /// <summary>대화형 사용자의 SID 문자열을 읽습니다. 실패하면 false.</summary>
    /// <param name="sid">SID 문자열(성공 시).</param>
    public static bool TryGetSid(out string? sid)
    {
        sid = null;
        if (!TryQuery(WTS_USER_NAME, out var user) || string.IsNullOrEmpty(user) || !TryQuery(WTS_DOMAIN_NAME, out var domain))
        {
            return false;
        }

        try
        {
            var account = string.IsNullOrEmpty(domain) ? new NTAccount(user) : new NTAccount(domain, user);
            sid = ((SecurityIdentifier)account.Translate(typeof(SecurityIdentifier))).Value;
            return true;
        }
        catch (IdentityNotMappedException) { return false; }
        catch (SystemException) { return false; }
    }

    private static bool TryQuery(int infoClass, out string? value)
    {
        value = null;
        if (!WTSQuerySessionInformationW(WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, infoClass, out var buffer, out _)) { return false; }
        try { value = Marshal.PtrToStringUni(buffer); return true; }
        finally { WTSFreeMemory(buffer); }
    }
}
```

`UserScopeMode.cs`, `UserScopeResolver.cs`(순수 정적, 위 테스트 표 그대로). `App.xaml.cs`:

```csharp
        var elevation = WindowsElevationState.Capture();
        var interactiveSid = InteractiveSessionUser.TryGetSid(out var sid) ? sid : null;
        var userScope = UserScopeResolver.Resolve(elevation.CurrentUserSid, interactiveSid);
        logger.Info(LOG_CATEGORY, $"AppStarted elevated={elevation.IsElevated} scope={userScope}");
        // ... scanService: limitToSystemScope: userScope == UserScopeMode.SystemOnly
        // MainViewModel 생성자: relauncher/launchMode 인자 제거, userScope 전달, actionAvailability(Task 5), spec(Task 8) 전달
```

`MainViewModel.Elevation.cs` 삭제 후 `MainViewModel.cs`에:

```csharp
    /// <summary>이 인스턴스의 사용자 범위.</summary>
    public UserScopeMode UserScope { get; }

    /// <summary>시스템 범위만 검사하는지(다른 관리자 계정으로 실행됨).</summary>
    public bool IsSystemOnly => UserScope == UserScopeMode.SystemOnly;

    /// <summary>범위 안내 배너(전체 범위면 null).</summary>
    public string? ScopeBannerText => IsSystemOnly ? Strings.Banner_SystemOnly : null;

    /// <summary>배너 표시 여부.</summary>
    public bool HasScopeBanner => ScopeBannerText is not null;
```

`MainWindow.xaml`: 승격 재검사 버튼과 `HasElevatedBanner` 배너를 제거하고 `HasScopeBanner` 배너로 교체. `Strings.resx`: `Button_ElevatedRescan`, `Tooltip_ElevatedRescan*`, `Elevation_*`, `Banner_Elevated*` 키 제거, `Banner_SystemOnly` 추가. `ScanServiceSmokeTests`의 `RecordingProcessStarter` 사용부와 `AppTestDoubles.FakeElevationState`는 유지(캐시 도구가 씀). 삭제된 테스트 파일 2개와 `MainWindowLayoutTests`의 "관리자 버튼 활성·배너" 검사를 제거한다.

- [ ] **Step 4: 통과 확인** — 기본 필터 전체 PASS. 빌드 후 `src/PcOptimizer.App/bin/Release/net10.0-windows/PcOptimizer.App.exe`를 관리자 셸에서 실행해 창이 뜨고 로그에 `scope=Full`이 찍히는지 확인(이 셸은 이미 관리자라 UAC 프롬프트는 뜨지 않음 — 일반 셸에서의 UAC 프롬프트는 수동 검증 항목으로 원장에 기록).

- [ ] **Step 5: 커밋**

```bash
git add -A src/PcOptimizer.App src/PcOptimizer.Probes/Platform tests/PcOptimizer.Tests
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 항상 관리자 권한(requireAdministrator) 전환, 승격 재실행 제거, 대화형 사용자 SID로 검사 범위 판정

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: 관리자 실행 시 도구 정리 조건 정리와 정리 창 문구

> 2026-09-27 독립 리뷰 후속: [공유 원장](../../reviews/REVIEW_LEDGER.md)의 REV-016~018 및 REV-010과 [재현 보고서](../../reviews/2026-09-27-sp4-task9-review.md)를 브리프 작성 전에 읽을 것. 승격 거절을 제거하기 전에 SystemOnly 사용자 조치 거절 계약과 검사·사양·실행의 실제 작업 수명 관문을 반영해야 한다. 현재 정리 실행 차단을 신규 결함이 해결됐다는 근거로 삼지 않는다.

**2026-09-27 REV-016·018 반영(컨트롤러 룰링, Codex 독립 리뷰 후속):** 아래 항목을 이 Task의 필수 범위에 추가한다. 승격 거절(`NormalUserRequired`) 제거는 (a)(b)가 먼저 통과한 뒤에만 한다.

- (a) **UI 관문(REV-016·018)**: `MainViewModel.CanOpenCacheTools`는 `CacheToolsAvailable && !IsScanning && !HasDrainingNote && !IsSystemOnly && !Spec.IsLoading`. `OnSpecPropertyChanged`에서 `IsLoading` 변경 시 `CanOpenCacheTools`/`OpenCacheToolsCommand` 알림도 갱신한다. 테스트는 `docs/reviews/repro/Sp4Task9ReviewTests.cs`의 `SystemOnlyMustNotOfferUserCacheActions`, `SpecLoadingMustBlockCacheActions`를 이름·단언 그대로 `tests/PcOptimizer.Tests/Unit/App/MainViewModelTests.cs`에 편입한다(헬퍼는 기존 AppTestDoubles 사용).
- (b) **실행기 관문(REV-016)**: Probes는 App 형식을 참조할 수 없으므로 `SystemCacheToolBackend(IAppLogger? logger = null, bool limitToSystemScope = false)` 인자를 추가하고, `CacheToolsWindow(IAppLogger?, bool limitToSystemScope)`가 `MainViewModel.IsSystemOnly`를 전달한다. `limitToSystemScope`가 true면 `LocateAsync`·`ClearAsync` 모두 관측·실행 전에 코드 상수 `USER_SCOPE_EXCLUDED = "UserScopeExcluded"`로 거절한다(npm·pip·NuGet은 전부 사용자별 캐시). 정리 창 문구 `Cleanup_UserScopeExcluded`="다른 관리자 계정으로 실행 중이라 이 계정의 캐시는 정리하지 않아요." 테스트: SystemOnly 거절(Started=false, 파일 시스템 관측 0회), Full 허용 회귀.
- (c) **보호 위치 검사 확장**: 도구 경로는 `CachePathInspector`의 기존 정규화(8.3 별칭·링크 해소, CanonicalPath)를 거친 뒤 접두 비교한다. pip는 `python.exe -m pip` 형태이므로 python.exe 경로를 검사한다. `%ProgramFiles%`·`%ProgramFiles(x86)%`·`%ProgramW6432%` 중 비어 있는 값은 건너뛴다.
- (d) **문구 정정**: 표준 계정은 항상 다른 관리자 자격 증명으로 승격되므로 "원래 계정으로 로그인"은 해결책이 아니다. `Banner_SystemOnly`와 `CoreStrings.ProbeIssue_UserScopeExcluded`의 안내 부분을 "관리자 계정으로 로그인해서 실행하면 전부 검사할 수 있어요"로 바꾼다(앞부분 "다른 관리자 계정으로 실행 중이라 이 계정의 항목은 검사하지 않았어요"는 유지). `README.md`의 "관리자 재검사" 문장과 `docs/superpowers/specs/2026-09-27-first-release-actions.md:10`의 "일반 사용자 권한만 허용… 다른 SID 재검사 인스턴스" 문장을 새 모델(항상 관리자·보호 위치 도구만·SystemOnly 거절)로 정정한다.
- (e) 원장 `docs/reviews/REVIEW_LEDGER.md`의 REV-016·018에 **대응 기록**(커밋·테스트 이름)을 쓴다. 상태는 "수정됨·재검증 대기"로 두고 "검증 완료"로 바꾸지 않는다.

**Files:**
- Modify: `src/PcOptimizer.Probes/Actions/SystemCacheToolBackend.cs`(승격 거절 → "보호 위치 도구만 실행" 규칙으로 교체), `src/PcOptimizer.App/ViewModels/CacheToolsViewModel.cs`(문구), `Strings.resx`, `ProbeStrings.resx`
- Test: `tests/PcOptimizer.Tests/Unit/App/CachePathInspectorTests.cs`, `CacheCleanupTests.cs`(수정)

**Interfaces:**
- 변경: 기존 `NormalUserRequired`(승격이면 거절) 코드를 제거하고, 도구 실행 파일 경로가 `%ProgramFiles%`·`%ProgramFiles(x86)%`·`%ProgramW6432%` 아래가 아니면 `ToolNotInProtectedLocation` 코드로 거절한다(PATH 탐색 결과도 같은 검사를 통과해야 함). 정리 창은 이 코드에 "직접 실행 안내" 문구(`Cleanup_ToolUserWritable`="이 도구는 사용자 폴더에 설치돼 있어 관리자 권한 앱이 실행하지 않습니다. 터미널에서 직접 `{0}`를 실행하세요.")를 보여준다.

- [ ] **Step 1: 실패하는 테스트 작성** — `CacheCleanupTests.cs`에 추가:

```csharp
    /// <summary>관리자 권한이어도 보호 위치 도구는 실행하고, 사용자 폴더 도구는 직접 실행 안내로 거절한다.</summary>
    [Theory]
    [InlineData(@"C:\Program Files\nodejs\node.exe", true)]
    [InlineData(@"C:\Users\kim\AppData\Roaming\nvm\v20\node.exe", false)]
    public async Task ElevatedRunsOnlyProtectedLocationTools(string toolPath, bool expectedAllowed)
    {
        var backend = CreateBackend(toolPath, isElevated: true);
        var plan = await backend.LocateAsync(CacheTool.Npm, CancellationToken.None);
        if (expectedAllowed) { Assert.True(plan.Allowed); }
        else { Assert.False(plan.Allowed); Assert.Equal("ToolNotInProtectedLocation", plan.Code); }
    }
```

(`CreateBackend`는 이 파일의 기존 가짜 환경 헬퍼를 확장해 도구 경로와 승격 여부를 주입한다. 실제 API 이름은 현재 `SystemCacheToolBackend`/`CachePathInspector` 시그니처를 읽고 맞춘다.)

- [ ] **Step 2: 실패 확인** — 두 번째 케이스가 기존 `NormalUserRequired` 코드로 실패하거나 첫 케이스가 승격 거절로 실패.

- [ ] **Step 3: 구현** — `SystemCacheToolBackend`의 승격 거절 분기를 제거하고 `IsProtectedProgramLocation(toolPath)`(세 환경 변수 경로 접두 비교, 정규화 후, 대소문자 무시) 검사를 `LocateAsync`·`ClearAsync` 양쪽에 둔다. 거절 코드 상수 `TOOL_NOT_IN_PROTECTED_LOCATION = "ToolNotInProtectedLocation"`. `CacheToolsViewModel`의 코드→문구 매핑에 추가. 기존 `NormalUserRequired` 관련 테스트는 삭제.

- [ ] **Step 4: 통과 확인** — `--filter "FullyQualifiedName~CacheCleanupTests|FullyQualifiedName~CachePathInspectorTests"` PASS, 기본 필터 전체 PASS, Smoke PASS(`CacheBoundarySmokeTests`는 임시 fixture만 사용).

- [ ] **Step 5: 커밋**

```bash
git add src tests
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 관리자 실행에서 보호 위치 도구만 정리 실행, 사용자 폴더 도구는 직접 실행 안내

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: 문서·원장·검증 마무리

**2026-09-27 이월 minor 편입(컨트롤러 룰링, Task 10·13 리뷰 후속):** Step 3 문서 갱신에 아래를 추가한다.

- `Strings.resx` `Cleanup_ToolUserWritable` 문구를 "이 도구는 사용자 폴더에 설치돼 있어 관리자 권한 앱이 실행하지 않습니다. 터미널에서 다음 명령을 직접 실행하세요: {0}"로 바꾼다(조사 결합 방지). 이 키를 확인하는 테스트가 있으면 함께 갱신한다.
- 스펙 `docs/superpowers/specs/2026-09-27-improvement-phase2-design.md` §0의 "원래 계정으로 로그인" 안내를 "관리자 계정으로 로그인해서 실행하면 전부 검사할 수 있어요"로 정정하고, 보호 위치 판정이 환경 변수가 아니라 Windows 시스템 폴더 API(`Environment.SpecialFolder.ProgramFiles`·`ProgramFilesX86` 및 `ProgramW6432`)로 읽는다는 문장을 §0에 한 줄 추가한다.
- 원장 Step 2의 새 절에 REV-016·017·018의 Task 10·13 커밋과 테스트 이름, 그리고 남은 한계(npm이 `C:\node_modules`를 루트로 잡을 가능성은 파일 생성 불가로 저위험, `NUGET_`·`NPM_CONFIG_`·`PIP_` 환경 변수는 유지하기로 룰링, pip 실제 실행 스모크 없음)를 적는다. 상태는 모두 `수정됨·재검증 대기`.
- HANDOFF의 수동 검증 목록에 "일반 셸에서 UAC 프롬프트", "표준 계정+다른 관리자 자격 증명 승격 시 SystemOnly 배너·정리 버튼 비활성", "Program Files에 Python만 있는 PC에서 정리 버튼 노출"을 남긴다.

**Files:**
- Modify: `README.md`(실행 권한·사양 화면·명령), `docs/HANDOFF.md`(SP4 완료 상태), `docs/superpowers/plans/2026-09-26-pc-optimizer-implementation-plan.md`(§5 2차 착수 기록), `docs/reviews/REVIEW_LEDGER.md`, 이 계획 파일의 체크박스

- [ ] **Step 1: 전체 검증 실행**

```bash
"C:\Program Files\dotnet\dotnet.exe" build PcOptimizer.sln --configuration Release
"C:\Program Files\dotnet\dotnet.exe" test PcOptimizer.sln --configuration Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"
"C:\Program Files\dotnet\dotnet.exe" test PcOptimizer.sln --configuration Release --no-build --filter "Category=Smoke"
"C:\Program Files\dotnet\dotnet.exe" publish src/PcOptimizer.App/PcOptimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64-sp4
```

Expected: 빌드 0/0, 기본·Smoke 전부 통과, 배포 폴더 생성. 배포 EXE를 실행해 창 생성·종료 코드 0 확인.

- [ ] **Step 2: 원장 기록** — REV-014·REV-015 대응 기록(커밋 SHA, 실행 명령, 결과, 남은 제한: 일반 셸에서의 UAC 프롬프트·표준 계정 실제 검증은 수동), 새 절 "2026-09-27 SP4 구현(구현 세션 자체 검증)"에 검증 수치를 적는다. 상태는 `수정됨·재검증 대기`.

- [ ] **Step 3: 문서 갱신** — README에 "앱은 관리자 권한으로 실행됩니다(UAC 프롬프트)", "내 PC 사양: 텍스트 복사·TXT·PNG 저장, 기본 익명화" 추가. HANDOFF 현재 상태를 SP4 완료·다음 SP1로 갱신. 계획 파일 체크박스 갱신.

- [ ] **Step 4: 커밋**

```bash
git add README.md docs
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "docs: SP4 완료 — README·HANDOFF·원장 갱신

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 12: 포터블 배포 패키징 — 단일 파일 exe, 아이콘, zip 구조

**Files:**
- Modify: `src/PcOptimizer.App/PcOptimizer.App.csproj`(AssemblyName·Version·ApplicationIcon·단일 파일 publish 속성)
- Create: `src/PcOptimizer.App/Assets/app.ico`(생성 산출물, 커밋), `tools/make-icon.ps1`, `tools/package.ps1`, `tools/README-in-zip.txt`, `THIRD-PARTY-NOTICES.md`
- Modify: `.gitignore`(`dist/` 추가), `README.md`(배포·실행 방법)
- Test: `tests/PcOptimizer.Tests/Unit/App/PackagingTests.cs`(csproj 속성 검증), 수동 검증 절차

**Interfaces:**
- Produces: `dist/PcOptimizer-v<Version>-win-x64.zip`. 압축 해제 시 최상위 항목 ≤ 6개: `PcOptimizer.exe`(단일 파일, self-contained), `실행방법.txt`, `LICENSES/`(winapp2 CC-BY-SA 고지, 서드파티 고지), `rules/`(출처 메타데이터 `sources.json`만, 실행 시 읽지 않음). 사용자는 `PcOptimizer.exe`만 실행한다.

- [ ] **Step 1: 실패하는 테스트 작성** — `tests/PcOptimizer.Tests/Unit/App/PackagingTests.cs`: App csproj를 XML로 읽어 속성을 단언한다.

```csharp
/**
 * @file    : PackagingTests.cs
 * @author  : rudals252
 * @brief   : 포터블 배포에 필요한 App 프로젝트 속성(단일 파일·self-contained·아이콘·어셈블리 이름·버전)이 설정돼 있는지 검증
 */

// 기본 패키지
using System.Xml.Linq;

namespace PcOptimizer.Tests.Unit.App;

/// <summary>배포 속성을 검증합니다.</summary>
public sealed class PackagingTests
{
    private static readonly string CSPROJ = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "PcOptimizer.App", "PcOptimizer.App.csproj"));

    /// <summary>단일 파일 self-contained 배포·아이콘·이름·버전 속성이 있다.</summary>
    [Theory]
    [InlineData("AssemblyName", "PcOptimizer")]
    [InlineData("ApplicationIcon", @"Assets\app.ico")]
    [InlineData("PublishSingleFile", "true")]
    [InlineData("SelfContained", "true")]
    [InlineData("RuntimeIdentifier", "win-x64")]
    [InlineData("IncludeNativeLibrariesForSelfExtract", "true")]
    [InlineData("EnableCompressionInSingleFile", "true")]
    [InlineData("SatelliteResourceLanguages", "ko")]
    [InlineData("DebugType", "none")]
    public void PublishPropertiesArePresent(string name, string expected)
    {
        var doc = XDocument.Load(CSPROJ);
        var value = doc.Descendants(name).Select(e => e.Value.Trim()).FirstOrDefault();
        Assert.Equal(expected, value);
    }

    /// <summary>버전이 유의적 버전 형식이다.</summary>
    [Fact]
    public void VersionIsSemantic()
    {
        var version = XDocument.Load(CSPROJ).Descendants("Version").Select(e => e.Value.Trim()).FirstOrDefault();
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }
}
```

- [ ] **Step 2: 실패 확인** — `--filter "FullyQualifiedName~PackagingTests"` → 속성 없음으로 FAIL.

- [ ] **Step 3: 아이콘 생성 스크립트** — `tools/make-icon.ps1`(자체 제작, 외부 자산 없음): `System.Drawing`으로 16·24·32·48·64·128·256px 비트맵을 그려 ICO로 저장한다. 도형은 원본 디자인(둥근 사각형 배경 `#2563EB` + 흰색 체크 표시 + 작은 렌치 실루엣). 각 크기의 PNG 프레임을 ICO 컨테이너(헤더 6바이트 + 항목 16바이트씩 + PNG 데이터, 256px는 폭·높이 0으로 기록)로 묶는 코드를 스크립트에 둔다. 실행: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1 -Output src/PcOptimizer.App/Assets/app.ico`. 산출물 `app.ico`는 커밋한다(빌드 입력). 디자이너 아이콘으로 바꿀 때는 이 파일만 교체한다.

- [ ] **Step 4: csproj 속성** — `PropertyGroup`에 추가:

```xml
    <AssemblyName>PcOptimizer</AssemblyName>
    <Version>0.2.0</Version>
    <ApplicationIcon>Assets\app.ico</ApplicationIcon>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
    <SatelliteResourceLanguages>ko</SatelliteResourceLanguages>
    <DebugType>none</DebugType>
    <PublishReadyToRun>false</PublishReadyToRun>
```

WPF는 trimming을 지원하지 않으므로 `PublishTrimmed`는 두지 않는다. `RuntimeIdentifier`를 고정하면 일반 `dotnet build`도 win-x64로만 빌드된다(허용; 테스트 프로젝트는 영향 없음 — 영향이 있으면 `Directory.Build.props`가 아니라 App csproj에만 둔다). `ApplicationManifest`는 그대로(Task 9의 requireAdministrator). 기존 `rules/*.ini;*.json` publish 복사는 `sources.json`과 `LICENSE-winapp2.md`만 남긴다.

- [ ] **Step 5: 패키징 스크립트** — `tools/package.ps1`:

```powershell
<#
 @file    : package.ps1
 @author  : rudals252
 @brief   : 단일 파일 publish 후 포터블 zip을 dist/에 만든다(최상위 항목 최소화, 실행 파일 하나)
#>
param([string]$Configuration = "Release")
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root "src\PcOptimizer.App\PcOptimizer.App.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$publishDir = Join-Path $root "dist\publish"
$stage = Join-Path $root "dist\PcOptimizer-v$version-win-x64"
foreach ($dir in @($publishDir, $stage)) { if (Test-Path $dir) { Remove-Item $dir -Recurse -Force } }
& "$env:ProgramFiles\dotnet\dotnet.exe" publish $csproj -c $Configuration -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "publish failed: $LASTEXITCODE" }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item (Join-Path $publishDir "PcOptimizer.exe") $stage
Copy-Item (Join-Path $PSScriptRoot "README-in-zip.txt") (Join-Path $stage "실행방법.txt")
New-Item -ItemType Directory -Path (Join-Path $stage "LICENSES") | Out-Null
Copy-Item (Join-Path $root "rules\LICENSE-winapp2.md") (Join-Path $stage "LICENSES\winapp2-CC-BY-SA-4.0.md")
Copy-Item (Join-Path $root "THIRD-PARTY-NOTICES.md") (Join-Path $stage "LICENSES\THIRD-PARTY-NOTICES.md")
New-Item -ItemType Directory -Path (Join-Path $stage "rules") | Out-Null
Copy-Item (Join-Path $root "rules\sources.json") (Join-Path $stage "rules\sources.json")
$zip = "$stage.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip
$top = (Get-ChildItem $stage).Count
if ($top -gt 6) { throw "zip top-level entries too many: $top" }
Write-Host "created $zip"
```

`tools/README-in-zip.txt`(한국어, 10줄 이내): 압축을 푼 폴더에서 `PcOptimizer.exe`를 더블클릭 → 관리자 권한 확인창에서 "예" → 설치 없음, 삭제하려면 폴더 삭제 → 설정·로그 위치 `%LocalAppData%\PcOptimizer` → 문의 시 "내 PC 사양 → 이미지 저장" 파일을 첨부. `THIRD-PARTY-NOTICES.md`를 저장소 루트에 만든다(WPF-UI MIT, CommunityToolkit.Mvvm MIT, .NET 런타임 MIT, Fluent System Icons MIT(아이콘에 쓴 경우), Pretendard OFL(쓴 경우), winapp2 CC-BY-SA 4.0). `.gitignore`에 `dist/` 추가.

- [ ] **Step 6: 검증** — `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package.ps1` → `dist/PcOptimizer-v0.2.0-win-x64.zip` 생성, 최상위 항목 ≤ 6, `PcOptimizer.exe` 크기 기록. zip을 `%TEMP%`에 풀어 exe 실행 → 창 생성(이 셸은 관리자라 UAC 생략) → 정상 종료 코드 0. 탐색기에서 아이콘이 보이는지 확인(수동, 스크린샷은 커밋하지 않음). `PackagingTests` PASS, 기본 필터 PASS.

- [ ] **Step 7: 문서·커밋** — README에 "배포: `tools/package.ps1` → GitHub Release에 zip 첨부. 사용자는 zip을 풀고 `PcOptimizer.exe` 실행" 절과 아이콘 교체 방법을 적는다.

```bash
git add src/PcOptimizer.App/PcOptimizer.App.csproj src/PcOptimizer.App/Assets tools .gitignore README.md THIRD-PARTY-NOTICES.md tests/PcOptimizer.Tests/Unit/App/PackagingTests.cs
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 포터블 배포 — 단일 파일 self-contained exe, 자체 제작 아이콘, zip 패키징 스크립트

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## 자체 점검

- **스펙 커버리지**: 배포(스펙 §7A: GitHub zip 포터블, 파일 수 최소, 실행 파일 명확, 아이콘)는 Task 12. §0 항상 관리자(Task 9), 사용자 쓰기 가능 도구 미실행(Task 10), 표준 계정 시스템 범위(Task 9); §3 카드 형식·안전 배지(Task 1~4), 요약 타일·도구 노출 조건(Task 5), 승격 버튼·배너 제거(Task 9); §3.2 사양 섹션 9항목·확인 불가·캡처·공유·익명화(Task 6~8); §7 1단계 순서 준수. 고급 탭·되돌리기 목록·확인/결과 창 일반화는 SP1·SP2 계획으로 미룸(스펙 §3에 명시된 항목이나 이 단계 범위 밖).
- **자리표시자**: 코드 블록에 실제 내용. Task 7 빌더 9개는 라벨·형식 규칙을 주석으로 명시했고 구현자가 그대로 옮긴다.
- **형식 일관성**: `Explanation(What, Effect, Caution)`, `SafetyLevel {Safe, Caution, Irreversible}`, `IActionAvailability.CanExecuteInApp(Finding)`, `UserScopeMode {Full, SystemOnly}`, `PcSpecService.BuildSnapshot(IReadOnlyDictionary<string, ProbeResult>, DateTimeOffset)`, `PcSpecTextFormatter.Format(snapshot, includeIdentity, machineName, userName)` — 전 작업에서 동일하게 사용.

### Task 13: 실행 수명 공유와 종료 견고성 (REV-017·REV-010, 실행 순서 10 → 13 → 11 → 12)

> 2026-09-27 컨트롤러 룰링: Codex 독립 리뷰([보고서](../../reviews/2026-09-27-sp4-task9-review.md), [재현 소스](../../reviews/repro/Sp4Task9ReviewTests.cs))의 REV-017·REV-010 잔여를 SP4 안에서 해결한다. 포터블 배포(Task 12) 전에 끝낸다.

**Files:**
- Modify: `src/PcOptimizer.App/Services/PcSpecService.cs`(프로브별 진행 중 Task 추적·재진입 차단·종료 대기 노출), `src/PcOptimizer.App/ViewModels/PcSpecViewModel.cs`(`IsDraining`), `src/PcOptimizer.App/ViewModels/MainViewModel.cs`(`CanStartScan`·`CanOpenCacheTools`·사양 새로 고침 관문에 `Spec.IsDraining`과 `HasDrainingNote` 양방향 반영), `src/PcOptimizer.App/Resources/Strings.resx`(`Spec_DrainingNote`="이전 사양 읽기가 끝나기를 기다리는 중이에요."), `src/PcOptimizer.Probes/Actions/CacheProcessGuard.cs`(AggregateException 처리)
- Test: `tests/PcOptimizer.Tests/Unit/App/PcSpecTests.cs`, `MainViewModelTests.cs`, `tests/PcOptimizer.Tests/Unit/Probes/CacheProcessGuardTests.cs`(없으면 생성)

**Interfaces:**
- Produces: `PcSpecService.HasLiveProbes`(bool, 타임아웃 뒤에도 끝나지 않은 프로브 Task가 있으면 true), `PcSpecService.WaitForDrainAsync(CancellationToken)`(살아 있는 Task가 모두 끝날 때까지 대기), `PcSpecViewModel.IsDraining`(bool, 알림 속성), `MainViewModel.CanStartScan`·`CanOpenCacheTools`는 `Spec.IsDraining`이면 false, `PcSpecViewModel.Refresh`는 `IsDraining` 또는 검사 진행/종료 중이면 실행하지 않음.
- 계약(REV-017): 같은 `IProbe` 인스턴스는 일반 검사와 사양 수집이 공유한다. 사양 수집에서 타임아웃된 프로브 Task는 서비스가 `probeId → Task` 사전에 보존하고, 그 Task가 끝나기 전에는 같은 프로브를 다시 호출하지 않는다(건너뛰고 `SpecProbeStillRunning probe={id}` 경고 로그). 반대 방향(검사 종료 중 → 사양)은 `MainViewModel`이 `HasDrainingNote`일 때 `Spec.SetBusy(true)`를 유지해 새로 고침을 막는다.
- 계약(REV-010): `CacheProcessGuard.StopAsync`의 `kill()`이 `AggregateException`을 던지면 `Flatten()`한 내부 예외가 전부 `InvalidOperationException`/`Win32Exception`일 때 `KillFailed`(형식 이름만 로그)로 처리하고 종료 대기(`wait()`)를 계속한다. 그 밖의 내부 예외는 기존대로 전파한다. 바깥 catch도 같은 규칙으로 `ProcessStillRunning`을 기록하고 false를 반환한다. 시작 이력·핸들 소유권·익명 로그는 그대로 보존한다.

- [ ] **Step 1: 실패하는 테스트 작성** — 재현 소스의 `TimedOutSpecMustNotReenterLiveSharedProbe`(PcSpecTests로), `AggregateKillFailureMustNotEscapeGuard`(CacheProcessGuardTests로)를 이름·단언 그대로 편입하고, `MainViewModelTests`에 `사양_종료_대기_중에는_검사와_정리를_시작하지_않는다`(Spec.IsDraining=true ⇒ StartScan.CanExecute=false, CanOpenCacheTools=false), `검사_종료_대기_중에는_사양_새로_고침을_하지_않는다`(HasDrainingNote=true ⇒ Spec.RefreshCommand.CanExecute=false)를 추가한다.
- [ ] **Step 2: 실패 확인** — 두 편입 테스트가 원장 기록대로 실패(Expected 1/Actual 2, AggregateException 전파)하는지 확인.
- [ ] **Step 3: 구현** — 위 인터페이스 계약대로. `PcSpecService`는 `ConcurrentDictionary<string, Task>`로 살아 있는 Task를 보관하고 완료 시 제거한다. `CaptureAsync` 반환 후에도 `HasLiveProbes`가 true면 `PcSpecViewModel.IsDraining`을 true로 두고 `WaitForDrainAsync` 완료 시 false로 되돌린다(UI 스레드 디스패처 사용). 매직 값 금지(타임아웃·대기는 기존 상수 재사용).
- [ ] **Step 4: 통과 확인** — `--filter "FullyQualifiedName~PcSpecTests|FullyQualifiedName~MainViewModelTests|FullyQualifiedName~CacheProcessGuardTests"` PASS, 기본 필터 전체 PASS, Smoke PASS.
- [ ] **Step 5: 원장 대응 기록** — `docs/reviews/REVIEW_LEDGER.md` REV-017·REV-010에 커밋·테스트 이름·남은 한계를 쓴다. 상태는 "수정됨·재검증 대기".
- [ ] **Step 6: 커밋**

```bash
git add src tests docs/reviews/REVIEW_LEDGER.md
git -c user.name=rudals252 -c user.email=jwr300028@gmail.com commit -m "SP4: 사양 수집·검사 실행 수명 공유와 종료 예외 처리(REV-017·REV-010)

Co-Authored-By: <실제 모델> <noreply@anthropic.com>"
```

---
