# SP4 최종 브랜치 리뷰 (e1c965c..888305b)

- 리뷰어: 독립 최종 리뷰(Claude, 읽기 전용). 원장(REVIEW_LEDGER.md)은 수정하지 않았다.
- 범위: 42커밋, 106파일, +6787/−1253. diff가 커서 영역별로 나눠 읽었다(보안·권한 → 사양/동시성 → UI·문구 → 패키징 → 테스트).
- Task별 리뷰에서 이미 다룬 줄 단위 지적은 반복하지 않았다.

## 요약 판정

Core 불변식(설명 3줄·안전 수준), Core의 Windows 비의존, 포함 리소스·SHA, 로그 익명성, 승격 재실행 코드 제거는 깨끗하다. 빌드는 경고·오류 0, 기본 테스트는 1257/1257 통과했다.

하지만 Task 단위 리뷰에서 보이지 않던 통합 결함이 3건 있다.

1. **항상 관리자 권한 전환(Task 9)의 부작용.** 기존 공식 링크 열기가 이제 관리자 권한 브라우저를 띄운다.
2. **사양 수집이 UI 스레드에서 동기로 실행된다(Task 7·8·13).** 실제 프로브가 모두 동기 구현이라 타임아웃과 REV-017의 "살아 있는 프로브 추적"이 실제로는 작동하지 않는다.
3. **첫 화면 문구가 오해를 준다.** 검사 전에도 "직접 해야 하는 것 0건"이 보인다.

이 3건은 병합 전에 고쳐야 한다. 나머지는 이월해도 된다.

## 병합 전 필수

### 1. [Critical] 공식 링크를 관리자 권한 브라우저로 연다

- **위치:** `src/PcOptimizer.App/Services/LinkPolicy.cs:121`
  - `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`
- **내용:** 매니페스트가 `requireAdministrator`이므로 셸 실행이 기본 브라우저(Chrome·Firefox·Whale 등 Win32 브라우저)를 **관리자 토큰으로** 시작한다. Edge는 스스로 권한을 낮추지만 다른 브라우저는 그렇지 않다.
  - 대상은 NVIDIA·AMD·Intel·OEM 드라이버 페이지다. 이 페이지에서 받은 설치 파일도 UAC 없이 관리자로 실행된다.
  - 브라우저 취약점 하나가 곧 관리자 권한 코드 실행이 된다.
  - SystemOnly(다른 관리자 자격 증명으로 승격)에서는 브라우저가 **그 관리자 계정 프로필**로 열린다.
  - `SettingsUriPolicy`(`ms-settings:`만 허용, 패키지 앱 활성화)는 해당하지 않는다.
- **왜 놓쳤나:** LinkPolicy는 SP4 이전 코드이고 Task 9 리뷰는 도구 실행 경로만 봤다.
- **수정:**
  - 검증된 `AbsoluteUri`를 비승격 셸에 넘긴다. 둘 중 하나를 쓴다.
    - `ProcessStartInfo("explorer.exe") { ArgumentList = { uri } }` — 이미 실행 중인 비승격 셸이 연다.
    - `IShellWindows` → `IShellDispatch2.ShellExecute`(데스크톱 셸 프로세스에서 실행).
  - 실패하면 URL 복사 안내로 대체한다.
  - 회귀 테스트: 링크 여는 함수가 `UseShellExecute`로 URL을 직접 실행하지 않는지 확인하고, 시작 정보를 주입해서 검증한다.

### 2. [Important] 사양 수집이 UI 스레드에서 동기로 실행되어 타임아웃·REV-017 추적이 작동하지 않는다

- **위치:**
  - `src/PcOptimizer.App/Services/PcSpecService.cs:214` — `running = probe.RunAsync(context, timeout.Token);`
  - 호출 경로: `PcSpecViewModel.cs:183`, `MainViewModel.cs:406`
- **내용:** 사양에 쓰는 8개 프로브는 모두 동기 작업 뒤 `Task.FromResult`를 돌려준다.
  - SystemDetails·SystemInfo·Memory·Display·PhysicalDisk·Volume·Power 등이 해당한다. 확인한 예: `SystemDetailsProbe.cs:120`, `DisplayProbe.cs:81`, `MemoryProbe.cs:108`.
  - `RefreshCommand`는 UI 스레드에서 시작하고, 완료된 Task에 대한 `await`는 양보하지 않는다. 그래서 **8개 프로브의 WMI 조회 전체가 UI 스레드에서 연달아 실행된다.** `Win32_PnPSignedDriver`만 해도 수 초가 걸린다.
