# SP4 Task 9 시점 독립 리뷰 — 2026-09-27

## 대상과 판정

- 요청 경로 `C:\Users\Administrator\Desktop\windows\docs\HANDOFF.md`는 없었다. `windows`에는 commands/hooks/persona/settings가 있고 PC Optimizer 저장소가 아니다. 대화와 Task 9 커밋이 일치하는 **pc_optimizer**의 HANDOFF/스펙/계획/공유 원장/Task 9 보고서를 대상으로 검토했다.
- 검토 기준 `66cd7cb`, Task 9 구현 `b9687af..c9ba0d7`. Task 1~8의 연결 코드와 Task 10 설계 경계도 확인했다. 검토 도중 `1463afe`가 HANDOFF의 Task 9 상태를 정정했다(문서만 바뀜). 이 변경은 보존했다.
- 마감 전 `512a82e`의 Task 9 수정 라운드 1도 diff로 확인했다. 옵션 문구·시작 로그·WTS 조회 예외 처리·레이아웃 단언 변경이며, 아래 네 재현 경로는 바뀌지 않았다. 테스트 수치는 `66cd7cb` 고정 복사본 기준이며 최신 HEAD 전체 재실행 수치로 표기하지 않는다.
- **Needs fixes / Task 10 실행 허용 전 관문 보완 필요.** requireAdministrator, WTS 현재 세션 사용자 SID 비교, SID 미상 시 SystemOnly, ScanService의 사용자 프로브 차단은 연결돼 있다. 하지만 조치 경로로 범위가 전달되지 않으며 사양 수집 수명과 실행 상호 배제에도 빈 곳이 있다.
- 현재 정리 실행기는 승격 상태를 거절한다. 따라서 아래 REV-016·018에서 **실제 다른 계정 캐시가 삭제됐다고 주장하지 않는다**. Task 10이 이 거절을 제거하기 전에 고쳐야 할 경계다. 관리자 정리가 현재 불가인 것은 이미 알려진 Task 9→10 중간 상태이므로 별도 신규 버그로 중복 등록하지 않았다.

## 발견 사항

### REV-016 — P1, Task 10 활성화 전 필수: SystemOnly가 조치 실행 경로에 전달되지 않음

위치: `src/PcOptimizer.App/ViewModels/MainViewModel.cs:175,226`, `Views/MainWindow.xaml.cs:43-46`, `Views/CacheToolsWindow.xaml.cs:27`, `App.xaml.cs:64-66`, Task 10 계획.

재현 조건: `UserScopeMode.SystemOnly`이고 보호 위치 도구가 있다. 배너는 시스템 범위만이라고 표시하지만 `CanOpenCacheTools`는 true다. 정리 창은 범위 인자 없이 새 `SystemCacheToolBackend`를 만든다. backend는 현재 프로세스의 프로필/SID만 알아서 대화형 사용자와 다른 관리자 계정의 프로필을 원래 대상으로 구분할 수 없다.

`SystemOnlyMustNotOfferUserCacheActions` 실패: Expected False / Actual True. **지금 재현한 것은 진입 허용과 컨텍스트 단절**이다. 실제 정리는 `NormalUserRequired`로 차단된다. 계획대로 이 차단만 제거하면 사용자별 캐시 조치가 다른 관리자 프로필을 대상으로 준비될 수 있다.

요청: Task 10 계획에 사용자 범위 전달 계약을 추가한다. UI 비활성화뿐 아니라 backend/Planner/Executor의 준비와 실행 직전에도 `SystemOnly`의 사용자별 조치를 거절한다. 사유·Started=false·재관측 없음 및 동일 사용자 허용을 테스트한다. SP1의 사용자 임시 파일/시작 프로그램 HKCU/되돌리기도 같은 계약을 사용해야 한다.

### REV-017 — P2: 사양 수집 타임아웃 후 같은 프로브가 살아 있는데 다시 실행됨

위치: `src/PcOptimizer.App/Services/PcSpecService.cs:172-197`, `ViewModels/MainViewModel.cs:403-414`.

`RunProbeAsync`는 내부 Task를 `WaitAsync`로 기다리다가 타임아웃을 정상 반환으로 바꾸지만 내부 Task나 종료 중 상태를 보존하지 않는다. CaptureAsync가 돌아오면 사양의 IsLoading도 해제되고 새 사양 읽기/일반 검사가 가능해진다. App은 두 경로에 같은 프로브 인스턴스를 공급한다. 반대 방향도 `Spec.SetBusy(scanning)`만 확인하므로 일반 검사가 Partial/Cancelled가 된 뒤 Draining 중인 프로브를 사양이 다시 호출할 수 있다.

`TimedOutSpecMustNotReenterLiveSharedProbe`: 취소를 무시하고 신호를 기다리는 프로브(DefaultTimeout 40ms)로 CaptureAsync를 두 번 실행. 첫 Task가 끝나지 않은 상태에서 호출 수가 **1→2**, Expected 1 / Actual 2. 반복 새로 고침으로 조회 작업이 누적될 수 있다.

요청: 일반 검사와 사양 수집이 공유하는 실제 실행/종료 추적을 사용한다. 대기 타임아웃과 실제 종료를 구분하고 같은 프로브가 종료될 때까지 재진입을 막는다. 양방향(사양→검사, 검사→사양) 회귀와 늦은 종료 후 정상 복구를 검증한다. 단순 IsLoading 검사만으로 해결되지 않는다.

### REV-018 — P2: 사양을 읽는 중에도 정리 창 진입이 허용됨

위치: `src/PcOptimizer.App/ViewModels/MainViewModel.cs:223-226,421-426`.