- **결과:**
  - "내 PC 사양"을 누르거나 새로 고치면 창이 수 초 동안 멈춘다. "사양을 읽는 중이에요…" 안내는 그려지지도 않는다.
  - `WaitAsync(DefaultTimeout)`는 동기 호출이 끝난 뒤에야 적용된다. 그래서 WMI 저장소가 손상된 PC에서 조회가 멈추면 **앱 전체가 영구히 응답하지 않는다.**
  - `TrackIfLive`/`IsDraining`(REV-017)은 실제 프로브에서 한 번도 작동하지 않는다.
  - `1d24cbf`의 "전환 즉시 반환" 의도도 실제로는 성립하지 않는다.
- **테스트 공백:** `PcSpecTests`는 비동기로 멈추는 `HangingProbe`만 쓴다. Core에는 동기 차단용 `BlockingProbe`(`Unit/Engine/Fakes/FakeProbes.cs:203`)가 이미 있는데 사양 테스트에는 쓰지 않았다.
- **수정:**
  - `ProbeExecutor.cs:105`처럼 `running = Task.Run(() => probe.RunAsync(context, timeout.Token), CancellationToken.None);`로 바꾼다.
  - 회귀 테스트 2개를 추가한다.
    - `BlockingProbe`에서 `CaptureAsync`가 타임아웃 뒤 반환되고 `HasLiveProbes == true`가 되는지.
    - `RefreshCommand.Execute`가 즉시 반환되는지.
  - REV-017은 이 수정 전에는 `검증 완료`로 올리면 안 된다.

### 3. [Important] 검사 전에도 "직접 해야 하는 것 0건"이 보인다

- **위치:**
  - `src/PcOptimizer.App/Views/MainWindow.xaml:191-203` — 타일에 표시 조건이 없다.
  - `src/PcOptimizer.App/ViewModels/MainViewModel.Overview.cs:60-72`
- **내용:** `LastResult == null`이어도 `RecommendedCards`가 비어 있으면 "직접 해야 하는 것 0건"을 그린다.
  - 이전 타일은 `Overview_NotScanned`("검사 전")로 이 상태를 구분했는데, Task 5에서 그 키를 지웠다.
  - 모든 사용자가 처음 보는 화면에서 "할 일 0건"이라고 말하는 셈이다. 비전문가는 이를 "문제 없음"으로 읽을 수 있다(정직성 원칙 위반).
- **수정:**
  - 타일 묶음을 `HasCards`(또는 `LastResult is not null`)일 때만 표시한다.
  - 또는 검사 전 문구 키를 되살린다.
  - 레이아웃 테스트에 "검사 전 타일 숨김" 단언을 추가한다.

## 이월 가능

### 4. [Important] SID 조회 실패 시 같은 사용자도 SystemOnly가 되고, 배너가 사실과 다르게 말한다

- **위치:**
  - `src/PcOptimizer.Probes/Platform/InteractiveSessionUser.cs:28-35`
  - `App.xaml.cs:47-49`
  - `Strings.resx` `Banner_SystemOnly`
- **내용:** WTS 사용자·도메인 이름을 `NTAccount.Translate`로 SID로 바꾼다. 실패하면 보수적으로 SystemOnly가 된다(안전 측면에서는 맞다).
  - 실패할 수 있는 경우: Entra ID(`AzureAD\이름`) 계정, DC에 연결되지 않은 도메인 계정, 이름이 잘린 계정.
  - 이런 PC에서는 같은 관리자 본인인데도 앱 캐시·시작 프로그램·임시 파일 검사와 정리가 모두 막힌다.
  - 배너는 "다른 관리자 계정으로 실행 중이라…"라고 **사실과 다른 이유**를 표시한다.
  - Entra 환경은 실기 확인을 하지 못했다.
- **수정:**
  - 이름 변환 대신, 또는 실패 시 대체 수단으로, 셸 프로세스 토큰 SID를 읽는다. 순서: `GetShellWindow` → `GetWindowThreadProcessId` → `OpenProcess(QUERY_LIMITED)` → `OpenProcessToken(TOKEN_QUERY)`.
  - "확인 불가"와 "다른 계정"을 구분하는 배너 문구를 따로 둔다.
  - `InteractiveSessionUser.TryGetSid`가 개발 PC에서 현재 SID와 같은지 확인하는 Smoke 테스트를 추가한다.

### 5. [Important] 항상 관리자 권한 위협 모델: 같은 사용자의 일반 권한 프로세스에 대한 방어가 부분적이다(문서화·경화 권장)

보호 위치 도구 규칙 자체는 올바르다. 확인한 내용은 다음과 같다.
- Known Folder API로 보호 위치를 판정한다.
- 경로를 정규화하고, 8.3 별칭은 거절한다.
- 실행 직전에 다시 확인한다(`CachePathInspector.cs:237`).
- 작업 폴더는 System32다.
- `-I`, `DOTNET_`·`NODE_`·`PYTHON*`·`COR*` 변수를 제거한다.
- SystemOnly에서는 관측 전에 거절한다.
- 삭제 대상은 토큰 사용자 프로필 안으로 한정하고, 다른 사용자·보호 경로·링크는 거절한다.

같은 사용자의 NPM_CONFIG_/PIP_/NUGET_ 설정은 Full 범위에서 결국 자기 캐시에만 영향을 준다. 그래서 "Full 범위에서는 허용"이라는 판단은 유지된다. 다른 표준 사용자(SystemOnly)는 도구 실행·사용자 범위 조회가 모두 막혀 있어 경로가 없다.

다만 스펙 §0의 근거("사용자 폴더 npm-cli.js 바꿔치기로 관리자 획득")와 같은 위협, 즉 **같은 사용자의 중간 무결성 프로세스**에는 아래 경로가 남아 있다.

- **(a) 실행 파일 자체:** 포터블 exe는 다운로드·바탕화면 등 사용자 쓰기 가능 위치에 있다. 바꿔치기하면 다음 UAC 승인 때 관리자로 실행된다. 배포 형태에서 오는 한계다.
- **(b) 앱 자체의 .NET 환경 변수:** `HKCU\Environment`의 `DOTNET_STARTUP_HOOKS`, `CORECLR_ENABLE_PROFILING`+`CORECLR_PROFILER_PATH`가 관리자 권한 PcOptimizer.exe에 적용된다. 트리밍을 하지 않으므로 시작 훅 기본값은 켜짐이다.
- **(c) 단일 파일 추출 폴더:** `IncludeNativeLibrariesForSelfExtract=true`라서 WPF 네이티브 DLL이 `%TEMP%\.net\PcOptimizer\<hash>`로 추출된다. 이미 있는 파일은 다시 쓰지 않으므로 미리 심어 둔 DLL이 로드될 수 있다.
- **(d) dotnet 자식 프로세스:** `CacheToolProcess.cs:93-95`는 `DOTNET_`은 지우지만 같은 런타임 설정의 옛 접두사인 `COMPlus_`는 남긴다.

**권장:**
- csproj에 `<StartupHookSupport>false</StartupHookSupport>`를 넣는다.
- `COMPlus_` 접두사도 제거한다.
- 스펙 §0에 "보호 위치 규칙은 심층 방어이며, 같은 사용자 중간 무결성 코드는 포터블 exe 특성상 위협 모델 밖"이라고 명시한다.

(a)·(c)는 서명·설치형 배포(Program Files)로 가야 근본적으로 해결된다.

### 6. [Minor] "앱 안에서 바로 실행" 판정이 도구별이 아니다

- **위치:** `CacheToolActionAvailability.cs:40,50`
- **내용:** npm 카드도 "보호 위치에 npm·pip·dotnet 중 **아무거나**"로 판정한다. 예를 들어 dotnet은 Program Files에, npm은 nvm에 있으면 npm 카드가 "바로 할 수 있는 것"으로 세어지지만, 실행하면 `ToolNotInProtectedLocation`으로 거절된다.
  - 지금은 AppCacheRule이 Candidate를 만들지 않아(Task 5 보고에 기록됨) 드러나지 않는다. SP1에서 카드를 Candidate로 올리는 순간 드러난다.
  - 판정할 때마다 PATH를 파일 시스템에서 조회하는데, UI 스레드에서 속성 알림마다 반복된다.
- **수정:** appId → `CacheTool` 매핑과 `IsToolInProtectedLocation`으로 도구별 판정을 하고, 결과를 생성 시 한 번 캐시한다.

### 7. [Minor] 패키징 속성이 모든 빌드에 적용되고, lock 파일 churn이 남아 있다