`CanStartScan`은 Spec.IsLoading을 확인하지만 `CanOpenCacheTools`는 확인하지 않는다. OnSpecPropertyChanged도 검사 명령만 갱신한다. Task 8 fix 보고서에는 정리 CanExecute 가드가 반영됐다고 되어 있지만 실제 조건에는 빠져 있다.

`SpecLoadingMustBlockCacheActions`: 사양 프로브를 멈춘 상태에서 IsLoading=true, StartScan.CanExecute=false를 확인했지만 CanOpenCacheTools=true라 실패했다. Task 10 전에는 정리 자체가 승격 거절되므로 변경 작업 겹침까지 재현한 것은 아니다.

요청: 사양 읽기/종료 중 상태를 정리 창과 실행 가능 판정에 포함하고 상태 변경 알림을 연결한다. 시작·완료 시 양방향 상태 변화 테스트를 추가한다. REV-017의 수명 추적과 함께 해결해야 한다.

### 기존 REV-010 — 잔여 경로 재현 확인

위치: `src/PcOptimizer.Probes/Actions/CacheProcessGuard.cs:41-42,51`, `CacheToolProcess.cs:78-92`.

`AggregateKillFailureMustNotEscapeGuard`에서 Kill 대역이 AggregateException(Win32Exception 포함)을 던지면 종료 대기까지 가지 않고 예외가 탈출한다. 프로세스 실행기는 이 형식을 받지 못하고 retained=false 상태로 핸들을 Dispose한다. 가드에는 폐기된 핸들 조회가 남으며 서비스에는 Started 결과가 전달되지 않는 경로다. 기존 원장의 정적 지적을 실행으로 확인했다. 이 항목은 내가 이전에 작성한 코드의 자기 재현이므로 별도 독립 승인으로 바꾸지 않는다.

요청: 트리 종료 예외의 수집 형태를 처리하면서 시작 이력, 실제 종료 관측, 핸들 소유권, 익명 로그를 보존한다. 종료 예외를 무시하거나 재실행 차단을 무조건 풀면 안 된다.

## 검증 근거

고정 커밋을 `git archive 66cd7cb`로 `artifacts/review-sp4-66cd7cb/snapshot`에 풀어 실행했다. 원본 소스/정식 테스트는 수정하지 않았다.

| 검증 | 결과 | 산출물 |
|---|---|---|
| Release 빌드 | 경고 0, 오류 0 | 고정 복사본 `dotnet build PcOptimizer.sln -c Release --no-restore -p:CopyRetryCount=0` |
| 기존 기본 테스트 | 1196/1196 통과 | `sp4-review-baseline.trx` |
| 기존 Smoke | 22/22 통과 | `sp4-review-smoke.trx` |
| 추가 재현 테스트 | 4/4 실패(수정 필요를 나타내는 기대 단언) | `sp4-review-repro.trx` |
| Online/ToolSmoke | 이번에 실행하지 않음 | 수집 코드 변경 검토가 아니며 pip 실제 실행도 없음 |

TRX 위치: `artifacts/review-sp4-66cd7cb/snapshot/tests/PcOptimizer.Tests/TestResults/`.

재현 소스: [Sp4Task9ReviewTests.cs](repro/Sp4Task9ReviewTests.cs). 이 파일을 대상 스냅샷의 `tests/PcOptimizer.Tests/Unit/App/`에 복사하고 아래 명령으로 실행한다. 수정 후 정식 회귀 테스트에 편입할 때 이름·단언을 보존하거나 변경 이유를 원장에 기록한다.

```powershell
dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter 'FullyQualifiedName~Sp4Task9ReviewTests' --logger 'trx;LogFileName=sp4-review-repro.trx'
```

처음 기본 locked restore는 저장소 잠금의 win-x64와 명령 RID가 달라 NU1004로 실패했다. App csproj를 `-r win-x64 --locked-mode`로 복원한 뒤 빌드했다. 샌드박스 NuGet.Config 읽기 제한은 권한을 사용해 해결했고, 실제 Windows Smoke도 제한 밖에서 실행했다. 원본 저장소 잠금/패키지 버전 변경 없음. Smoke의 실제 명령은 소유 임시 fixture만 사용했다.

## 추가 설계에 전달할 조건

1. ActionPlan뿐 아니라 **실행자의 현재 사용자 범위와 실제 진행 중 작업 상태**를 공통 관문으로 정한다. 실행 버튼 노출 조건과 실행기 거절 조건을 서로 다른 곳에서 따로 추정하지 않는다.
2. requireAdministrator 전환 승인 자체를 다시 묻지 않는다. 대신 SystemOnly에서 사용자별 기능을 못 쓰는 이유와 지원 범위를 정확히 설명한다. 표준 계정은 이미 원래 계정으로 로그인해 있어도 매번 다른 관리자 자격 증명을 요구받으므로 “원래 계정으로 로그인” 안내만으로는 해결되지 않는다.
3. Task 10의 Program Files 접두 조건은 실행 파일/스크립트의 경로·링크 관문과 묶어 검토하고, 사용자 쓰기 가능 여부라는 설계 목적을 실제 검증 조건으로 정한다. 이 부분은 아직 미구현 설계 검토 사항이지 재현된 새 실행 취약점 판정은 아니다.
4. 일반 사용자 셸 UAC, 표준 계정→다른 관리자 승격, WTS 조회 불가, 별도 세션의 실제 검증은 남아 있다. 기존 SID 문자열 fixture와 관리자 셸 실행만으로 이를 완료 처리하지 않는다.

범위 결정: Task 9의 기본 전환을 전부 실패로 판정한 것은 아니다. Task 9 완료 기준에 연결 경계 보완을 넣거나 Task 10의 필수 선행 조건으로 명시한 뒤 구현을 이어갈 수 있다. 이 리뷰는 제품 출시 승인이나 추가 기능 구현이 아니다.