- **csproj 속성:** `PcOptimizer.App.csproj`의 `RuntimeIdentifier`·`SelfContained`·`DebugType=none`에 조건이 없다.
  - Debug 빌드에도 pdb가 없어 스택 줄 번호가 사라진다.
  - 일반 build 출력이 self-contained(런타임 전체 복사)가 된다.
  - `Microsoft.NET.ILLink.Tasks 10.0.12`가 App lock에 Direct로 고정되어, SDK를 업데이트할 때마다 lock이 바뀐다.
  - **권장:** 게시 전용 속성은 `Properties/PublishProfiles/win-x64.pubxml` 또는 `Condition="'$(_IsPublishing)'=='true'"`로 옮긴다.
- **lock churn:** 이번 솔루션 빌드 뒤 `git status`는 깨끗했다(churn 없음). 하지만 Core·Probes를 단독으로 빌드하면 `net10.0/win-x64` 섹션이 빠지는 기존 문제는 그대로다.
  - **권장:** `Directory.Build.props`에 `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`를 넣어 모든 프로젝트 그래프를 같게 만든다. CI와 package.ps1에서는 `--locked-mode`로 복원한다.
- **`tools/package.ps1`:**
  - 게시 전에 테스트를 실행하거나 작업 트리가 깨끗한지 확인하지 않는다.
  - zip의 SHA-256을 출력하지 않는다.
  - `--locked-mode`를 쓰지 않는다.
- **`실행방법.txt`:** 서명하지 않은 exe라서 SmartScreen("Windows의 PC 보호" → 추가 정보 → 실행)과 UAC "알 수 없는 게시자" 창이 뜬다. 이에 대한 초보자 안내가 없다.

### 8. [Minor] 서드파티 고지가 빠진 부분이 있다

- **위치:** `THIRD-PARTY-NOTICES.md`
- **내용:**
  - self-contained 런타임에는 .NET이 포함한 네이티브 서드파티 구성 요소가 들어 있다. 런타임 팩의 `THIRD-PARTY-NOTICES.TXT` 원문을 동봉해야 한다.
  - WPF-UI의 `ThirdPartyNotices.txt`는 "따른다"고만 적고 실제로 동봉하지 않았다.
- **수정:** 두 원문을 `LICENSES/`에 복사한다(package.ps1).

### 9. [Minor] Task 5 이후 죽은 코드

- **내용:** 요약 타일이 사라져서 다음 코드에 도달할 수 없다.
  - `ShowSettingsCommand`·`ShowDriversCommand`·`ShowSettingsOnly`
  - 관련 문구 `Overview_SettingsFocusHelp`·`Overview_EmptySettings` (`MainViewModel.Overview.cs:47,54,96,106-126`)
  - 이 명령에 바인딩된 XAML이 없다.
  - `MainViewModel.IsElevated`도 뷰에서 쓰지 않는다.
- **참고:** 승격 재실행 관련 흔적(ElevationRelauncher, ScanLaunchMode, IProcessStarter, 문구·테스트)은 완전히 제거됐다.

### 10. [Minor] 앱 캐시 문구가 예전 "별도 관리자 검사" 전제를 그대로 쓴다

- **위치:** `AppCache_Detail_ConfigElevated`·`AppCache_Catalog_Detail_Elevated`
- **내용:** "관리자 권한 검사라…"는 예전 "별도 관리자 검사" 전제의 문구다. 이제는 모든 검사에 표시된다.
- **기능상 결과:** 항상 관리자 권한이 되면서 사용자 지정 캐시 경로(npm·pip·NuGet·Steam 설정)를 **항상** 무시한다. 스펙에서 이 기능 축소를 명시적으로 인정하는지 확인해야 한다.
- **수정:** 문구를 "사용자 설정 경로는 보안상 적용하지 않고 기본 위치만 확인했어요"로 바꾼다.

### 11. [Minor] 테스트 품질

- `PackagingTests`는 csproj 문자열만 단언한다(동어반복에 가깝다). `AppContext.BaseDirectory` 기준 상대 경로라서 출력 경로가 바뀌면 깨진다.
- `InteractiveSessionUser`는 실제 호출 테스트가 없다(4번 참고).
- 2번 결함은 사양 테스트에 동기 차단 프로브가 없어서 통과했다.

### 12. [Nit] 문서·문구 세부

- **오래된 문서 주석:** `ScanService.cs:26`("관리자 권한 동작은 하지 않으며"), `ScanService.cs:104`("일반 권한에서는 ElevationRequired로 건너뜀").
- **스펙과 구현 불일치:** 스펙 §7A는 `rules/`를 "출처 메타데이터만"이라고 하지만, 구현은 CC-BY-SA 원문을 동봉한다(`888305b`). 스펙을 구현에 맞춰 고친다.
- **말투 혼용:** 해요체와 합니다체가 섞여 있다.
  - `Banner_SystemOnly`는 한 문장 안에 "않습니다"와 "있어요"가 함께 있다.
  - `Cleanup_ToolUserWritable`은 합니다체, `Cleanup_UserScopeExcluded`는 해요체다.
  - 설명 3줄은 합니다체다.
  - 앱 전체를 해요체로 맞추는 것을 권장한다.
- **익명화 사양의 볼륨 이름:** 익명화 상태 사양에도 사용자가 정한 볼륨 이름(`Spec_Volume` + `FIELD_LABEL`)이 나온다. 이름이 들어갈 수 있으므로 `IsIdentifying=true`로 두는 것을 검토한다.
- **SystemOnly의 사용자명:** SystemOnly에서 "식별 정보 포함" 사용자명은 `Environment.UserName`, 즉 승격한 관리자 계정 이름이다.

## 영역별 한 줄 평가

| 영역 | 평가 |
|---|---|
| 1. 항상 관리자 권한 위협 모델 | 도구 실행 경로는 견고하다. 다만 브라우저 승격(필수 1번)과 같은 사용자 대상 경화·문서화(이월 5번)가 남아 있다. |
| 2. SystemOnly 정확성 | 사용자 범위 프로브(AppCache·StartupItems·FileScan·GameMode)가 조율기·사양 모두에서 건너뛰어진다. 정리 실행기는 관측 전에 거절하고 UI 관문도 막는다. 조회 실패 시 오판·배너 문구는 이월 4번. |
| 3. Core·리소스·SHA | 깨끗하다. Core에 Windows API 참조가 없고(`CoreAssemblyReferenceTests`), rules·포함 리소스는 이번 범위에서 바뀌지 않았으며 SHA 테스트가 통과한다. |
| 4. 개인정보 | 로그는 형식 이름·코드·프로브 ID만 남긴다. SID·계정명은 기록하지 않는다. 기본 PNG/TXT/클립보드는 PC 이름·사용자명을 빼며, 일련번호·MAC·UUID는 아예 수집하지 않는다. 볼륨 이름만 Nit. |
| 5. 동시성 | ViewModel 관문(검사↔사양↔정리, 양방향 종료 대기)은 논리적으로 맞다. 하지만 사양 수집이 동기라 실제로는 UI 정지와 무한 대기가 생긴다(필수 2번). |
| 6. 패키징 | zip 구성·항목 이름('/')·버전 추출은 적절하다. 게시 속성의 범위, lock 파일, 고지, SmartScreen 안내는 이월 7·8번. |
| 7. 승격 재실행 제거 | 깨끗하다(관련 코드·문구·테스트 모두 제거). 죽은 코드는 요약 타일 쪽만 남았다(이월 9번). |
| 8. 사용자 문구 | 설명 3줄은 60자 이내이고 내용이 정확하다. 첫 화면 0건 문제(필수 3번), 말투 혼용(Nit). |
| 9. 테스트 | 수는 충분하다. 동기 프로브와 실제 WTS 조회 경로가 빠져 있다(이월 11번). |

원장 관련 권고(원장은 수정하지 않음):
- REV-017은 필수 2번을 고칠 때까지 `검증 완료` 불가.
- REV-015는 이월 6번(도구별 판정) 제한을 함께 적을 것.
- REV-014·016·018은 diff상 해결을 확인했다.

## 검증 명령 결과

- `"C:\Program Files\dotnet\dotnet.exe" build PcOptimizer.sln --configuration Release` → 종료 0, **경고 0개, 오류 0개**
- `"C:\Program Files\dotnet\dotnet.exe" test tests/PcOptimizer.Tests --configuration Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"` → **통과 1257, 실패 0, 건너뜀 0** (전체 1257)
- 빌드 후 `git status --short`는 **변경 없음**이었다(Core·Probes `packages.lock.json` churn이 이번에는 생기지 않아 복원할 필요가 없었다).
- Smoke·Online·ToolSmoke 테스트와 앱 실행은 지시대로 하지 않았다. 따라서 다음 항목은 **실기 검증을 하지 못했다**. 모두 코드를 읽고 내린 판단이다.
  - 필수 1번(승격 브라우저)
  - 필수 2번(UI 정지)
  - 이월 4번(Entra 조회)

VERDICT: FIX_REQUIRED (3 must-fix)
