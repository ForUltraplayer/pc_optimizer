# 공유 리뷰 원장

사용자 요청에 따라 Codex의 검토 결과를 구현자·리뷰어가 파일로 확인하고 대응하기 위한 원장이다. 마지막 기록일: 2026-09-27(SP4 Task 9 시점 REV-016~018 신규 재현, REV-010 잔여 재현 확인). 사용자의 ‘이어서 작업 진행’ 지시 이후 Codex가 구현도 인계받았다. 이후 자기 수정 검증과 별도 독립 리뷰를 구분한다.

## 읽기와 대응

1. 구현 시작·리뷰 시작·단계 완료 보고 전에 관련 항목을 확인한다.
2. 대응 후 항목의 `대응 기록`에 변경 파일/커밋, 실행한 검증 명령, 결과, 남은 제한을 적는다. 보류·비반영은 이유를 적는다.
3. 상태는 `미해결 → 수정됨·재검증 대기 → 검증 완료`로 구분한다. 설계 범위는 `범위 결정 필요`로 별도 표시한다.
4. 이후 독립 리뷰도 이 파일에 ID를 추가한다. 이미 확인된 문제를 다른 리뷰어의 승인만으로 닫지 않는다.

## 현재 항목

| ID | 중요도 | 상태 | 대상 | 요약 |
|---|---|---|---|---|
| REV-001 | P1 | 검증 완료 | P4 보호 정책 | 드라이브 루트 Known Folder 보호 및 기존 스캔 루트 제한 독립 확인 |
| REV-002 | P2 | 수정됨·재검증 대기 | P4 파일 순회 | 마지막 조회 경계 회귀 2개 통과, 관측 완료와 예산 초과 분리 |
| REV-003 | P2 | 수정됨·재검증 대기 | P2 UI / P7 | 실행 상태 이벤트와 구독 해제, 늦은 완료 후 안내 제거 회귀 통과 |
| REV-004 | 제품 범위 | 사용자 결정 반영 | 구현 계획 | npm·pip·NuGet HTTP 공식 정리와 Windows 도구 연결을 1차에 포함 |
| REV-005 | P2 | 검증 완료(장치 ID 범위) | P3a 내보내기 | 장치 내부 ID 원문 노출 보완 확인 |
| REV-006 | P2 | 수정됨·재검증 대기 | P5 앱 설정 읽기 | 사전 메타데이터 검사·본문 읽기 공급자 경계, 실제 링크 fixture 통과 |
| REV-007 | P2 | 수정됨·재검증 대기 | P5 앱 설정 읽기 | 읽기 실패 보존 및 기본 캐시 미탐지 시 최종 사유 카드 회귀 통과 |
| REV-008 | P1 | 검증 완료 | P7 정리 실행기 | 8.3 별칭 정규화(CanonicalPath) 및 별칭 fixture 거절 테스트 독립 확인 |
| REV-009 | P2 | 검증 완료 | P7 정리 실행기 | CachePathInspector 주입·관문 7종 테스트 독립 확인(ProfileList SID 분기·루프 내 ProtectedOrLinkedChild는 간접 커버) |
| REV-010 | P2 | 검증 완료(잔여 예외 경로) | P7 정리 실행기 | SP4 Task 13 `3d286be`: Kill(entireProcessTree)의 AggregateException을 Flatten해 내부가 모두 InvalidOperation/Win32(바깥은 +Timeout)이면 KillFailed/ProcessStillRunning 경로, 그 밖은 전파 |
| REV-011 | P2 | 검증 완료 | UI 커밋 | 헤더 6파일·공개 멤버 한글 주석·테스트 summary 독립 확인 |
| REV-012 | P3 | 검증 완료 | P7 정리 실행기 | 인자 2개 제거로 스펙 문구와 일치, 고정 인자 테스트 일치, NuGet --list 파싱 ko/ja/en 무관 확인 |
| REV-013 | P2 | 검증 완료 | UI 정리 결과 | Started 플래그로 미실행 코드 분리·Outcome 미생성·메인 화면 미잔존 독립 확인 |
| REV-014 | P2 | 검증 완료(SP4 표시) | UI 개요 | SP4 Task 5: 드라이버 타일 제거, 온라인 완료 판정에서 로컬 Driver CannotVerify 제외(온라인 공급자·NVIDIA 비교 규칙만), 온라인 상태는 옵션 영역 `LastOnlineCheckText`만 |
| REV-015 | 제품 범위 | 검증 완료(SP4 1·2항) | 개요 화면 | SP4 Task 5: 제안 1·2항(요약 타일 '바로 할 수 있는 것/직접 해야 하는 것', 0이면 숨김, 정리 창은 보호 위치 도구 있을 때만) 구현. 3·4항은 후속 단계 |
| REV-016 | P1(Task 10 선행) | 검증 완료(UI·backend) | Task 9→10 사용자 범위 | SP4 Task 10 `0c80a8b`: UI `CanOpenCacheTools`에 !IsSystemOnly, `SystemCacheToolBackend(limitToSystemScope)`가 Locate·Inspect·Clear를 관측 전 `UserScopeExcluded`로 거절. 승격 거절 제거는 이후 `34670c5` |
| REV-017 | P2 | 검증 완료(현재 UI 경로) | SP4 사양 수집 | SP4 Task 13 `3d286be`: 살아 있는 사양 프로브 Task를 probeId별 보관·재호출 차단, `Spec.IsDraining`으로 검사·정리 차단, 검사 종료 중(HasDrainingNote)엔 사양 새로 고침 차단. 최종 리뷰 후속 `ec3547e`: 프로브를 Task.Run으로 시작(이전에는 동기 실제 프로브에 추적이 적용되지 않았음) |
| REV-018 | P2 | 검증 완료(UI 경계) | SP4 실행 상호 배제 | SP4 Task 10 `0c80a8b`: `CanOpenCacheTools`에 !Spec.IsLoading, IsLoading 변경 시 CanOpenCacheTools 알림. SP4 Task 13 `3d286be`: `CanOpenCacheTools`·`CanStartScan`에 `!Spec.IsDraining` 관문·변경 알림 추가 |

## REV-001 — 보호 경로와 스캔 경로의 검증 정책을 분리

- 발견/확인: 2026-09-26, P4 작업본에서 재현. 이후 `9fed432`에 같은 로직이 커밋된 것을 소스로 재확인했다. 커밋 후 별도 테스트 재실행은 하지 않았다.
- 위치: `src/PcOptimizer.Probes/Storage/ProtectionPolicyResolver.cs`의 `Single`, `src/PcOptimizer.Probes/Storage/PathTemplate.cs`의 `NormalizeAbsolute`.
- 조건: 문서 Known Folder를 `D:\`로 지정한 환경.
- 실제: 보호 해석에서도 드라이브 루트를 거부하여 `D:\Temp\private`에 대한 `IsProtected`가 false다. 그 하위가 별도 스캔 루트로 잡히면 보호 제외가 적용되지 않는다.
- 기대: 드라이브 전체를 **스캔 대상으로 금지**하는 것과, 드라이브 전체를 **보호 대상으로 인정**하는 것은 구분해야 한다.
- 재현: [IndependentReviewTests.cs](repro/IndependentReviewTests.cs)의 `KnownFolderAtVolumeRootStillProtectsDescendants`. 임시 복사본 실행 결과 실패(Expected true, Actual false).
- 완료 근거: 위 테스트 통과, 일반 보호 경로 및 스캔 루트 금지 정책 회귀 확인.
- 대응 기록 (2026-09-26, P4 리뷰 1차 수정, 구현자):
  - 변경: `src/PcOptimizer.Probes/Storage/PathTemplate.cs`(보호 루트용 `ResolveProtected`/`NormalizeProtected` 추가 — 드라이브 루트 허용, UNC·상대 경로는 계속 거부; 스캔 루트용 `NormalizeAbsolute`는 드라이브 루트 금지 유지), `src/PcOptimizer.Probes/Storage/ProtectionPolicyResolver.cs`(Known Folder·환경 경로·동기화 루트 해석에 보호용 정규화 사용). 커밋 `5399f00`.
  - 회귀 테스트: 재현 테스트를 `tests/PcOptimizer.Tests/Unit/Probes/IndependentReviewTests.cs`로 편입(단언 유지). `KnownFolderAtVolumeRootStillProtectsDescendants` 통과. 스캔 루트 금지는 `ScanRootCatalogTests.다른_프로필과_볼륨_루트는_거부한다`(C:\·UNC·다른 프로필 → Rejected)가 계속 통과. 일반 보호 경로는 `ProtectionPolicyResolverTests` 전부 통과.
  - 명령(작업 루트, Release):
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~IndependentReviewTests"` — 수정 전 `실패: 3, 통과: 0`(Expected True / Actual False, OperationCanceledException 기대 불일치), 수정 후 전부 통과.
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~ProtectionPolicyResolverTests|FullyQualifiedName~FileScanRulesTests|FullyQualifiedName~PathTokenizerTests|FullyQualifiedName~ReportExporterTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~ScanRootCatalogTests"` — `통과: 80, 실패: 0`.
    - `dotnet build -c Release` — 경고 0개, 오류 0개.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke"` — `통과: 655, 실패: 0`.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category=Smoke"` — `총 16, 통과 16`(관리자 셸, 실제 파일 스캔 Success, timedOut=False).
  - 남은 제한: 정책 파일(protect.json) 자체의 `environmentPath` 항목에 드라이브 루트를 적는 것은 파서가 여전히 무효로 처리한다(정책 작성 오류 방지, 이번 항목 범위 밖). 드라이브 루트 Known Folder가 시스템 드라이브(C:\)이면 C: 위의 모든 스캔 루트가 보호로 제외되어 파일 검사 결과가 비게 된다(실패 시 닫힘 방향).

- 독립 재검증 (2026-09-26, Codex, 기준 `23f92e9`, 구현 `5399f00`/`4c901e4` 포함):
  - 별도 `git archive` 복사본에서 `KnownFolderAtVolumeRootStillProtectsDescendants`, 보호 정책·스캔 루트 제한 회귀를 포함한 기본 659/659 및 Smoke 16/16 통과.
  - 보호용 `NormalizeProtected`와 스캔용 `NormalizeAbsolute` 분리 확인. REV-001을 `검증 완료`로 변경한다.

## REV-002 — 폴더 내부에도 취소·시간 예산 검사 필요

- 발견/확인: 2026-09-26, P4 작업본에서 재현. `9fed432`에서 해당 두 루프의 누락이 유지됨을 소스로 확인했다.
- 위치: `src/PcOptimizer.Probes/Storage/VolumeTraversalRun.cs`, `ListDirectory`의 열거 및 처리 루프.
- 조건: 하위 폴더 없는 단일 폴더의 열거 도중 취소하거나, 수동 시간 공급자를 121초 진행시킨다(예산 120초).
- 실제: 취소해도 스캐너가 정상 반환한다. 예산 초과도 `TimedOut=false`로 반환한다. 폴더 외부 루프에만 검사가 있어 파일이 많은 폴더의 내부 작업이 중단 조건을 확인하지 않는다.
- 기대: 항목 열거·처리 중에도 협력적 취소와 예산을 확인한다. 예산 초과는 관측된 부분 합계와 시간 초과로 기록한다. OS 호출 한 번 자체를 강제로 중단할 수 있다는 뜻은 아니다.
- 재현: [IndependentReviewTests.cs](repro/IndependentReviewTests.cs)의 `CancellationInsideOnlyDirectoryIsObserved`, `BudgetExceededInsideOnlyDirectoryIsReported`. 임시 복사본에서 두 테스트 모두 실패.
- 완료 근거: 두 재현 테스트 통과, 큰 단일 폴더와 열거 중 부분 결과 처리를 검증.
- 대응 기록 (2026-09-26, P4 리뷰 1차 수정, 구현자):
  - 변경: `src/PcOptimizer.Probes/Storage/VolumeTraversalRun.cs` `ListDirectory` — 항목 열거 루프와 처리 루프에서 항목마다 `ThrowIfCancellationRequested`와 볼륨 예산을 확인. (1차, 커밋 `5399f00`) 항목 열거·처리 루프에 취소와 예산 확인을 넣었다. **정정**: 1차 기록의 '그때까지 관측한 항목만 집계'는 사실과 달랐다. 1차 코드는 처리 루프 시작 시 예산을 다시 확인해 이미 받은 항목을 버렸다(해당 폴더 0바이트). 1차 테스트의 61→41바이트 기대값 변경이 이를 가렸다.
  - 회귀 테스트: `IndependentReviewTests.CancellationInsideOnlyDirectoryIsObserved`, `BudgetExceededInsideOnlyDirectoryIsReported` 통과. 기존 `FileSystemScannerTests.시간_예산을_넘기면_부분_집계로_멈춘다`는 열거 도중 예산을 넘긴 폴더(d2)가 이제 관측 항목 없이 Timeout으로 남도록 기대값을 갱신(합계 41, Timeout 2, d2 열거 실패 사유 Timeout).
  - 명령(작업 루트, Release):
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~IndependentReviewTests"` — 수정 전 `실패: 3, 통과: 0`(Expected True / Actual False, OperationCanceledException 기대 불일치), 수정 후 전부 통과.
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~ProtectionPolicyResolverTests|FullyQualifiedName~FileScanRulesTests|FullyQualifiedName~PathTokenizerTests|FullyQualifiedName~ReportExporterTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~ScanRootCatalogTests"` — `통과: 80, 실패: 0`.
    - `dotnet build -c Release` — 경고 0개, 오류 0개.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke"` — `통과: 655, 실패: 0`.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category=Smoke"` — `총 16, 통과 16`(관리자 셸, 실제 파일 스캔 Success, timedOut=False).
  - 남은 제한: 확인은 협력적이다. OS 열거 호출 한 번(FindNextFile 묶음)이나 파일 ID·할당 크기 조회 한 번은 중간에 끊을 수 없으므로 그 호출이 끝난 뒤 다음 항목에서 멈춘다. 큰 단일 폴더의 실제 대량 항목(수십만 개) 성능 측정은 하지 않았다(가짜 열거로만 검증).
- 대응 기록 (2026-09-26, P4 리뷰 2차 수정, 구현자, 커밋 `4c901e4`):
  - 실제 동작:
    - 열거 중에는 항목을 받을 때마다 취소를 확인한다. 받은 항목을 목록에 넣은 뒤 예산을 확인하고, 넘기면 다음 항목을 요청하지 않는다.
    - 이미 받은 항목은 예산과 관계없이 모두 처리해 합계에 넣는다. 처리 루프는 취소만 확인한다.
    - 그 디렉터리는 `Timeout`으로 기록하고, 실행기·루트·볼륨은 `TimedOut=true`로 둔다.
    - 루트가 아무것도 관측하기 전에 예산을 넘기면 `RootScanState.TimedOut`으로 합계 없이 보고한다(0바이트 정상 루트로 보고하지 않음). 관측분이 있으면 부분 합계(`IsPartial`, Timeout 건너뜀 포함)와 `TimedOut=true`로 보고하고, 루트별 `fileScan.root[i].timedOut` 측정값을 추가했다.
    - 디렉터리당 실패 사유는 하나다. 열거 예외(접근 거부·사용 중)가 있으면 그 사유가 우선하고, `DirectoryNode.Fail`의 두 번째 호출은 건너뜀 개수를 늘리지 않는다.
  - 변경 파일: `src/PcOptimizer.Probes/Storage/VolumeTraversalRun.cs`, `DirectoryNode.cs`, `FileScanMeasurements.cs`, `src/PcOptimizer.Core/Rules/FileScanProbeContract.cs`; 테스트 `FileSystemScannerTests.cs`, 가짜 `FakeDirectoryEntrySource.cs`(OnEntry·OnProbeRoot), `FakeFileIdentityReader.cs`(OnIdentity).
  - 회귀 테스트:
    - `폴더_안에서_예산을_넘기면_받은_항목은_세고_시간_초과로_기록한다`: 세 번째 항목 뒤 예산 초과. 받은 3개(70바이트) 집계, 네 번째 항목 요청 없음, Timeout 1, TimedOut.
    - `루트_열거_중_예산을_넘기면_부분_합계와_시간_초과로_보고한다`: 100바이트 부분 합계, TimedOut.
    - `관측_전에_예산을_넘긴_루트는_합계가_없다`: TimedOut 상태, 합계 null, 열거 없음.
    - `디렉터리당_실패_사유는_하나다`: 열거 예외 뒤 처리 중 예산 경과. InUse 1, Timeout 0, Incomplete 1, 받은 2개 집계.
    - 기존 `시간_예산을_넘기면_부분_집계로_멈춘다`의 기대값을 61바이트로 복원했다(d2가 받은 f2 포함, d2·d1 Timeout 2). 재현 테스트 2건(`IndependentReviewTests`)도 계속 통과한다.
  - 명령(작업 루트, Release):
    - 소스 두 파일만 되돌린 상태(`git stash push -- src/.../VolumeTraversalRun.cs src/.../DirectoryNode.cs`)에서 `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~FileSystemScannerTests"`를 실행하면 `실패: 5, 통과: 13`이다(Expected 100/Actual 0, Expected 70/Actual 0, Expected TimedOut/Actual Scanned, Expected 61/Actual 41, Expected 134217728/Actual 67108864). 수정 코드를 복원한 뒤 통과했다.
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTempTreeTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests"` — `통과: 34, 실패: 0`.
    - `dotnet build -c Release` — 경고 0개, 오류 0개.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke"` — `통과: 659, 실패: 0`.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category=Smoke"` — `총 16, 통과 16`(실제 파일 스캔 Success, elapsedMs 17811, 볼륨·루트 timedOut=False).
  - 남은 제한:
    - 확인은 협력적이다. OS 열거 호출 한 번이나 파일 ID·할당 크기 조회 한 번은 끊지 못한다.
    - 예산을 넘긴 순간 받은 항목까지는 처리하므로, 그 처리 시간(크게는 64MiB 이상 파일의 ID 조회)만큼 예산을 조금 넘길 수 있다.
    - 예산을 넘긴 시점에 폴더가 사실 끝까지 열거됐을 수도 있지만, 더 요청하지 않으므로 보수적으로 시간 초과로 기록한다.
    - 실제 대량 항목 폴더의 성능은 측정하지 않았다(가짜 열거로만 검증).

- 독립 재검증 (2026-09-26, Codex, 기준 `23f92e9`):
  - 기존 재현 2건과 2차 부분 합계·0바이트·실패 중복 회귀를 포함한 기본 659/659, Smoke 16/16 통과. 기존 재현의 수정은 확인했다.
  - **남은 실패: 처리 단계의 시간 예산.** `ListDirectory`는 열거가 끝난 뒤 `entries` 전체를 처리하면서 취소만 확인한다. `HandleFile`은 아직 파일 ID·할당 크기 OS 조회를 하므로 단순 메모리 합산이 아니다. 처리 중에만 예산을 넘기면 마지막 폴더의 루트/볼륨은 `TimedOut=false`로 끝날 수 있다.
  - 추가 재현: [ProcessingBudgetReviewTests.cs](repro/ProcessingBudgetReviewTests.cs). 64MiB 파일 메타데이터 3개를 즉시 열거하고 가짜 ID 조회마다 시간을 121초 진행시킨다. 120초 예산인데 ID 조회 3회/가짜 경과 363초, 루트 `TimedOut=false`로 **1/1 실패**했다. 실제 363초 대기나 대용량 파일 생성은 없다.
  - 명령: 검증용 복사본 `tests/PcOptimizer.Tests/Unit/Probes/`에 위 파일을 복사하고 `dotnet test PcOptimizer.sln --configuration Release --no-restore --filter "FullyQualifiedName~ProcessingBudgetReviewTests"` 실행.
  - 요청: 이미 열거한 논리 크기를 보존하는 것과 추가 OS 조회를 계속하는 것을 분리한다. 예산 소진 뒤 신규 ID/할당 크기 조회는 중단하고 미확인·중복 가능 등 품질 저하와 시간 초과를 표시하거나, 미처리 구간을 명시한 부분 결과로 종료한다. 기존 부분 합계 보존 테스트도 유지한다. OS 호출 한 번을 강제로 중단하라는 요구는 아니다.
  - 구현자 기록의 ‘예산을 조금 넘길 수 있음’만으로 닫지 않는다. 현재 초과 시간은 받은 항목 수와 각 OS 조회 지연에 따라 누적된다. REV-002는 `부분 수정·추가 재현 실패`로 유지한다.
- 대응 기록 (2026-09-26, P4 리뷰 3차 수정, 구현자, 커밋 `c8d2c59`):
  - 대상: 독립 재검증(기준 `23f92e9`)의 처리 단계 재현 `ProcessingBudgetReviewTests.IdentityProcessingBudgetOverrunIsReported`.
  - 실제 동작(`VolumeTraversalRun.ListDirectory`/`HandleFile`):
    - 이미 받은 항목의 논리 크기·확장자·파일 수는 메모리 계산이므로 계속 집계한다.
    - 파일을 처리할 때마다 예산을 확인한다. 넘긴 뒤로는 새 OS 조회(64MiB 이상 파일의 파일 ID, 압축·희소 파일의 할당 크기)를 하지 않는다. 열거 단계에서 이미 예산을 넘겼으면 처리 단계의 조회도 하지 않는다.
    - 조회를 생략한 파일은 `DuplicatesPossible=true`와 조회 생략 수(`DirectoryTotals.LookupsSkipped`, 루트 측정값 `fileScan.root[i].lookupsSkipped`)로 품질을 낮춘다.
    - 디렉터리·루트·볼륨은 `TimedOut=true`로 둔다. Timeout 사유는 디렉터리당 한 번이며, 읽기 오류가 있으면 그 사유가 우선한다(2차 규칙 유지).
  - 변경 파일: `src/PcOptimizer.Probes/Storage/VolumeTraversalRun.cs`(파일 헤더 설명도 실제 동작으로 정정), `DirectoryNode.cs`, `DirectoryTotals.cs`, `FileScanMeasurements.cs`, `src/PcOptimizer.Core/Rules/FileScanProbeContract.cs`; 테스트 `tests/PcOptimizer.Tests/Unit/Probes/ProcessingBudgetReviewTests.cs`.
  - 회귀 테스트:
    - 재현을 `ProcessingBudgetReviewTests.cs`로 정식 편입했다(원 단언 유지).
    - 추가 `IdentityLookupsStopAfterBudgetButLogicalSizesArePreserved`: 논리 크기 3×64MiB 보존, 파일 ID 조회 1회, 조회 생략 2, 할당 크기 조회 없음, Timeout 1, Incomplete 1, 볼륨 시간 초과.
    - 2차에서 추가한 부분 합계 테스트(61바이트, 70바이트, 루트 부분 합계, 관측 전 시간 초과, 사유 하나)는 모두 그대로 통과한다.
  - 명령(작업 루트, Release):
    - 테스트 파일과 `DirectoryTotals.LookupsSkipped` 필드만 추가하고 동작은 바꾸지 않은 상태에서 `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~ProcessingBudgetReviewTests"` — `실패: 2, 통과: 0`(원 재현 메시지 `calls=3, elapsed=363s; root TimedOut=false`). 수정 후 통과.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "FullyQualifiedName~ProcessingBudgetReviewTests|FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTempTreeTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests"` — `통과: 36, 실패: 0`.
    - `dotnet build -c Release` — 경고 0개, 오류 0개.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke"` — `통과: 760, 실패: 0`(P5 테스트 포함).
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category=Smoke"` — `총 18, 통과 18`(실제 파일 스캔 Success, elapsedMs 22865, 루트·볼륨 timedOut=False, lookupsSkipped 0).
  - 남은 제한:
    - OS 호출 한 번(열거 묶음 한 번, 파일 ID 조회 한 번, 할당 크기 조회 한 번)은 중간에 끊을 수 없다. 예산 초과분은 이제 진행 중이던 OS 조회 한 번(또는 열거 호출 한 번)으로 제한되며, 받은 항목 수만큼 누적되지 않는다.
    - 이미 받은 항목의 메모리 집계(확장자·크기 합산)는 예산 뒤에도 끝까지 수행한다. 이는 OS 호출이 아니라서 짧지만 0은 아니다.
    - 예산 초과 뒤 받은 큰 파일들은 하드링크 중복 확인이 빠져 논리 크기 추정(중복 가능)이 된다.
    - 실제 대량 항목 폴더에서의 시간은 측정하지 않았다(가짜 열거·가짜 조회로 검증).
- 대응 기록 (2026-09-26, P4 리뷰 4차 수정, 구현자, 커밋 `b00fa49`) — 3차 기록 정정:
  - 정정: 3차 기록의 "예산 초과분은 진행 중이던 OS 조회 한 번으로 제한"은 `c8d2c59`에서 사실이 아니었다. 예산 확인이 파일마다 한 번이라, 64MiB 이상이면서 압축·희소인 파일은 파일 ID 조회가 예산을 넘겨도 같은 파일의 할당 크기 조회가 이어졌다. 실제 상한은 조회 두 번이었다.
  - 수정: `VolumeTraversalRun.HandleFile`이 할당 크기 조회 직전에 예산을 다시 확인한다. 넘겼으면 조회하지 않고 `LookupsSkipped`를 늘리며, 이후 파일도 조회하지 않도록 `ListDirectory`에 알린다. 이제 예산을 넘긴 뒤 수행되는 OS 조회는 이미 진행 중이던 한 번뿐이다. `ListDirectory` 문서 주석도 이에 맞게 고쳤다.
  - 회귀 테스트: `ProcessingBudgetReviewTests.AllocatedSizeLookupIsSkippedWhenIdentityLookupCrossesBudget`. 64MiB 압축 파일의 ID 조회가 121초를 진행시키는 상황에서 할당 크기 조회 0회, 파일 ID 조회 1회, LookupsSkipped 1, 논리 크기 보존, Timeout 1, 루트·볼륨 TimedOut을 확인한다. 가짜 파일 ID 공급자에 `AllocatedCalls` 기록을 추가했고, 3차 테스트에는 할당 크기 조회 없음 단언을 더했다.
  - 명령(작업 루트, Release):
    - `VolumeTraversalRun.cs`만 `git stash`로 되돌린 상태에서 `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~ProcessingBudgetReviewTests"` — `실패: 1, 통과: 2`(`Assert.Empty() Failure: Collection was not empty` — 할당 크기 조회가 일어남). 복원 후 통과.
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~ProcessingBudgetReviewTests|FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTempTreeTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests"` — `통과: 37, 실패: 0`.
    - `dotnet build -c Release` — 경고 0개, 오류 0개.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke"` — `통과: 761, 실패: 0`. 이번 라운드는 지시에 따라 Smoke를 실행하지 않았다.
  - 남은 제한:
    - OS 호출 한 번(열거 묶음, 파일 ID 조회, 할당 크기 조회)은 중간에 끊을 수 없다. 예산을 넘긴 뒤의 초과는 그 진행 중인 호출 한 번으로 제한된다.
    - 스캔 전체의 마지막 OS 조회에서 예산을 넘기면(뒤에 확인할 항목이 없으면) 데이터는 모두 관측됐지만 `TimedOut=false`로 끝난다. 이 경우는 최종 리뷰로 미뤘다.

- 독립 재검증 (2026-09-26, Codex, 기준 `1b9a9ad`, P4 구현 `c8d2c59`/`b00fa49` 포함):
  - 별도 `git archive` 복사본 Release 빌드 경고 0/오류 0. P4 관련 회귀 37/37, 전체 기본 761/761 통과. P5 수정 중인 미커밋 파일은 포함하지 않았다. 이번 재검증에서는 Smoke를 실행하지 않았다.
  - 37개 필터: `FullyQualifiedName~ProcessingBudgetReviewTests|FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTempTreeTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests`. 실행 명령은 `dotnet test PcOptimizer.sln --configuration Release --no-build --no-restore --filter "<필터>"`이며 전체 기본은 `Category!=Smoke`다.
  - 원래 처리 단계 재현 통과, 예산 초과 뒤 ID 조회 1회로 제한, 남은 논리 크기 보존·조회 생략 표시, 같은 압축 파일의 추가 할당 크기 조회 생략까지 확인했다. 앞선 핵심 수정 요청은 반영됐다.
  - 구현자가 4차 제한으로 남긴 마지막 OS 조회 초과 시 `TimedOut=false`는 코드에도 남아 있다. 다음 항목이 없는 비압축 파일 ID 조회나 최종 할당 크기 조회 뒤에는 예산 초과를 표시할 경로가 없다. 새 결함으로 중복 등록하지 않고 이 항목의 보류 경계로 유지한다. P7에서 종료 시점 예산 상태를 검증하고, 관측 완료 여부와 시간 예산 준수 여부를 구분해 마무리한다.
  - 상태를 `주요 수정 검증·최종 조회 경계 보류`로 변경한다. P5 진행을 막는 요청은 아니며, P4 전체 무결함 또는 P7 검증 완료를 뜻하지 않는다.

## REV-003 — ‘종료 중’ 안내 자동 갱신

- 위치: `src/PcOptimizer.App/ViewModels/MainViewModel.cs`, `ApplyResult` / `CreateDrainingNote`.
- 실제: 안내는 결과 적용 시 한 번 계산된다. 늦게 종료한 프로브의 실제 상태가 바뀌어도 UI에는 다음 검사까지 남는다.
- 확인: 소스 검토. UI 실제 조작 재현은 미실시. 기존 테스트는 종료 중 표시 생성만 확인한다.
- 기대: 현재 실행 상태 변경을 UI에 전달해 안내를 해제하거나, 문구가 검사 종료 시점의 기록임을 명확히 구분한다.
- 기존 처리: 계획 문서는 P7/최종 리뷰에서 자동 갱신 여부를 결정한다고 기록했다. 이는 수정 완료가 아니다.
- 대응 기록: 아직 없음.

## REV-004 — 기존 도구 통합 범위 확인

- 사용자 의도: ‘기존 여러 가지 도구들의 통합 사용’으로 초보자가 진단부터 수동/자동 대처까지 이어갈 수 있는 제품.
- 현재 계획: 자체 진단 화면을 1차로 두고, 기존 도구 통합은 계획 문서 5장에서 2차로 미뤘다.
- 검토 의견: 진단 기반 구현은 가능하나, 이 연기를 사용자가 승인한 범위 결정으로 간주하지 않는다. 진단 항목별 Windows 기능/공식 도구/기존 프로그램, 연결 방법, 조치 후 재확인 항목을 표로 정리하고 출시 범위를 명시할 필요가 있다.
- 이 항목은 코드 결함이나 임의의 작업 중단 명령이 아니다. 구현 진행과 별도로 범위 결정 근거를 기록한다.
- 대응 기록 (2026-09-26, 구현 세션 컨트롤러): 설계 스펙 §13에 진단 항목별 연결 도구(1차 안내·열기 / 2차 자동 조치)와 조치 후 재확인 표 초안을 작성해 커밋했다(`099b4b9`). 1차 출시 범위 결정 3건(1차 연결 포함 여부, 2차 자동 조치 대상, 커뮤니티 규칙 표시 방식)은 사용자 결정 대기. 코드 변경 없음. 상태 `범위 결정 필요` 유지.

- 부록 초안 독립 검토 (2026-09-26, Codex, `099b4b9`):
  - 진단별 연결 도구와 재확인을 분리한 표는 범위 결정 자료로 확인했다. 초안이므로 사용자 승인으로 처리하지 않는다.
  - 표의 `앱 캐시(검토 규칙 7종) → 검토 규칙만 격리 이동`은 범위를 다시 써야 한다. 현재 7종에는 Squirrel 앱 버전 폴더와 NuGet 전역 패키지가 포함되고, 스펙 §5는 이들의 미사용·재다운로드 가능을 보장하지 않는다. ‘검토된 관측 규칙’이라는 이유만으로 자동 격리 허용 목록이 되지 않는다. Squirrel 버전 폴더를 자동 조치 범위에서 명시적으로 제외하고, 나머지도 실행용 검증 조건을 별도로 정한다. 아직 실행 기능이 없으므로 현재 코드가 삭제한다는 지적은 아니다.
  - §13 도입부의 ‘1차는 설정 URI와 공식 링크만 열기’와 같은 표의 ‘미분류 폴더 탐색기 열기’가 충돌한다. 탐색기 열기를 1차로 넣을지 보류할지 명시하고, 포함한다면 관측된 로컬 경로만 여는 별도 동작 계약을 정의한다.
  - 이 두 문구를 정리한 뒤 1차 연결 범위를 사용자 결정 자료로 제시한다. 구현 중인 진단 기능은 계속 진행할 수 있다.

## REV-005 — 장치 ID 익명화 검증 이력

- 최초: P3a `f3c657b` 내보내기에 모니터 장치 경로, GPU PnP ID, 물리 디스크 제공자 ID 원문이 남았다.
- 보완: `7de6324`에서 내보내기 단위 토큰화 도입. 후속 `a450fac`에서 문장 부호 처리 보완.
- 독립 확인: 보완 작업본을 임시 복사본에 적용한 429개 테스트 통과, 실제 내보내기의 모니터/어댑터/PnP/디스크 ID 토큰 치환 확인. P3b 커밋의 기본 523개 및 스모크 13개도 재실행 통과.
- 한계: 장치 ID 범위의 확인이다. P4 경로, 시작 명령의 인자 등 향후 추가되는 모든 데이터의 익명화를 포괄 승인하지 않는다.

## REV-006 — 설정 파일 본문 읽기에도 링크 경계 적용

- 독립 발견: 2026-09-26, 기준 `96a593c`(P5 3차 수정 포함). 기존 캐시 대상의 정션 미추종 수정과 다른 입력 경로다.
- 위치: `src/PcOptimizer.Probes/Applications/ConfigReaders/ConfigFileText.cs:35`, `src/PcOptimizer.Probes/Platform/SystemPathEnvironment.cs`의 `ReadSmallTextFile`, 각 설정 리더.
- 실제: `ConfigFileText.Read`는 `environment.ReadSmallTextFile`을 먼저 호출한다. 그 구현은 크기 확인 뒤 `File.ReadAllText`를 호출하며 중간 reparse·파일 링크·placeholder를 확인하지 않는다. `AppCacheProbe`의 `ReparseAncestorCheck`는 탐지/관측 대상에 적용되고, 설정 파일 본문을 읽는 이 경로에는 전달되지 않는다. 설정에서 읽은 결과 경로를 나중에 분류해도 이미 읽은 본문을 보호할 수 없다.
- 재현: [ConfigReadBoundaryReviewTests.cs](repro/ConfigReadBoundaryReviewTests.cs)의 `ConfigUnderReparseAncestorMustNotBeRead`. 프로필 경로의 메타데이터가 ReparsePoint인 가짜 환경에서도 `.npmrc` 본문 읽기가 기록돼 `Assert.Empty(environment.FileReads)` 실패. 실제 개인 파일은 읽지 않았다.
- 요청: 허용된 작은 설정 파일 예외에도 본문 읽기 전 중간 경로와 파일 자체의 reparse/placeholder 경계를 적용하고, 건너뛴 사유를 반환한다. 허용 설정 파일 예외를 없애거나 Program Files 아래 Steam 설정을 일괄 금지하라는 요구는 아니다. 파일 링크는 현재 `ProbeRoot`가 파일을 먼저 NotDirectory로 처리하므로 디렉터리 전용 상태만으로 검증하지 말 것.
- 완료 근거: 재현 통과, 정상 설정 읽기 유지, 중간 정션·파일 링크·placeholder 각각에서 본문 읽기 미호출 검증.
- 대응 기록: 아직 없음.

## REV-007 — 설정 파일 읽기 실패를 부재와 구분

- 독립 발견: 2026-09-26, 기준 `96a593c`.
- 위치: `ConfigFileText.cs:38`. 읽기 결과가 null이면 `ProbeRoot(path) == NotDirectory`일 때만 Unreadable이다. `AccessDenied`/`Error`는 false가 되어 npm 리더가 NotConfigured로 돌려준다.
- 재현: 같은 [ConfigReadBoundaryReviewTests.cs](repro/ConfigReadBoundaryReviewTests.cs)의 `DeniedConfigMustNotLookAbsent`. `.npmrc` 읽기 null + ProbeRoot AccessDenied에서 기대 Unreadable, 실제 NotConfigured로 실패.
- 영향: 사용자가 별도 캐시 위치를 설정했지만 읽을 권한이 없을 때 ‘설정 없음/기본 위치만’처럼 보일 수 있다.
- 요청: Missing만 부재로 처리하고 접근 거부·조회 오류는 읽기 실패/확인 불가로 구분한다. 리더 결과뿐 아니라 `AppCacheProbe.ConfigDetectedRuleIds`/`reportedConfigs`까지 확인해, 기본 탐지가 없는 앱도 실제 설정 읽기 실패 사유가 요약 또는 카드에 남게 한다. 후자의 전파 경로는 소스상 확인 필요 사항이며 별도 파이프라인 재현은 아직 하지 않았다.
- 완료 근거: 재현 통과, 없는 파일은 NotConfigured 유지, 실패 사유가 최종 결과에서 사라지지 않는 회귀 검증.
- 대응 기록: 아직 없음.

## 2026-09-27 독립 리뷰 (Claude 세션, 범위 `9aa45b3..ad06cca`)

전체 보고서: [2026-09-27-claude-independent-review.md](2026-09-27-claude-independent-review.md). diff·커밋 문서·`.trx`만으로 검토했고 빌드·테스트는 재실행하지 않았다(동시 작업 세션의 미커밋 변경으로 작업 트리 빌드가 깨져 있었음). 작업 트리 미커밋 변경은 범위 밖.

- 실행기 안전: 승인 허용 목록 안에서만 동작. 셸 없음, 규칙 문자열 인자 없음, 승격·현재 프로필·포함 보호 정책·다른 사용자·reparse/placeholder·완전 열거·NuGet .dat 전용 관문이 코드에서 강제되고 실행 직전 재검사됨. Critical 없음.
- REV-002/003/006/007: diff와 편입된 회귀 테스트로 수정 확인(ADDRESSED). 독립 재실행은 하지 않았으므로 상태는 바꾸지 않는다(Codex 독립 재검증 몫).
- REV-004: 09-27 스펙 허용 목록과 구현 일치. 인자 2개 초과분은 REV-012.
- 판정: 일반 권한 수동 검증 진행 가능. 출시 전 REV-008~012 필수.

## REV-008 — 캐시 경로 8.3 별칭 미정규화

- 위치: `src/PcOptimizer.Probes/Actions/SystemCacheToolBackend.cs:44`(`Path.GetFullPath`만), `Inspect :118-122`(`StartsWith`/`IsProtected` 접두 비교).
- 실제: `DOCUME~1` 같은 8.3 별칭으로 설정된 캐시 경로가 프로필 하위 검사를 통과하고 Known Folder 보호를 우회할 수 있다(IsProtected 내부의 GetLongPathNameW 여부는 diff에서 미확인). 악용성은 낮음(자기 설정, npm/pip는 하위만 삭제, NuGet은 .dat 검사).
- 기대: P4/P5 가드와 동일하게 `environment.NormalizePath`(GetFullPath+GetLongPathNameW) 정규화 후 비교.
- 완료 근거: 8.3 별칭 fixture가 보호 경로로 거절되는 테스트, 기존 정리 테스트 회귀.
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): `CachePathInspector.CanonicalPath`로 캐시·현재 프로필·보호 루트를 긴 절대 경로로 정규화한다. 없는 말단도 조상부터 정규화하고, 남은 `~` 별칭은 보수적으로 거절한다. Locate도 같은 정규화를 사용한다. `OtherUserLocationGuard.Create`의 현재 프로필 정규화도 맞췄다. `CachePathInspectorTests`의 보호 폴더 별칭/없는 말단/프로필 별칭/미해석 별칭 회귀 통과. 실제 OS 스모크는 존재·없는 말단 보호를 확인했으나 이 볼륨은 새 폴더에 8.3 별칭을 만들지 않아 실제 별칭 우회 재현까지 확인한 것은 아니다.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.

## REV-009 — Inspect 관문 직접 테스트 부재

- 위치: `SystemCacheToolBackend.Inspect`(private/static, 실제 WindowsIdentity·레지스트리·FS 의존). 보호 거절 테스트는 가짜 `Allowed=false`(`CacheCleanupTests.cs:83`)뿐.
- 미검증 관문: OutsideUserProfile, ProtectedPath, 다른 사용자, 링크/placeholder 하위, InspectionIncomplete, UnexpectedHttpCacheContent, 승격 거절. 09-27 스펙 출시 검증 항목 "보호 거절"에 해당.
- 기대: IPathEnvironment/IRegistryReader/IDirectoryEntrySource/승격 상태 주입, 관문별 가짜 FS 테스트.
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): `CachePathInspector`에 IPathEnvironment/IRegistryReader/IDirectoryEntrySource/승격/SID/정책/단조 시간을 주입했다. 실행기는 이 클래스를 실행 직전에 호출한다. `CachePathInspectorTests`에서 프로필 밖, 보호 대상과 상위, 다른 사용자, 루트/조상/하위 정션, 루트/하위 placeholder, 접근·열거 실패, 항목/시간 예산(빈 폴더 마지막 열거 포함), NuGet 비-.dat, 승격을 직접 검증했다. 단순 Allowed=false 가짜 백엔드만 검사한 것이 아니다.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.

## REV-010 — Kill 실패 경로 무기록·오진

- 위치: `src/PcOptimizer.Probes/Actions/CacheToolProcess.cs:69-71`.
- 실제: Kill 예외를 삼키고 전역 `_unfinishedProcess`를 세워 이후 모든 RunAsync(LocateAsync 포함)가 false → UI가 `Cleanup_Unavailable`("도구가 설치돼 있어야…")로 표시. 로그 없음.
- 기대: 실패 로그(형식 이름), 별도 코드(ProcessStillRunning)로 UI 안내, 거절 플래그는 유지(올바름).
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): `CacheProcessGuard`가 Kill/종료 관측 예외 유형만 로그에 남기고, 실제 종료 확인 전까지 실행을 막는다. 핸들을 보존해 다음 요청에서 종료를 확인한 뒤에만 차단을 해제한다. `ProcessStillRunning`을 backend→service→VM 전용 문구로 전달한다. 프로세스 전역 중복 실행은 대기열 없이 Busy로 거절한다. `CacheProcessGuardTests`의 Kill 실패+Timeout/뒤늦은 종료/익명 로그, `CacheCleanupTests.RunningToolIsNotMisdiagnosedAsUninstalled` 통과. 죽일 수 없는 실제 프로세스를 생성한 테스트는 아니다.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.
- 대응 기록 (2026-09-27, Claude Opus 5.5 구현자, SP4 Task 13): 상태 `수정됨·재검증 대기`(Task 9 시점 잔여 재현 `AggregateKillFailureMustNotEscapeGuard` 대응).
  - 커밋 `3d286be`, `src/PcOptimizer.Probes/Actions/CacheProcessGuard.cs`: `kill()` 예외가 `AggregateException`이면 `Flatten()`한 내부 예외가 1개 이상이고 모두 `InvalidOperationException`/`Win32Exception`일 때 `KillFailed type=AggregateException inner=…`(형식 이름만)로 기록하고 `wait()`를 계속한다. 바깥 catch도 같은 규칙에 `TimeoutException`을 더해 `ProcessStillRunning`을 기록하고 false. 그 밖의 내부 예외(또는 빈 AggregateException)는 기존대로 전파. 시작 이력·핸들 소유권(`_hasExited`/`_dispose` 보존)·익명 로그는 그대로.
  - 테스트(`tests/PcOptimizer.Tests/Unit/App/CacheProcessGuardTests.cs` — 기존 파일 위치 유지): `AggregateKillFailureMustNotEscapeGuard`(재현 소스 이름·단언 보존), `AggregateKillAndWaitFailureKeepsBlockerWithTypeOnlyLogs`(중첩 Aggregate+대기 Aggregate(Timeout) ⇒ false·차단 유지·핸들 미해제·메시지 비노출·실제 종료 후 복구), `AggregateWithUnexpectedInnerExceptionPropagates`(ArgumentException 섞이면 전파). RED: 수정 전 `System.AggregateException : One or more errors occurred. (fixture)`가 `StopAsync` 밖으로 전파(원장 기록과 동일).
  - 검증: REV-017 기록과 같음(빌드 0/0, 기본 1245/1245, Smoke 22/22). 죽일 수 없는 실제 프로세스 트리로 재현한 것은 아니다(대역 예외). 독립 재검증 필요.

## REV-011 — 파일 헤더·XML 주석 누락

- 위치: `src/PcOptimizer.App/ViewModels/MainViewModel.Overview.cs:1`(신규, 헤더 없음), `Views/CacheToolsWindow.xaml:1`(신규, 없음), `Views/MainWindow.xaml:1`(이 범위에서 헤더 제거). 공개 멤버 주석: `FindingCardViewModel.cs:92,163-166`, `MainViewModel.Overview.cs:14-41`, `MainViewModel.cs:94`.
- 기대: 공통 제약의 헤더·한글 XML 주석 복구. 브랜치 마무리 전 필수.
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): 지적된 UI 파일 6개의 @file/@author/@brief 헤더를 복원했다. FindingCardViewModel/MainViewModel.Overview/MainViewModel/CacheToolsViewModel/CleanupOutcomeViewModel 및 CleanupOutcomeView 공개 멤버·생성 프로퍼티·명령의 한글 설명과 해당 테스트 summary를 보완했다. Release 빌드 0경고/0오류, git diff --check 통과.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.

## REV-012 — 승인 인자 목록 초과

- 위치: `CacheToolProcess.cs:89`(`--disable-pip-version-check`), `:91`(`--force-english-output`).
- 실제: 09-27 스펙 "…만 허용한다" 목록에 없는 인자. 무해하나 사용자 승인 목록·고정 인자 테스트(`CacheCleanupTests.cs:133`)와 정확히 일치해야 한다.
- 기대: 인자 삭제(pip는 `PIP_DISABLE_PIP_VERSION_CHECK=1` env와 중복) 또는 스펙 개정 후 테스트 갱신. 사용자 확인 필요.
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): 허용 목록을 넓히지 않고 `--disable-pip-version-check`와 `--force-english-output`을 조회/정리 양쪽에서 삭제했다. 기존 `PIP_DISABLE_PIP_VERSION_CHECK=1`, `DOTNET_CLI_UI_LANGUAGE=en-US` 환경 설정은 유지한다. 따라서 스펙 확장 승인을 요청할 필요가 없다. `CommandsAreFixedAndDoNotUseShell`의 pip/dotnet 정확한 배열 검사와 실제 임시 캐시 npm·pip·NuGet 명령 검증 통과.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.

## 2026-09-27 독립 리뷰 2 (Claude 세션, 커밋 `4e8a5bc`)

전체 보고서: [2026-09-27-claude-independent-review.md](2026-09-27-claude-independent-review.md) 하단 "4e8a5bc" 절. diff·`.trx`만으로 검토. 컨트롤러가 HEAD `d6f926e`에서 Release 빌드 0/0, 기본 1122/1122, Smoke 21/21을 직접 실행해 확인했다(Online·ToolSmoke 미실행).

- 실행기·관문: 변경 없음(BeforeBytes 추가만). 확인 문구를 계획에서 재구성한 것은 개선. 새 내비게이션은 필터만이며 정리 서비스에 닿지 않는다.
- 결과 의미: "확인됨"은 실행 직전 재측정과 도구 후 재측정의 비교이며 종료 코드에 의존하지 않는다. "확보 완료" 표현 없음.
- 스모크 +56줄은 읽기 전용 ScanService 위의 실제 MainViewModel 구동이며 도구·실제 캐시를 건드리지 않는다.
- 판정: Needs fixes — REV-013, REV-014, REV-011(추가분).

## REV-013 — 실행 전 거절을 정리 실패로 표시

- 위치: `src/PcOptimizer.App/ViewModels/CacheToolsViewModel.cs:93-94,101-105`, `CleanupOutcomeViewModel.cs:13,17`, `MainWindow.xaml.cs:41`.
- 실제: `ExecuteAsync`의 모든 결과에 Outcome을 만들어, 도구를 실행하지 않은 Busy·PlanExpired·TargetChanged·Blocked·NormalUserRequired도 "최근 정리: 완료를 확인하지 못했습니다" + "일부 캐시만 정리됐을 수 있습니다"로 표시되고 메인 화면에 재검사 후에도 남는다. 같은 창의 Message는 `Cleanup_Changed`로 모순. 예외 catch는 도구 시작 전 예외에도 `"ToolFailed"`를 부여.
- 기대: 도구가 실제 실행된 경우(Completed/ToolFailed)에만 Outcome 생성, 미실행 코드는 "실행하지 않음" 제목. PlanExpired·TargetChanged 테스트 추가.
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): `CacheToolExecution`/`CacheCleanupResult.Started`로 실제 프로세스 시작을 전달한다. 시작 전 거절·예외는 미실행 안내만 표시하며 새 Outcome/NeedsRescan을 만들지 않고 이전 실행 이력을 보존한다. 시작 후 실패나 후속 관측 실패는 실행 이력과 재검사를 유지한다. 만료/변경/보호/시작 실패/사전 예외/종료 대기, Busy Started=false, 과거 Outcome 보존 회귀 통과. 준비 조회와 Windows 설정 열기 오류 문구도 정리 실패에서 분리했다.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.

## REV-014 — 드라이버 타일이 온라인 요청을 비교 완료로 취급

- 위치: `src/PcOptimizer.App/ViewModels/MainViewModel.cs:336`(`_lastScanIncludedOnline = onlineRequested`, Cancelled/Partial 포함), `MainViewModel.Overview.cs` DriverCount, 테스트 `MainViewModelTests` diff L927-928.
- 실제: 취소된 온라인 검사나 NVIDIA/WUA 조회 실패 뒤에도 "후보 0개"로 표시되어 비교 완료와 구분되지 않는다.
- 기대: 드라이버·온라인 Finding의 실제 상태(완료/NotRequested/NetworkFailed/Cancelled) 또는 ScanOutcome.Completed로 라벨 도출, 별도 "온라인 확인 불가" 상태.
- 대응 기록 (2026-09-27, Codex 구현자, `4c1e93d`): NVIDIA·WUA 프로브가 모두 Success(이슈/종료 중 없음)이고 Driver CannotVerify가 없을 때만 비교 완료로 표시한다. 실패·부분·취소·누락은 온라인 확인 불가, 후보가 있으면 후보 수와 일부 확인 불가를 함께 표시한다. 마지막 온라인 확인 시각도 실제 비교 완료 때만 갱신한다. `MainViewModelTests`에서 성공0/실패/부분/취소/Skipped/체크박스만 변경/로컬 재검사/부분 후보를 검증했다. 온라인 공급자 코드는 변경하지 않았다.
- 검증 근거: [REV-008~014 수정·검증 기록](2026-09-27-rev008-014-fixes.md). 기본 1169/1169, Smoke 22/22, ToolSmoke 1/1. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.
- 대응 기록 (2026-09-27, SP4 Task 5 구현자 Claude 세션, 커밋 `e58cae9`(부모 `031e9da`) "SP4: 요약 타일을 바로 할 수 있는 것/직접 해야 하는 것으로 교체, 도구 정리 노출 조건"): 드라이버 타일 제거(SP4 Task 5). `MainViewModel.Overview.cs`의 `DriverCount`·`SettingsCount`와 `_lastScanIncludedOnline`을 삭제해 타일 문구 경로를 없앴고, 온라인 상태는 옵션 영역 `LastOnlineCheckText`만 사용한다. 로컬 CannotVerify가 온라인 완료 판정에 섞이는 코드 경로 삭제: `OnlineComparisonComplete`(마지막 온라인 확인 시각 갱신에 계속 쓰임)의 조건 `Category == Driver && CannotVerify`를 `DriverUpdateRule.FINDING_ID_PREFIX`(NVIDIA 온라인 비교) CannotVerify로 좁혔다 — NVIDIA·WUA 두 프로브 Success(이슈 0, 종료 중 아님) 조건은 유지. 회귀: `MainViewModelTests.OnlyOnlineRuleCannotVerifyBlocksOnlineCompletion`(로컬 Driver CannotVerify `gpu-vendor-link:*` + 두 프로브 성공 → 확인 시각 표시, `driver-update:*` CannotVerify → 미표시), `LastOnlineCheckRequiresActualSuccessfulOnlineResults`(성공/실패/부분/취소/Skipped), `PartialOnlineResultWithCandidateDoesNotRecordOnlineCheck`, `OfflineComparisonAndLastCleanupRemainExplicitAfterRescan`(체크박스만 변경은 비교 완료 아님). 검증: `dotnet build PcOptimizer.sln --configuration Release` 경고 0/오류 0, `dotnet test ... --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"` 1205/1205. 남은 제한: 실제 Intel iGPU/AMD PC에서 온라인 확인은 실행하지 않았다(가짜 프로브·규칙 fixture). 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.

- 추가 명시(컨트롤러, Task 5 리뷰 반영): SP4 이후 메인 화면에 별도의 '온라인 확인 불가' 라벨은 없다. 온라인 조회 실패는 전체 결과의 사유 카드와 '확인 불가 n개' 설명으로만 드러나며, 옵션 영역의 마지막 온라인 확인 시각은 성공한 시각만 갱신된다(실패 시 이전 값 유지). 전용 상태 표시는 최종 리뷰에서 필요 여부를 다시 판단한다.

## REV-015 — '공간 확보' 기대 오해 방지 (제안, 사용자 결정 대기)

- 발견: 2026-09-27 사용자 지적. 배포 대상은 개발을 모르는 일반인인데, 첫 화면의 "공간 확보" 묶음이 실제로 자동 실행하는 것은 npm·pip·NuGet HTTP 캐시 정리뿐이라 개발 도구가 없는 PC에서는 "누르면 뭔가 나온다"는 기대와 결과가 어긋난다. 진단은 GB 단위 관측 크기를 보여주므로 오해가 커진다.
- 제안(코드 범위: 개요 화면 문구·필터·카드 노출 조건. 실행기 불변):
  1. 목적 제목 "공간 확보" 대신 결과 유형으로 센다: "이 PC에서 바로 할 수 있는 정리 n건 / 직접 해야 하는 정리 n건". 자동 정리 후보 0이면 그 묶음을 숨긴다.
  2. 개발 도구 정리 카드·요약 건수는 도구가 설치된 PC에서만 생성한다("도구가 설치돼 있어야…" 안내 카드 제거).
  3. 일반인용 공간 확보는 Windows 내장 정리(저장소 센스·디스크 정리 설정 URI)로 연결한다. 임시 파일·업데이트 캐시 카드의 주 버튼이 그 화면을 연다.
  4. 크기 숫자 옆에 행동 가능성 라벨을 고정한다: "앱에서 정리 가능" / "Windows 설정에서 정리" / "확인만 가능". 세 번째는 요약 합계에서 제외한다.
- 완료 근거: 개발 도구 없는 가짜 환경에서 개요에 자동 정리 묶음이 없고 Windows 정리 연결이 보이는 테스트; 도구 있는 환경에서 기존 카드 유지; "확보 가능" 표현 부재 회귀 유지.
- 대응 기록 (2026-09-27, Claude 세션): 사용자 승인 2차 개선 설계 `docs/superpowers/specs/2026-09-27-improvement-phase2-design.md` §3에 흡수(요약 타일을 '바로 할 수 있는 것/직접 해야 하는 것'으로, 개발 도구 카드는 보호 위치 설치 시만). 구현은 SP4 단계. 코드 변경 없음.
- 대응 기록 (2026-09-27, SP4 Task 5 구현자 Claude 세션, 커밋 `e58cae9`(부모 `031e9da`) "SP4: 요약 타일을 바로 할 수 있는 것/직접 해야 하는 것으로 교체, 도구 정리 노출 조건"): 요약 타일 교체·도구 카드/정리 창 노출 조건 구현. 메인 화면 타일 3개(공간 확보·설정 개선·드라이버 확인)를 "바로 할 수 있는 것 n건 / 앱에서 확인 후 바로 실행합니다"(0이면 숨김)와 "직접 해야 하는 것 n건 / Windows 설정이나 공식 페이지에서 직접 합니다" 두 타일로 바꿨다. 건수는 커뮤니티를 제외한 Candidate(`RecommendedCards`)만 세며, 앱 내 실행 판정 `IActionAvailability`/`CacheToolActionAvailability`는 `appCache.app:` + 검토 규칙 appLabel(`npm`·`pip`·`NuGet`, 대소문자 무시) Candidate이고 보호 위치 도구가 있을 때만 true. 정리 창 버튼은 생성 시 한 번 확인한 `CacheToolsAvailable`(= `SystemCacheToolBackend.AnyToolInProtectedLocation()`: `%ProgramFiles%\nodejs\node.exe`+`npm-cli.js`, `%ProgramFiles%\dotnet\dotnet.exe`의 `IsPlainPath` 존재 확인만, 프로세스 실행·PATH 탐색 없음)가 true일 때만 보이고 `CanOpenCacheTools`도 이 값으로 막는다. 회귀: `ActionAvailabilityTests`, `MainViewModelTests.OverviewCountsDoNowAndDoManually`·`OverviewCountsExcludeNonCandidatesAndCommunityCards`·`OverviewTilesRaisePropertyChangedAfterScan`·`CacheToolsRequireToolInProtectedLocation`, `MainWindowLayoutTests.SummaryTilesSplitDoNowAndDoManually`. 검증: Release 빌드 경고 0/오류 0, 기본 1205/1205. 남은 제한: 제안 3항(Windows 내장 정리 연결을 주 버튼으로)·4항(크기 옆 행동 가능성 라벨)은 이번 범위 밖. 현재 `AppCacheRule`은 Candidate를 만들지 않으므로 실제 PC에서 "바로 할 수 있는 것"은 항상 0(타일 숨김)이며, 정리 창 버튼이 타일 밖 별도 줄에 남는다. pip는 기존 탐색에 Program Files 표준 경로가 없어 보호 위치 판정에 기여하지 않는다. 구현자 자체 검증이며 독립 재검증을 대체하지 않는다.
- 대응 기록 (2026-09-27, SP4 Task 10 수정 라운드 1, Claude Opus 5.5 구현자, 커밋 `68003d9`): 위 "pip는 보호 위치 판정에 기여하지 않는다" 제한 해소. `AnyToolInProtectedLocation`이 실행 규칙(`LocateAsync`)과 같은 후보 목록 `CandidateExecutables`(세 보호 위치 표준 폴더 + PATH 절대 경로, WindowsApps 제외)와 같은 판정(`CachePathInspector.IsProtectedProgramLocation` + `IsPlainPath`, npm은 진입 파일 포함)을 쓴다. 프로세스 실행은 여전히 없다(PATH 문자열을 읽고 존재만 확인). 회귀: `ActionAvailabilityTests.ProtectedLocationCheckMatchesExecutionRule`·`PythonOnlyInProgramFilesIsExposed`·`X86OnlyToolIsExposed`·`UserFolderToolsOnlyAreNotExposed`. 상태는 `수정됨·재검증 대기` 유지.

## 2026-09-27 독립 재검증 (Claude 세션, 커밋 `4c1e93d`, 원장 `6bbd4a8`)

컨트롤러 실행: Release 빌드 경고 0/오류 0, 기본 1169/1169, Smoke 22/22(ToolSmoke 미실행). 재리뷰는 diff·원장·1회 읽기 전용 명령(`dotnet nuget locals http-cache --list`를 ko-KR/ja-JP/en-US로 실행해 `http-cache:` 접두 확인)으로 수행.

- REV-008·009·011·012·013 → `검증 완료`. 관문 순서·범위는 이전 `Inspect`와 동등 이상(예산 비교 `>`→`>=`, 종료 시 예산 검사 추가), 계획 수명·허용 목록·승격·지문 재검사 불변.
- REV-010 → `부분 수정·재현 미완`: `CacheToolProcess.cs:78` `process.Kill(entireProcessTree: true)`는 실패 시 **AggregateException**을 던지는데 `CacheProcessGuard.cs:42,51`은 InvalidOperationException/Win32Exception만 잡는다(테스트 람다는 Win32Exception을 직접 던져 통과). 결과: `_hasExited`/`_dispose` 선설정 후 StopAsync 예외 → `CacheToolProcess.cs:85` 필터 불일치 → `:92`에서 프로세스 Dispose → 이후 `CanRun`의 `HasExited`가 InvalidOperationException → 앱 재시작까지 ProcessStillRunning(영구 차단), 그리고 진행 중이던 실행은 `CacheCleanupService.cs:104-108`에서 `execution == null`로 PreflightFailed/Started=false → "정리를 실행하지 않았습니다"(도구는 최대 2분 실행됨). 완료 근거: AggregateException(내부 Win32Exception)을 던지는 가짜 Kill로 (a) 가드가 차단 유지·프로세스 미Dispose, (b) 해당 실행이 Started=true·ToolFailed/ProcessStillRunning으로 보고되는 테스트.
- REV-014 → `부분 수정·재현 미완`: `MainViewModel.Overview.cs:80` `OnlineComparisonComplete`가 Driver 분류의 모든 CannotVerify를 미완료로 취급. `GpuVendorLinkRule.cs:98-106`(AMD/Intel 항상 Unsupported), `OemSupportRule.cs:76,95,104`, `SystemInfoRule.cs:60`, `InstalledDriverRule.cs:79`, `ProbeResultConverter`의 InstalledGpu/SystemInfo Skipped/Failed/Partial이 모두 해당 → Intel iGPU·AMD GPU·OEM 미확인 PC에서는 NVIDIA·WUA가 성공해도 항상 "온라인 확인 불가"이고 마지막 온라인 확인 시각이 표시되지 않는다. 테스트 `DriverTileRequiresActualSuccessfulOnlineResults`는 GPU/SystemInfo 프로브 없는 fixture라 통과. 완료 근거: 온라인 규칙(DriverUpdateRule, WindowsUpdateDriverRule)과 두 온라인 프로브의 상태만으로 완료 판정, Intel iGPU + NVIDIA 성공 fixture에서 "비교 완료" 표시 테스트.
- 새 Minor(작업 목록 D에 추가): ObservationFailed 코드가 `Cleanup_Blocked` 문구로 표시(실행됐는데 "허용되지 않음"), 타임아웃 후 가드 보존 시 Started=true 실행에 "이전 도구가 아직…" 문구, CleanupStarted 로그가 실행 후 CleanupExecuted로만 남아 중간 크래시 시 무기록, 보호 루트/프로필 이름에 `~` 포함 시 전부 InspectionFailed(fail-safe), LocateAsync에서 CanonicalPath ArgumentException이 다음 후보 탐색 중단, `MainViewModel.cs:45-46` `_showCommunityDetails` summary 없음, XAML 헤더 들여쓰기 불일치, 8.3 별칭 네이티브 스모크가 이 볼륨에서 별칭을 실제로 만들지 못함.

## 독립 검증 기록

### P5 독립 확인 (2026-09-26, Codex)

- 기준 `96a593c` 별도 `git archive` 복사본: Release 빌드 경고 0/오류 0, 기본 803/803 통과. 포함 규칙·보호 정책 리소스 사용, Steam 설치 경로 단일 값 조회, Squirrel 별도 목록, Section 범주 제외 코드를 확인했다. 테스트 통과를 전체 실제 UI·모든 환경의 검증으로 확대하지 않는다.
- 같은 복사본에서 `dotnet test PcOptimizer.sln --configuration Release --no-build --no-restore --filter "Category=Smoke"` 실행: **18/18 통과**, 약 47초. 실제 조회·테스트용 임시 링크 검증이며 사용자 설정 변경·삭제·온라인 요청은 없다. 기본 803개와 Smoke 18개 통과는 추가 경계 테스트 2개 실패와 별도로 기록한다.
- 추가 가짜 환경 테스트 2개는 **2/2 실패**. 원본 작업 소스에는 추가하지 않고 `docs/reviews/repro/ConfigReadBoundaryReviewTests.cs`로 공유했다. 복사본의 `tests/PcOptimizer.Tests/Unit/Probes/`에 편입 후 `dotnet test PcOptimizer.sln --configuration Release --no-restore --filter "FullyQualifiedName~ConfigReadBoundaryReviewTests"`로 재현한다.
- P5 3차 보고서는 최종 앱 캐시 카드가 106장이라고 기록한다. 119→97은 2차 수치이며 Games/Adobe 과병합을 푼 뒤의 최종 수치와 구분한다. 카드가 많다는 이유만으로 검사 결과를 숨기지 말고 P7에서 요약·필터·정렬 사용성을 확인한다. 실제 WPF 조작 검증은 이번 검토에서 하지 않았다.

### P5 다음 리뷰 인계 — 구현 세션 보고, 독립 검증 전

사용자가 전달한 구현 세션의 리뷰 결과(2026-09-26): 무결성 검사 기준 자기참조, 이동된 캐시 미탐지, 다른 사용자 경로 가드 오판, 197장 카드 노출, 비캐시 규칙의 캐시 제목, 정션 추종, Squirrel 삭제형 FileKey 표현, Steam 레지스트리 전체 값 읽기 등 8건을 P5 fix round 1에서 수정 중이다. 이는 Codex가 8건 모두 독립 재현했다는 뜻이 아니다.

현재 관측: Steam 리더의 작업본은 설치 경로 단일 값 조회로 바뀌고 있으나 아직 미커밋이며, 이번 P4 검증에는 넣지 않았다. P5 수정 완료 후 리뷰 패키지에 8건 각각의 변경 파일/커밋·재현/회귀 검증을 포함해 주세요. 단위 테스트 수 증가나 diff 승인만으로 정션 미추종·민감 값 미수집·카드 사용성 전체를 확인했다고 처리하지 않습니다. 진행 중인 소스는 이 검토 세션에서 수정하지 않았습니다.

- P5 fix round 1 구현 보고(2026-09-26, 구현 세션): 커밋 `99c1804`. 8건별 변경 파일·RED/GREEN 테스트·남은 한계 표는 `.superpowers/sdd/2026-09-26-pc-optimizer-implementation-plan/task-P5-report.md` 10장. 구현 세션 자체 실행 결과(빌드 0/0, 기본 782/782, Smoke 18/18)이며 독립 검증이 아니다. REV 상태 변경 없음.
- P5 fix round 2 구현 보고(2026-09-26, 구현 세션): 커밋 `aff581b`. Finding 4(Section= 기준 앱 카드)와 신규 결함 1~3의 변경 파일·RED/GREEN·남은 한계 표는 `.superpowers/sdd/2026-09-26-pc-optimizer-implementation-plan/task-P5-report.md` 11장. 구현 세션 자체 실행 결과(빌드 0/0, 기본 797/797, Smoke 18/18)이며 독립 검증이 아니다. REV 상태 변경 없음.
- 관련 수정(P4 코드, 신규 결함 3): `FileScanService`가 보호 정책 `rules\protect.json`을 출력 폴더(`AppContext.BaseDirectory`)에서 읽던 것을 Probes 어셈블리 포함 리소스로만 읽게 바꾸고 앱 출력 복사를 없앴다(`aff581b`). REV-001·REV-002 상태는 바꾸지 않았다.
- P5 fix round 3 구현 보고(2026-09-26, 구현 세션): 커밋 `18ea323`. 범주 Section(Games·Adobe)·공유 100개 초과 Section을 앱 묶음에서 제외(AppGroupResolver), protect.json 문구·측정 출처를 포함 리소스 이름으로, 공유 출력 폴더에 쓰던 테스트 제거. 표는 `.superpowers/sdd/2026-09-26-pc-optimizer-implementation-plan/task-P5-report.md` 12장. 구현 세션 자체 실행 결과(빌드 0/0, 기본 803/803, Smoke 18/18, 내보내기 `C:\Users\`·`VEN_` 0건)이며 독립 검증이 아니다. REV 상태 변경 없음.

### P6 인계 — 구현 세션 보고, 독립 검증 전

- P6 구현(2026-09-26, 구현 세션): 커밋 `8180165`. NVIDIA 온라인 조회 어댑터·계열 판정, Windows Update 드라이버 검색(검색 전용), 공식 링크 표(`rules/vendor-links.json`)·AMD/Intel/OEM 링크, App 링크 열기 정책. 보고서 `.superpowers/sdd/2026-09-26-pc-optimizer-implementation-plan/task-P6-report.md`(구현 파일·Studio 매개변수 실측·TDD·빌드/테스트·실제 조회/검색 결과·E2E·우려 12건).
- 구현 세션 자체 실행 결과이며 독립 검증이 아니다: Release 빌드 경고 0/오류 0, 기본 `Category!=Smoke&Category!=Online` 1085/1085, Smoke 18/18, Online 3/3(실제 NVIDIA 조회·WUA 검색 각 1회, 온라인 E2E 내보내기).
- 기본 테스트 필터가 `Category!=Smoke&Category!=Online`으로 바뀌었다(계획 문서 3장 갱신). Online 트레이트는 실제 네트워크·Windows Update에 연결하므로 명시적으로만 실행한다.
- P6을 대상으로 하는 REV 항목은 없다. 작업 중 검토자가 원장 작업본에 추가하던 REV-006/REV-007(P5 앱 설정 읽기, 이 커밋 시점 미커밋)은 이번 작업 범위가 아니며 REV 상태는 바꾸지 않았다.

### 실행 이력

- P4 4차 수정 재확인(기준 `1b9a9ad`): 빌드 0/0, 관련 회귀 37/37, 기본 761/761. Smoke 미실행. REV-002에 검증 범위와 마지막 조회 경계 보류를 기록했다.

- P4 최종 보고 재확인 (2026-09-26, Codex): `23f92e9` 별도 복사본 Release 빌드 경고 0/오류 0, 기본 659/659, Smoke 16/16(약 18초) 통과. 기본/Smoke는 추가 리뷰 테스트 편입 전에 실행했다. 이후 처리 단계 예산 재현 1개를 추가한 실행은 1/1 실패했다. 따라서 기존 테스트 수치의 재현과 추가 경계 조건의 미해결을 구분한다. P5 작업본은 이 검증에 포함하지 않았다.

- P3b: `0bce903`(구현 `a0ab605` 포함)을 `git archive`한 별도 임시 복사본에서 Release 빌드 경고 0/오류 0, 기본 523/523, Smoke 13/13 확인.
- 명령: `dotnet build PcOptimizer.sln --configuration Release`, `dotnet test PcOptimizer.sln --configuration Release --no-build --no-restore --filter "Category!=Smoke"`, 같은 명령의 `Category=Smoke` 필터.
- 실제 UAC 승인/취소 UI, 다른 계정 자격 증명 입력, 화면 배치 전체는 위 결과로 검증됐다고 간주하지 않는다.
- P4: 완료 승인 전 작업본을 별도 복사해 추가 재현 테스트 3개 실행, 3개 실패. 실제 개인 폴더를 스캔하거나 수정하지 않은 fake 환경 테스트다.
- 재현 파일은 `docs/reviews/repro`에 보관하므로 일반 테스트 빌드에 자동 포함되지 않는다. 검증용 복사본의 `tests/PcOptimizer.Tests/Unit/Probes/`에 복사 후 `dotnet test PcOptimizer.sln --configuration Release --filter "FullyQualifiedName~IndependentReviewTests"`로 실행한다. 수정 시 정식 회귀 테스트로 편입할 수 있다.

## 2026-09-27 인계 후 대응 기록 (Codex 구현·자체 검증)

아래 대응의 구현 커밋은 `89e2cbb`이다. 변경 파일 전체는 `git show --stat 89e2cbb`, 검증 및 남은 출시 조건은 `2026-09-27-validation.md`에서 확인한다. 이 커밋으로 다시 만든 최종 배포 EXE도 창 생성 및 정상 종료 코드 0을 확인했다.

- **REV-006**: `ConfigFileText`가 본문 요청 전에 상위 경로·파일의 reparse/placeholder/접근 실패를 검사한다. `FileSystemDirectoryEntrySource.ProbeRoot`는 파일 링크도 파일 유형보다 먼저 확인한다. `SystemPathEnvironment.ReadSmallTextFile`도 실제 모든 조상/파일을 확인하고 바이트 상한 안에서 읽는다. 가짜 공급자의 미등록 경로는 실제 읽기 공급자가 판정한다. 실제 공급자는 Missing을 읽지 않는다. Steam 설정의 Program Files 예외는 유지한다. 원장 재현을 정식 `ConfigReadBoundaryReviewTests`로 편입했다. 정상 파일/중간 링크/파일 링크/크기 초과 실제 fixture도 통과했다. 동시 악의적 경로 교체를 원자적으로 봉쇄하는 OS 핸들 기반 보안 경계까지 구현한 것은 아니다.
- **REV-007**: Missing만 부재로 보고 나머지 읽기 실패를 Unreadable로 유지한다. `SteamLibraryReader`도 실패를 무시하지 않는다. `AppCacheProbe`는 Unreadable 설정을 설치 탐지 근거로 쓰지 않으면서 최종 설정 사유 카드에는 남긴다. 기본 npm 캐시가 없는 가짜 PC 회귀 통과.
- **REV-002**: `VolumeTraversalRun.Run`이 루트 완료 뒤 경과 시간을 확인한다. 마지막 ID/할당 크기 조회에서 초과하면 루트·볼륨 TimedOut은 true지만 실제 누락이 없으면 Timeout skip/LookupsSkipped는 0으로 보존한다. 회귀 2개 통과. 요약 문구도 시간 예산 초과를 무조건 부분 관측이라고 하지 않도록 수정했다.
- **REV-003**: `ProbeExecutor` → `ScanCoordinator` → `ScanService` → `MainViewModel` 변경 이벤트를 추가했다. UI 디스패치 시점의 현재 목록으로 안내를 갱신하며 과거 Report 플래그와 합치지 않는다. 창 종료 시 구독 해제·검사 취소. 늦은 작업 완료 후 재검사 없이 안내가 없어지고 같은 리포트가 유지되는 회귀 통과. 종료 중에는 정리 도구도 열지 않는다.
- **REV-004**: 사용자 답변 ‘자동 조치도 1차에 포함’, 이어 ‘공식 도구의 캐시 정리부터: npm·pip·NuGet HTTP 캐시, Windows 정리 도구 연결’을 반영했다. `docs/superpowers/specs/2026-09-27-first-release-actions.md`가 기존 조회 전용 원칙의 명시적 예외다. 별도 도구 창에서 대상 확인→사용자 확인→공식 명령→재관측→재검사. 일반 권한·현재 프로필 아래·보호 경계만 자동 실행하며 이동한 외부 캐시는 수동 관리 안내. 커뮤니티 카드는 기본 요약+펼치기이고 자동 정리에는 사용하지 않는다. 후속 단계의 다른 자동 조치까지 승인받았다는 뜻은 아니다.

### P6 검토 및 이탈 4건 판단

- 원 구현 `6451f31..5231f28`의 네트워크 옵트인, 설치 정보 보존, NVIDIA/WUA 어댑터와 fixture, 공식 링크 흐름을 검토하고 기본·실제 온라인 테스트를 재실행했다.
- Studio 목록 실패 시 Ambiguous 유지: 반대 계열에 같은 설치 버전이 있는지 모르는 상태이므로 보수적 판정 유지.
- GPU 이름 정확 일치: 미매칭을 임의 제품으로 추측하지 않는 동작 유지. 노트북 매핑 제한은 알려진 한계다.
- NVIDIA 링크: App의 호스트만 허용하는 예외를 호스트+드라이버 경로로 좁혔다. 다른 경로 거절 회귀 추가. 검증된 `Uri.AbsoluteUri`로 여는 동작 유지.
- WUA 링크: `ms-settings:windowsupdate-optionalupdates`로 규칙·UI 허용 목록·회귀 테스트를 일치시켰다. 근거: https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings

### 추가 이월 항목 처리

- P3a의 드라이버 기본 주사율 0/1 오탐을 CannotVerify로 처리하고 회귀 2개 추가.
- 테스트용 `TestDirectory` 정리가 정션을 따라가던 위험을 제거: 루트 경계 검사 후 직접 항목만 순회하고 링크 자체만 제거한다.
- 예상 밖 검사 예외에서 UI Scanning 고착을 해제한다.
- 원장에 있던 나머지 minor를 일괄 해결했다고 표시하지 않는다. 특히 짧은 사용자명과 구조 문자열의 내보내기 과치환, 경로 토큰화 경계, OneDrive 계정 값 전체 읽기, 실제 비관리자/UAC·SDK 없는 PC·다중 DPI 조작 검증은 후속 검토 대상이다.

### 검증 근거

- Release 빌드: 경고 0/오류 0.
- 기본: `dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"` — **1113/1113**. `ToolSmoke`는 새로 추가한 선택형 실제 도구 테스트라 기본 필터에서 반드시 제외한다.
- 실환경: `--no-build --no-restore --filter "Category=Smoke"` — **21/21**(46초). 처음 샌드박스 안 실행은 권한 제한으로 9건 실패했고, 제한 밖 재실행은 모두 통과했다. 제한 환경 실패를 제품 회귀와 구분한다.
- 실제 온라인: `Category=Online` — **3/3**(36초), 검색만. WUA 설치·다운로드 없음.
- 실제 pip: `PCOPTIMIZER_TEST_PYTHON`에 기존 Python 3.12/pip 26.2.1을 지정하고 `Category=ToolSmoke` — **1/1**. Python은 테스트용 Codex 런타임이며 제품 의존성/자동 발견 경로로 넣지 않았다.
- npm·NuGet·pip 모두 소유한 임시 fixture 캐시만 정리했고 형제 프로젝트/설치 패키지 fixture 보존을 확인했다. 사용자 캐시 정리 없음.
- WPF 진단/정리 창 100·150·200% 렌더링 6개 이미지 확인. 실제 모니터 DPI 전환·키보드 전체 조작 검증은 아님.
- self-contained win-x64 배포 성공. 포함 런타임 10.0.12, 규칙 원본·출처·라이선스 포함. 배포 EXE 실제 창 생성, 정상 종료 코드 0 확인. 별도 .NET 미설치 PC 실행은 미검증.

동일 구현자가 수행한 수정 검증이므로 REV-002/003/006/007은 `수정됨·재검증 대기`로 유지한다. 상세 변경·출시 제한은 `docs/reviews/2026-09-27-validation.md`를 읽고 재검토한다.

## 2026-09-27 UI 대응 — 개선 효과와 다음 행동 우선

- 사용자 발견: 검사 정보는 있으나 이 앱으로 무엇을 개선할 수 있는지 눈에 들어오지 않는다.
- 변경: `MainViewModel.Overview.cs`, `MainViewModel.cs`, `FindingCardViewModel.cs`, `MainWindow.xaml`, `CacheToolsWindow.xaml`, 한국어 리소스. 기본 화면은 추천 조치, 상단은 공간 확보·설정 개선·드라이버 확인의 목적별 요약이다. 카드에는 기대 효과→현재 상태→권장 행동→조건/주의→실행 가능한 버튼을 표시하고 원시 측정값은 상세로 옮겼다. 동작하지 않는 적용/진단용 내보내기 버튼을 화면에서 제거했다.
- 정보 보존: 정상·참고·확인 불가·커뮤니티 카드는 전체 결과에서 접근하며 원본 리포트와 건수는 변경하지 않는다. Candidate만 추천 대상으로 삼고 커뮤니티 규칙과 캐시 Info를 승격하지 않는다. 관측 크기를 확보 용량으로 합산하지 않는다. 검사가 일부만 완료된 상태와 후보 0개를 정상 판정으로 바꾸지 않는다.
- 정리 창: 도구 선택→대상 조회→미리보기·영향→확인 후 정리 순서로 재배치. 실제 정리 엔진/확인 정책은 그대로다. Windows 저장소 연결은 별도 영역으로 표시한다.
- 검증: 기본 테스트 1114/1114 통과(`ui-overview-unit.trx`). 추천/전체 전환의 원본 보존, 커뮤니티·관측 캐시 비승격, 취소·0후보 안내를 검증했다. 기존 긴 경로 레이아웃 검사가 데이터 템플릿 내 줄바꿈 누락을 발견하여 명시적 Wrap으로 수정했다. 옵션을 펼친 뒤 관리자 버튼/배너 접근 검증도 통과했다.
- 렌더: 예시 개선 후보, 검사 전, 후보 없음, 720px 좁은 창, 정리 창을 100/150/200%로 생성(`artifacts/ui-overview/`). 화면의 120→130 Hz 등은 가짜 fixture이며 이 PC 실측 결과가 아니다. 실제 모니터 DPI 전환·전체 키보드 조작·독립 사용자 사용성 평가는 별도다.
- 리뷰 상태: 구현자 자체 검증. 기존 REV의 독립 재검증 대기 상태를 변경하지 않는다. 이 기록을 포함한 UI 커밋을 이전 구현 커밋 `89e2cbb`와 구분해 검토한다.
- 최종 배치 변경 후 레이아웃 7/7 재통과(`ui-overview-layout.trx`). 기존 `artifacts/win-x64`로 publish는 사용 중인 DLL 잠금으로 실패했으므로 앱을 강제 종료하지 않고 `artifacts/win-x64-ui`에 새로 publish했다. 새 EXE 창 생성·정상 종료 코드 0을 확인했다. 기존 실행 창에는 새 UI가 반영되지 않으므로 새 경로를 사용한다. 이 UI 변경에서는 실제 정리/온라인 검사를 다시 실행하지 않았다.

## 2026-09-27 UI 후속 대응 — 조치로 이동하고 결과 확인

- 기준 UI 커밋 `ad06cca` 이후 사용자 ‘진행해’에 따른 후속 개선. 설정 후보·드라이버 결과 버튼이 해당 목록을 거르고 포커스를 옮긴다. 원본 리포트는 보존한다. 온라인 미요청 검사에서 드라이버 후보가 없으면 ‘온라인 비교 전’으로 표시하며, 체크박스만 바꾸어도 조회한 것으로 표시하지 않는다.
- `CacheCleanupService` 결과에 실행 직전 재검증에서 읽은 `BeforeBytes`를 추가했다. `CleanupOutcomeViewModel`/`CleanupOutcomeView`는 이 값과 실행 직후 관측을 비교한다. 미리보기 때의 오래된 크기를 기준으로 쓰지 않는다. 도구 실패·후속 관측 불가·크기 동일·증가를 용량 확보 성공으로 표현하지 않는다. 논리 크기의 감소이지 디스크 여유 공간 증가량은 아니며, 동일 창 세션에서 가장 최근 실행 결과를 보여 준다.
- `CacheToolsViewModel` 확인 문구를 변경 가능한 Message 대신 발급된 계획에서 다시 구성한다. 대상 조회 후 Windows 설정 버튼을 눌러 Message가 바뀌어도 도구·경로·영향을 확인창에서 빠뜨리지 않는다. 취소 시 실행/재검사 요청 없음 회귀 유지.
- 기본 **1122/1122** (`ui-flow-unit-final.trx`), 확인창 포함 관련 회귀 **17/17** (`ui-flow-confirmation.trx`). 결과 비교·필터·리포트/최근 조치 보존·확인 취소는 가짜 실행기로 검증했다. 개인 캐시 정리는 실행하지 않았다.
- 실제 조회 스모크 **1/1**, 약 18초 (`ui-flow-actual.trx`): 실제 서비스→MainViewModel→목적별 필터→익명화 내보내기. 195개 결과 중 개선 후보 1개, 디스플레이 120→130 Hz 같은 해상도 후보를 화면에 표시했다. 일부 관측만 완료된 상태를 유지하며 전체 PC 정상 판정은 하지 않는다. 온라인 조회와 설정 변경 없음. 실행 권한은 테스트 프로세스 권한이며 일반 권한 전체 UI 조작 검증과 구분한다.
- 화면: `artifacts/ui-flow/actual-overview.png`는 이번 조회의 실측 결과를 오프스크린 WPF로 렌더링한 것이다. `cleanup-summary-*`의 정리 수치는 예시 fixture이며 실제 정리 결과가 아니다. 100/150/200% 렌더 회귀도 기본 테스트에 포함했다.
- 배포: 일반 대상 자산만 남아 첫 publish가 NETSDK1047로 실패했다. 이번 빌드 중 제거된 RID 잠금 항목을 기존 커밋 상태로 복원하고 `dotnet restore src/PcOptimizer.App/PcOptimizer.App.csproj -r win-x64 --locked-mode` 뒤 publish 성공. 패키지 버전/잠금 파일 변경 없음. 경로 `artifacts/win-x64-ui` 유지.
- 구현자 자체 검증이며 기존 REV 독립 검증 상태는 바꾸지 않는다. 이 기록을 포함한 커밋의 App ViewModels/Views, `CacheCleanupService`, 관련 App/Smoke 테스트를 후속 리뷰 대상으로 삼는다.


## 2026-09-27 REV-008~014 수정 인계 (Codex 구현자)

- 코드 커밋 `4c1e93d`, 독립 검토 범위 `5457807..4c1e93d`(소스/테스트만). 다른 세션의 `e2fddf2`, `5457807` 문서와 REV-015 제안은 보존했다.
- 우선 [수정·검증 기록](2026-09-27-rev008-014-fixes.md)을 읽고 각 REV의 재현 테스트와 실제 생산 경로 연결을 검토할 것. 자체 검증을 독립 승인으로 바꾸지 않는다.
- 평가 배포물은 `artifacts/win-x64-review-fixes/PcOptimizer.App.exe`. 실행 중인 이전 앱은 교체/종료하지 않았다. 폴더 전체를 함께 사용한다.
- 남은 범위: 이 수정분 독립 재검증, REV-015 제품 구성 결정, 기존 이월 minor, 일반 권한 실제 UI·UAC·다른 SID·DPI 전환·.NET 없는 별도 PC 검증.


## REV-016 — SystemOnly 사용자 범위가 정리 조치로 전달되지 않음

- 발견: 2026-09-27, Codex, `66cd7cb` 고정 복사본. Task 9 구현과 Task 10 활성화 경계 검토.
- 위치: `MainViewModel.cs:175,226`, `MainWindow.xaml.cs:43-46`, `CacheToolsWindow.xaml.cs:27`.
- 조건/실제: SystemOnly + 보호 위치 도구. `SystemOnlyMustNotOfferUserCacheActions`는 CanOpenCacheTools=false 기대, 실제 true. 새 정리 backend는 사용자 범위를 전달받지 않는다.
- 영향과 한계: 현재 NormalUserRequired가 실제 정리를 차단한다. **다른 사용자 데이터 삭제 재현이 아니라 Task 10 활성화 전 보완할 계약 결함**이다. 승격 거절만 제거하면 관리자 프로필과 대화형 사용자 프로필의 구분이 사라진다.
- 요청: 사용자별 정리의 UI·준비·실행 직전 관문에 범위 전달/재확인. SystemOnly 거절 Started=false, 동일 사용자 허용 회귀를 추가한다.
- 근거: [Task 9 리뷰](2026-09-27-sp4-task9-review.md), [실패 재현 소스](repro/Sp4Task9ReviewTests.cs).
- 대응 기록: 2026-09-27 Claude 컨트롤러 — SP4 Task 10 필수 선행 범위로 편입(UI `CanOpenCacheTools`와 `SystemCacheToolBackend(limitToSystemScope)` 양쪽 거절, 재현 테스트 이름 보존). 구현 커밋은 Task 10 완료 시 기록.
- 대응 기록 (2026-09-27, Claude Opus 5.5 구현자, SP4 Task 10): 상태 `수정됨·재검증 대기`.
  - 커밋 `0c80a8b`(관문, 승격 거절 제거 전): `MainViewModel.CanOpenCacheTools = CacheToolsAvailable && !IsScanning && !HasDrainingNote && !IsSystemOnly && !Spec.IsLoading`. `SystemCacheToolBackend(IAppLogger?, bool limitToSystemScope)` — true면 `LocateAsync`는 도구 탐색·파일 관측·프로세스 실행 전에 `CacheToolUnavailableException("UserScopeExcluded")`, `InspectAsync`는 열거 없이 `Allowed=false`, `ClearAsync`는 `Started=false`로 거절(npm·pip·NuGet 전부). `CacheToolsWindow(IAppLogger?, bool limitToSystemScope)`에 `MainWindow`가 `viewModel.IsSystemOnly` 전달. 문구 `Cleanup_UserScopeExcluded`.
  - 커밋 `34670c5`: 위 관문이 통과한 뒤 `NormalUserRequired` 승격 거절을 제거하고 보호 위치(Program Files 계열) 도구만 실행하도록 교체.
  - 테스트: `MainViewModelTests.SystemOnlyMustNotOfferUserCacheActions`(재현 소스 이름·단언 보존), `CacheCleanupTests.SystemOnlyBackendRefusesBeforeObservation`(npm/pip/NuGet 3건: 계획 없음·Inspect/Clear 거절·Started=false·가짜 파일 시스템 ProbeRoot/Enumerate 0회·탐색/실행/지문 0회), `SystemOnlyWindowShowsUserScopeExcluded`, `FullScopeBackendStillPreparesAndClears`(동일 사용자 허용 회귀). RED: 수정 전 SystemOnly에서 CanOpenCacheTools=true, 백엔드가 계획을 발급(`Assert.Null() Failure: Value is not null`).
  - 검증(`34670c5` 기준 작업 트리, Release): 빌드 경고 0·오류 0, 기본 필터 1225/1225, Smoke 22/22. Online/ToolSmoke 미실행. 앱 GUI·실제 다른 SID 승격 환경은 실행하지 않았다(단위 테스트 가짜 대역만).
  - 남은 제한: SystemOnly에서도 정리 버튼은 보이되 비활성(배너가 이유 설명). 독립 재검증 필요.
  - 수정 라운드 1 `68003d9`: 관리자 권한 도구를 사용자 쓰기 가능 작업 폴더(`%LOCALAPPDATA%\PcOptimizer\ToolWork`)에서 실행하던 경로는 `34670c5`(승격 거절 제거)에서 처음 열렸다가 같은 Task 10에서 닫혔다 — 작업 폴더를 System32로 고정하고 ToolWork 코드 삭제(`CacheCleanupTests.ToolsRunFromSystemDirectory` npm·pip·NuGet × 조회/정리 6건). 기본 1236/1236, Smoke 22/22.

## REV-017 — 사양 수집 타임아웃 이후 살아 있는 프로브 재실행

- 발견: 2026-09-27, Codex, `66cd7cb`. Task 7~8 연결부이며 Task 9에서 새로 도입된 코드라고 보지는 않는다.
- 위치: `PcSpecService.cs:172-197`, `MainViewModel.cs:403-414`.
- 재현: `TimedOutSpecMustNotReenterLiveSharedProbe`. 취소를 무시하는 40ms 제한 프로브로 CaptureAsync 두 번. 첫 Task가 살아 있는데 호출 수 Expected 1 / Actual 2.
- 실제: WaitAsync 타임아웃 뒤 내부 Task/종료 중 상태를 보존하지 않는다. 사양 새로 고침과 공유 인스턴스를 쓰는 일반 검사가 다시 실행될 수 있다. 일반 검사 종료 중→사양의 반대 방향도 별도 확인할 것.
- 요청: 두 경로의 실제 실행/종료 상태를 공유하고, 타임아웃과 실제 종료를 분리한다. 재진입 차단·늦은 종료 후 복구를 양방향으로 검증한다.
- 근거: [Task 9 리뷰](2026-09-27-sp4-task9-review.md), [실패 재현 소스](repro/Sp4Task9ReviewTests.cs).
- 대응 기록: 2026-09-27 Claude 컨트롤러 — SP4 신규 Task 13(실행 수명 공유·종료 견고성)에 REV-010 잔여와 함께 배정. 실행 순서 10 → 13 → 11 → 12.
- 대응 기록 (2026-09-27, Claude Opus 5.5 구현자, SP4 Task 13): 상태 `수정됨·재검증 대기`.
  - 커밋 `3d286be`. `PcSpecService`: `probe.RunAsync` Task를 보존하고, 대기(타임아웃·사용자 취소·예외)가 끝났는데 Task가 살아 있으면 `ConcurrentDictionary<string, Task>`에 probeId로 보관(`SpecProbeLive`), 완료 시 제거(`SpecProbeDrained`, 늦은 실패는 `SpecProbeLateFailure` 형식 이름만 — 예외 관측). `CaptureAsync`는 보관 Task가 끝나기 전 같은 프로브를 호출하지 않고 `SpecProbeStillRunning probe={id}` 경고 후 건너뜀(해당 섹션은 확인 불가). `HasLiveProbes`, `WaitForDrainAsync(CancellationToken)`(던지지 않고 완료만 기다림, 취소 가능) 추가. 기존 타임아웃 값(`IProbe.DefaultTimeout`) 재사용, 새 대기 상수 없음.
  - 사양→검사 방향: `PcSpecViewModel.IsDraining` — Refresh 뒤 `HasLiveProbes`면 UI 디스패처에서 IsLoading 해제 전에 true, 별도 대기(`WaitForDrainAsync`, UI 스레드 비차단) 완료 시 디스패처로 false. `CanRefresh`·`RefreshAsync` 첫 줄 관문에 `!IsDraining`. `MainViewModel.CanStartScan`·`CanOpenCacheTools`에 `!Spec.IsDraining`, IsDraining 변경 시 두 값 알림. 화면 안내 `Spec_DrainingNote`("이전 사양 읽기가 끝나기를 기다리는 중이에요.", `PcSpecView.xaml`).
  - 검사→사양 방향: `MainViewModel.SyncSpecBusy()`가 `Spec.SetBusy(IsScanning || HasDrainingNote)` — `OnStateChanged`와 `OnDrainingNoteChanged`에서 호출. 조율기 자체의 종료 중 의미는 변경하지 않음.
  - 테스트: `PcSpecTests.TimedOutSpecMustNotReenterLiveSharedProbe`(재현 소스 이름·단언 보존), `LiveProbeIsSkippedUntilItFinishesThenRunsAgain`(건너뜀 경고·종료 후 재호출), `WaitForDrainIsCancellableAndCompletesWhenNothingLive`, `ViewModelKeepsDrainingUntilLiveProbeFinishes`, `MainViewModelTests.사양_종료_대기_중에는_검사와_정리를_시작하지_않는다`(차단 후 늦은 종료 시 복구), `검사_종료_대기_중에는_사양_새로_고침을_하지_않는다`(차단 후 늦은 종료 시 복구). RED: 수정 전 `Assert.Equal() Failure: Expected: 1 Actual: 2`(원장 기록과 동일), MainViewModel 두 건은 수정 전 게이트로 `Assert.False() Failure … Actual: True`.
  - 검증(Release, `3d286be` 작업 트리): 빌드 경고 0·오류 0, 기본 필터 1245/1245, Smoke 22/22, 대상 3개 클래스 61/61 5회 반복 통과. Online/ToolSmoke·앱 GUI 미실행.
  - 남은 제한: 끝나지 않는 프로브는 영원히 IsDraining=true로 남아 검사·정리·사양 새로 고침이 앱 재시작까지 막힌다(HasDrainingNote와 같은 의미, 컨트롤러 결정). 검사 종료 중 사양 화면에는 기존 `Spec_BusyScanning`("검사 중에는…") 문구가 보인다. 검사 측이 사양 측 보관 Task를 직접 보지는 않고 UI 관문(`CanStartScan`)으로만 막는다 — UI 밖에서 `ScanService`를 직접 부르는 경로는 없음(App.xaml.cs 확인). 동시 `CaptureAsync` 두 개가 같은 프로브를 동시에 보관하는 경합은 뷰모델의 IsLoading 관문으로만 배제된다. 독립 재검증 필요.
  - 수정 라운드 1 (Task 13 리뷰 발견 1·2·4·6): 결과 화면 개요 카드에 `MainViewModel.HasSpecDrainingNote`(=`Spec.IsDraining`, 변경 알림) 바인딩 `Spec_DrainingNote` 안내(`SpecDrainingNoteMain`) — 검사 시작·정리 버튼 비활성 사유 표시. `TrackIfLive`가 대기 직후 이미 실패로 끝난 Task의 예외도 관측(`SpecProbeLateFailure`). 테스트 `MainWindowLayoutTests.SpecDrainingNoteShowsOnResultsOverview`(RED: XAML 원복 시 `Assert.Single() Failure`), `MainViewModelTests.SpecDrainingNotifiesScanGateAndMainNote`(IsDraining 전이마다 StartScan CanExecuteChanged false→true·HasSpecDrainingNote 알림), 기존 `사양_종료_대기_중에는_…`에 HasSpecDrainingNote 단언. 기본 1247/1247, Smoke 22/22, 빌드 0/0. 리뷰 발견 3(검사 종료 중 사양 화면 문구)·5(서비스 계층 상호 배제)는 이월. 상태 유지 `수정됨·재검증 대기`.
- 대응 기록 (2026-09-27, Claude Opus 5.5 구현자, SP4 최종 리뷰 후속 필수 2): 상태 유지 `수정됨·재검증 대기`.
  - 최종 리뷰 발견: 실제 사양 프로브 8개는 모두 동기 WMI 조회 뒤 `Task.FromResult`를 돌려주므로 `PcSpecService.RunProbeAsync`가 `probe.RunAsync`를 호출 스레드(UI)에서 끝까지 실행했다. **이 수정 전에는 위 타임아웃·살아 있는 실행 추적(`TrackIfLive`·`HasLiveProbes`·`IsDraining`)이 실제 프로브에는 한 번도 적용되지 않았다**(기존 테스트는 비동기로 멈추는 대역만 써서 통과). WMI가 멈추면 앱 전체가 응답하지 않을 수 있었다.
  - 커밋 `ec3547e`: `running = Task.Run(() => probe.RunAsync(context, timeout.Token), CancellationToken.None);`(`ProbeExecutor`와 같은 방식). 사양 섹션 순서·타임아웃 값은 변경 없음.
  - 테스트: `PcSpecTests.SynchronouslyBlockingProbeTimesOutAndStaysTracked`(Core의 동기 차단 대역 `BlockingProbe`로 CaptureAsync가 프로브 타임아웃 뒤 반환하고 `HasLiveProbes == true`, 풀면 종료 대기 완료), `ViewModelRefreshExecuteReturnsWhileProbeBlocks`(`RefreshCommand.Execute`가 프로브가 막고 있는 동안 곧바로 반환). 두 테스트 모두 수정 전 코드가 영구히 멈추지 않도록 3초 뒤 대역을 푸는 안전장치를 두고, 안전장치가 풀기 전에 반환해야 통과한다. RED(수정 전): `동기 차단 프로브가 CaptureAsync 호출 스레드를 막았다`, `새로 고침 Execute가 동기 차단 프로브에 막혔다`(각 3초). `MainWindowLayoutTests.SpecToggleSwapsMainContent`는 사양 읽기가 동기로 끝난다는 가정에 의존해 창을 만들기 전에 사양을 한 번 읽어 두도록 고쳤다(전환은 여전히 창이 있는 상태에서 바인딩으로 확인).
  - 검증: 최종 리뷰 후속 절 참조. 실제 WMI가 멈추는 PC에서의 재현은 하지 않았다. 독립 재검증 필요.

## REV-018 — 사양 읽기 중 정리 창 진입 가드 누락

- 발견: 2026-09-27, Codex, `66cd7cb`. Task 8 보고서의 가드 반영 주장과 실제 조건 불일치.
- 위치: `MainViewModel.cs:223-226,421-426`.
- 재현: `SpecLoadingMustBlockCacheActions`. IsLoading=true와 StartScan.CanExecute=false를 확인했지만 CanOpenCacheTools=true라 실패.
- 요청: 사양 읽기/종료 중 상태를 실행 가능 조건에 반영하고 CanOpenCacheTools 변경 알림을 연결한다. 실제 정리가 현재 차단돼 있다는 사실과 별개로 Task 10 활성화 전 처리한다. REV-017과 함께 실제 수명을 기준으로 판단한다.
- 근거: [Task 9 리뷰](2026-09-27-sp4-task9-review.md), [실패 재현 소스](repro/Sp4Task9ReviewTests.cs).
- 대응 기록: 2026-09-27 Claude 컨트롤러 — `IsLoading` 관문은 Task 10(a)에서, 종료 대기(`IsDraining`) 관문은 Task 13에서 처리.
- 대응 기록 (2026-09-27, Claude Opus 5.5 구현자, SP4 Task 10 (a)): 상태 `수정됨·재검증 대기`(IsLoading 범위만).
  - 커밋 `0c80a8b`: `CanOpenCacheTools`에 `!Spec.IsLoading` 추가, `OnSpecPropertyChanged`에서 `IsLoading` 변경 시 `StartScanCommand.NotifyCanExecuteChanged()`와 함께 `OnPropertyChanged(nameof(CanOpenCacheTools))`. 정리 창은 명령이 아니라 Click 처리기(`MainWindow.OpenCacheTools`)와 `IsEnabled` 바인딩이라 `OpenCacheToolsCommand`는 없다.
  - 테스트: `MainViewModelTests.SpecLoadingMustBlockCacheActions`(재현 소스 이름·단언 보존), `SpecLoadingNotifiesCacheToolsGate`(읽기 시작·종료 시 CanOpenCacheTools 변경 알림 false→true). RED: 수정 전 `Assert.False() Failure … Actual: True`, 알림 목록 `[]`.
  - 검증: REV-016 기록과 같음(빌드 0/0, 기본 1225/1225, Smoke 22/22).
  - 남은 제한: 타임아웃 뒤 살아 있는 사양 프로브의 실제 종료 대기(IsDraining)는 이 관문에 없다 — REV-017과 함께 Task 13.
  - 후속 (2026-09-27, SP4 Task 13 `3d286be`): `CanOpenCacheTools`·`CanStartScan`에 `!Spec.IsDraining` 추가(REV-017 기록 참조). 상태는 그대로 `수정됨·재검증 대기`.

## 2026-09-27 Task 9 시점 재검증 (Codex)

- 기준 `66cd7cb`, Task 9 `c9ba0d7`. 소스 수정 없이 git archive 복사본에서 Release 0경고/0오류, 기존 기본 1196/1196, Smoke 22/22. 추가 재현 4건 모두 실패하여 REV-016~018 등록 및 기존 REV-010 잔여를 확인했다. Online/ToolSmoke는 미실행.
- REV-010 추가 근거: `AggregateKillFailureMustNotEscapeGuard`에서 AggregateException이 CacheProcessGuard.StopAsync 밖으로 전파됨. 원장의 이전 정적 지적을 실행 확인했으며 `부분 수정·잔여 재현 확인`으로 기록한다. Codex의 이전 구현에 대한 자기 재현이므로 독립 승인으로 취급하지 않는다.
- 상세 결과/명령/한계: [Task 9 리뷰](2026-09-27-sp4-task9-review.md). Task 10 실행 허용 전에 REV-016/018 및 기존 REV-010을 브리프에 반영하고, REV-017의 사양·검사 공통 실행 수명을 추가 설계에 포함할 것.
- 검토 도중 `1463afe`의 HANDOFF 정정을 확인했다. 원본 인계서와 구현 소스는 변경하지 않았다. 원래 요청의 windows 경로 대신 Task 9가 존재하는 pc_optimizer를 검토했음을 보고서에 명시했다.
- 마감 전 `512a82e`도 diff로 확인했다. 네 재현 경로는 변경되지 않았다. 위 테스트 수치는 `66cd7cb` 복사본 기준이며 최신 HEAD 전체 검증 수치가 아니다.

## 2026-09-27 SP4 구현(구현 세션 자체 검증) — Task 11 마무리

- 실행 순서 10 → 13 → 11 → 12. 이 절은 Task 11(문서·원장·검증 마무리)의 구현자 자체 검증이며 독립 재검증이 아니다.
- 커밋: Task 1~8은 `1d24cbf`에서 종료. Task 9 `c9ba0d7`+`512a82e`. Task 10 `0c80a8b`, `34670c5`, `7e39311`, `68003d9`, `f432d5a`. Task 13 `3d286be`, `2d140d7`, `be6e99a`.
- 검증(작업 트리, Release, 기준 커밋 `be6e99a`):
  - `dotnet build PcOptimizer.sln --configuration Release` — 경고 0, 오류 0.
  - `dotnet test PcOptimizer.sln --configuration Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"` — 통과 1247, 실패 0.
  - `dotnet test PcOptimizer.sln --configuration Release --no-build --filter "Category=Smoke"` — 통과 22, 실패 0(약 38초, 실제 PC 조회만, 파일 변경 없음).
  - `dotnet publish src/PcOptimizer.App/PcOptimizer.App.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/win-x64-sp4` — 배포 폴더 생성 확인(git-ignored).
  - 배포 EXE 실행(이미 관리자 권한인 셸이라 UAC 프롬프트는 뜨지 않음): `Start-Process`로 2회 실행, 창 제목 "PC 최적화 진단" 생성 확인, `Process.CloseMainWindow()`로 정상 종료, 두 실행 모두 `ExitCode=0`. `%LocalAppData%\PcOptimizer\logs\pcoptimizer-20260926.log`(파일명은 UTC 날짜, 로컬 시각은 09-27)에 `AppStarted elevated=True scope=Full sessionUserResolved=True` 2건 확인. 일반(비관리자) 셸에서의 UAC 동의 절차는 이 방법으로 확인할 수 없어 수동 검증으로 남긴다.
- REV-014·REV-015(Task 5 대응, 커밋 `e58cae9`/`68003d9`)를 위 최종 수치로 재확인했다. 상태는 계속 `수정됨·재검증 대기`.
- REV-016(SystemOnly 사용자 범위 미전달, Task 10 커밋 `0c80a8b`·`34670c5`·`68003d9`), REV-017(사양 수집 타임아웃 뒤 살아 있는 프로브 재실행, Task 13 커밋 `3d286be`), REV-018(사양 읽기 중 정리 창 진입 가드 누락, Task 10 커밋 `0c80a8b` + Task 13 `3d286be`)을 위 최종 수치로 재확인했다. 각 REV 절의 대응 기록에 이미 기록된 테스트를 재확인 대상으로 삼는다: `MainViewModelTests.SystemOnlyMustNotOfferUserCacheActions`, `CacheCleanupTests.SystemOnlyBackendRefusesBeforeObservation`, `SystemOnlyWindowShowsUserScopeExcluded`, `FullScopeBackendStillPreparesAndClears`, `CacheCleanupTests.ToolsRunFromSystemDirectory`, `PcSpecTests.TimedOutSpecMustNotReenterLiveSharedProbe`, `LiveProbeIsSkippedUntilItFinishesThenRunsAgain`, `WaitForDrainIsCancellableAndCompletesWhenNothingLive`, `ViewModelKeepsDrainingUntilLiveProbeFinishes`, `MainViewModelTests.SpecLoadingMustBlockCacheActions`, `SpecLoadingNotifiesCacheToolsGate`, `사양_종료_대기_중에는_검사와_정리를_시작하지_않는다`, `검사_종료_대기_중에는_사양_새로_고침을_하지_않는다`. 상태는 모두 `수정됨·재검증 대기`.
- 남은 한계(이월 minor 편입, 컨트롤러 룰링, 코드 변경 없음):
  - npm이 작업 폴더 위로 탐색해 `C:\node_modules`를 프로젝트 루트(localPrefix)로 잡을 가능성은 남아 있다. 그 루트의 설정 파일(`C:\.npmrc`)은 일반 사용자가 만들 수 없는 파일 경로라 설정 주입으로 이어지지 않는다고 판단해 저위험으로 룰링한다(정적 판단, 실행 확인은 하지 않음. 근거: `task-10-fix1-report.md`).
  - `NUGET_`·`NPM_CONFIG_`·`PIP_` 접두 환경 변수를 자식 프로세스에서 지우는 심층 방어는 적용하지 않는다. 제거하면 사용자가 환경 변수로 옮긴 캐시 위치(`NUGET_HTTP_CACHE_PATH`, `npm_config_cache` 등) 조회가 AppCacheProbe 진단과 어긋날 수 있어 현재 값을 유지하기로 룰링한다.
  - pip 캐시 정리의 실제 프로세스 실행 스모크(`Category=ToolSmoke`)는 이번 라운드에 실행하지 않았다(테스트용 Python 경로 지정이 필요해 기본 검증 범위 밖). `Category=Online`도 재실행하지 않았다.
  - 일반(비관리자) 셸에서의 UAC 프롬프트, 표준 계정이 다른 관리자 자격 증명으로 승격했을 때의 실제 SystemOnly 배너·정리 버튼 비활성화, Program Files에 Python만 있는 PC에서의 정리 버튼 노출, .NET 미설치 PC, 모니터 DPI·키보드 전환은 여전히 수동 검증 대기다.
- 이 절은 구현 세션 자체 검증이다. 독립 검토자(Codex)가 별도로 확인하기 전에는 위 REV 항목을 `검증 완료`로 바꾸지 않는다.

## 2026-09-27 최종 리뷰 후속 (Claude Opus 5.5 구현자, 구현 세션 자체 검증)

- 근거: `.superpowers/sdd/2026-09-27-sp4-format-and-admin/final-review.md`(범위 `e1c965c..888305b`)의 "병합 전 필수" 1~3과 컨트롤러가 이번 라운드에 편입한 "이월 가능" 4·5. 커밋 `ec3547e`(코드·테스트·스펙 §0). 이 절은 구현자 자체 검증이며 독립 재검증이 아니다.
  1. [Critical] 공식 링크가 관리자 권한 브라우저로 열림: `LinkPolicy`가 `UseShellExecute` 대신 `UnelevatedShellLauncher`로 Windows 폴더 `explorer.exe`(셸 없이, 작업 폴더 Windows)에 검증한 `AbsoluteUri`를 인자 하나로만 넘긴다. 시작 실패 시 카드에 `Link_OpenFailedCopyFormat`(주소 포함, 선택 가능한 읽기 전용 글상자). `SettingsUriPolicy`(`ms-settings:`, 패키지 앱 활성화)는 브라우저를 띄우지 않아 변경하지 않았다. 테스트 `LinkPolicyTests.기본_실행기는_비승격_셸에_검증한_URL만_넘긴다`·`거부한_링크는_셸을_시작하지_않는다`·`비승격_셸_시작_실패는_Failed다`, `FindingCardLinkTests.실행_실패는_안내한다`. RED: 새 형식 없음으로 컴파일 실패.
  2. [Important] 사양 수집 UI 스레드 동기 실행: REV-017 대응 기록 참조.
  3. [Important] 검사 전 "직접 해야 하는 것 0건": 요약 타일 묶음(`SummaryTiles`)을 `HasCards`(=`LastResult is not null`)일 때만 표시. 테스트 `MainWindowLayoutTests.SummaryTilesAreHiddenBeforeScanAndShownAfterResult`. RED: `Assert.Equal() Failure: Values differ`(검사 전 Visible).
  4. [Honesty] SID 확인 불가 시 사실과 다른 배너: `UserScopeResolver.IsUnresolved`(어느 SID든 null)를 `App.xaml.cs`에서 `MainViewModel`(선택 인자 `userScopeUnresolved`)로 넘겨 `Banner_ScopeUnknown`을 표시. 알려진 경우의 `Resolve` 판정표는 변경 없음. 테스트 `UserScopeResolverTests.DetectsUnresolvedScope`, `MainViewModelTests.UnresolvedSystemOnlyShowsScopeUnknownBanner`·`FullScopeIgnoresUnresolvedFlag`, `MainWindowLayoutTests.사용자_확인_불가면_확인_불가_배너가_보인다`. RED: 컴파일 실패. 셸 토큰 SID 대체 수단과 Entra 실기 확인은 하지 않았다(이월).
  5. [Defense] (a) `PcOptimizer.App.csproj`에 `StartupHookSupport=false`(빌드 산출 runtimeconfig에 `System.StartupHookProvider.IsSupported: false` 확인), `PackagingTests` 단언 추가. (b) `CacheToolProcess.CreateStartInfo`가 상수 `INJECTION_ENVIRONMENT_VARIABLES`(DOTNET_STARTUP_HOOKS·DOTNET_ADDITIONAL_DEPS·DOTNET_SHARED_STORE·NODE_OPTIONS·PYTHONSTARTUP·PYTHONPATH·PYTHONHOME)와 접두사 `STRIPPED_ENVIRONMENT_PREFIXES`(기존 DOTNET_·CORECLR_·COR_·NODE_·PYTHON에 COMPlus_ 추가)를 지운다. NUGET_*·NPM_CONFIG_*·PIP_*는 컨트롤러 판정대로 유지. 테스트 `CacheToolEnvironmentTests.InjectionCapableVariablesAreRemovedAndOthersKept`(현재 프로세스에 심어도 빠지고 무관한 변수·캐시 위치 변수는 남음, 병렬 비활성 컬렉션). RED: `COMPlus_EnableDiagnostics가 남아 있다`, `StartupHookSupport` 문자열 불일치.
- 검증(Release, 작업 트리 = `ec3547e`):
  - `dotnet build PcOptimizer.sln --configuration Release` — 경고 0, 오류 0.
  - `dotnet test PcOptimizer.sln --configuration Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"` — 통과 1279, 실패 0.
  - `dotnet test PcOptimizer.sln --configuration Release --no-build --filter "Category=Smoke"` — 통과 22, 실패 0.
  - Online/ToolSmoke와 앱 GUI 실행은 하지 않았다. 실제 브라우저가 비승격으로 열리는지, 링크 실패 안내 글상자의 모양은 실기 확인하지 못했다.
- 관련 REV: REV-017 상태 유지 `수정됨·재검증 대기`(위 절 기록). REV-015(요약 타일)는 검사 전 숨김이 추가됐을 뿐 상태 변경 없음. `검증 완료`는 독립 검토자만 기록한다.

## 2026-09-27 SP4 최종 독립 재검증 (Codex)

- 기준 `0fa2bf5`(마지막 제품 코드 `ec3547e`). 보고서: [SP4 최종 독립 재검증](2026-09-27-sp4-final-revalidation.md). 새 병합 차단 코드 결함은 검토 범위에서 발견하지 않았다. 병합·게시·개인 캐시 정리 없음.
- 고정 git archive 복사본: Release 경고 0/오류 0, 기본 **1279/1279**, Smoke **22/22**, ToolSmoke **1/1**. 이전 `Sp4Task9ReviewTests.cs` 원문을 추가해 재현 **4/4 통과**. 명령·TRX 위치·복원 제한·ZIP SHA-256은 보고서에 기록했다.
- REV-010: `3d286be`의 AggregateException 처리와 정식 실패·로그·늦은 종료 복구 회귀, 원본 AggregateKillFailureMustNotEscapeGuard를 확인해 잔여 예외 경로 `검증 완료`. 실제 죽일 수 없는 프로세스는 만들지 않았다.
- REV-014: SP4 Task 5의 타일 제거·온라인 완료 판정 분리와 요청만으로 완료 표시하지 않는 회귀를 확인해 `검증 완료(SP4 표시)`. 실제 Online은 이번 미실행.
- REV-015: 채택된 1·2항과 `ec3547e`의 검사 전 타일 숨김을 소스·레이아웃 회귀로 확인해 `검증 완료(SP4 1·2항)`. 제안 3·4항과 실제 초보자 사용성은 후속 범위 유지.
- REV-016: `0c80a8b`·`34670c5`의 UI→창→backend 범위 전달, 3종 Locate/Inspect/Clear의 관측 전 거절과 Full 허용을 확인. 원본 SystemOnlyMustNotOfferUserCacheActions 통과. `검증 완료(UI·backend)`이며 실제 표준 계정 UAC는 수동 대기.
- REV-017: `3d286be`·`be6e99a`·`ec3547e`의 Task.Run·실제 Task 추적·양방향 UI 차단·늦은 종료 복구·동기 BlockingProbe 테스트를 확인. 원본 TimedOutSpecMustNotReenterLiveSharedProbe 통과. `검증 완료(현재 UI 경로)`. 서비스 직접 동시 호출의 공통 직렬화는 이전 이월 그대로이며 완료 주장에 포함하지 않는다.
- REV-018: `0c80a8b`·`3d286be`의 IsLoading/IsDraining 정리 진입 조건과 알림 회귀, 원본 SpecLoadingMustBlockCacheActions 통과. `검증 완료(UI 경계)`.
- 남은 검증: 실제 브라우저 토큰·UAC/다른 계정·.NET 없는 PC·DPI·SmartScreen. explorer 셸 미실행/쉼표 URL, 제3자 고지 원문, 도구별 실행 가능 판정, 서비스 공통 직렬화, lock 그래프 등 기존 minor는 해결로 바꾸지 않는다. 추가 설계·SP1 브리프 작성 전에 이 절과 보고서의 인계 항목을 읽을 것.

## 2026-09-27 SP1 인계·계획 시작 (Codex 구현자)

- 사용자 인계 지시로 검증된 SP4를 로컬 master에 fast-forward 병합(`77efa2e`→`32ab51f`)하고 `codex/sp1-actions`를 만들었다. 원격 게시 없음.
- [SP1 계획](../superpowers/plans/2026-09-27-sp1-actions.md), [진행 기록](../superpowers/sp1-progress.md). 이번 변경은 문서뿐이며 제품 코드는 구현 미착수다.
- SP4 이월을 T1에 편입했다. 링크 쉼표/셸 미실행·도구별 가용성·문구·패키지/RID/고지 항목을 처리하고, 공통 실행 수명은 T2, 실제 복구 저장소는 T3, 실환경 확인은 T12에 배정했다. 기술 근거가 부족한 API는 실제 조치 활성화의 선행 조건으로 기록했다.
- 관련 REV-010·014~018의 완료 범위는 유지하며, 계획에 적었다는 이유로 잔여 minor를 해결 처리하지 않는다. 기존 REV-002·003·006·007의 독립 검증 대기도 그대로다. 이후 Task가 해당 코드를 변경하면 관련 회귀와 기존 원장 근거를 함께 확인할 것.
- 검증: master 조상 관계/fast-forward 결과, 기존 feature와 master 트리 일치, 문서 링크와 diff 형식. 동일 코드 병합·문서 변경이므로 빌드/테스트 재실행 없음. 계획 자체 검토와 독립 설계 검토를 구분한다.

## 2026-09-27 SP1 Task 1 대응 (Codex 구현자·자체 검증)

- 코드 `11959e0` (`57440ac..11959e0`). [파일별 대응 및 검증 보고서](2026-09-27-sp1-task1-implementation.md), [플랫폼 계약/이월 책임표](sp1-platform-contracts.md). **구현·자체 검증 완료, 독립 리뷰 대기**.
- SP4 이월 대응: Explorer 새 프로세스 실행을 기존 데스크톱 COM 위임으로 교체. 셸 창·동일 PID/세션·TokenElevation 확인, 셸 없음/승격/연결 실패는 거절, 쉼표 URL 차단, 실패 주소 AbsoluteUri. 실제 현 호스트는 Explorer가 승격 상태여서 거절 경로만 확인했으며 비승격 COM 성공/브라우저는 T12.
- 도구별 가용성 스냅샷과 명시적 갱신으로 dotnet만 설치된 PC에서 npm을 실행 가능으로 세지 않는다. SystemOnly 사용자 도구 조회 금지. 예전 미사용 UI 명령/문구 정리, 사용자 지정 볼륨 이름은 식별 정보 포함에만 표시.
- publish profile 분리·Debug PDB·공통 RID lock 그래프·런타임/WPF-UI 원문 고지·미서명 안내·ZIP SHA-256. 실제 ZIP 검증 및 고지 누락/규칙 변조/중복/해시 불일치 4종 거절 회귀. 시작점 의존성 버전 변경 없음.
- 검증: 솔루션+4개 csproj+publish `restore --locked-mode` 통과, Debug/Release 빌드 각 경고 0/오류 0, 기본 **1288/1288**, 조회 Smoke **23/23**. 최종 평가 ZIP 13파일/최상위 4개, SHA-256 `4EE550C46D099BB5FE8C8764F852992183951EE7EC3558D37F9836FE91A3F13D`. 명령/TRX/초기 실패와 보정 근거는 보고서 참조. Online/ToolSmoke 미실행, 실제 사용자 캐시·설정 변경 없음.
- REV-010·014~018 기존 완료 범위 유지. REV-017 서비스 직접 호출 상호 배제는 T2, 복구 저장소는 T3, 사용자 설정 경로는 T10, 실제 UAC/브라우저/.NET/DPI는 T12. REV-002/003/006/007의 이전 독립 검증 대기도 변경하지 않는다.
- 독립 리뷰 인계: 이 원장과 보고서를 읽고 `57440ac..11959e0`을 검토할 것. 기존 발견/이력은 보존하며 구현자 자체 검증을 독립 승인으로 바꾸지 않는다.

## 2026-09-27 SP1 Task 2 대응 (Codex 구현자·자체 검증)

- 코드 `cbadd67` (`c2b1d62..cbadd67`). [파일별 대응·검증 명령·제한·초기 실패 기록](2026-09-27-sp1-task2-implementation.md). **구현·자체 검증 완료, 독립 리뷰 대기**.
- REV-017 잔여인 서비스 직접 호출의 상호 배제를 `Core/Actions/OperationCoordinator`, ScanService/PcSpecService/기존 CacheCleanupService에 연결했다. UI를 우회한 직접 호출과 동기 차단·취소 무시·늦은 실제 종료를 테스트했다. 기존 REV-017 UI 경계 검증 완료 이력은 보존하며, 새 서비스 경계는 `수정됨·독립 재검증 대기`다.
- 준비·적용·복구의 코드 등록 모델/세션 귀속/5분 단조 만료/일회성 계획과 Started·결과 이력을 추가했다. 기존 공식 캐시도 같은 관문과 SID/세션 검증을 사용하며, 프로세스 종료 실패 후 실제 종료가 확인될 때까지 관문을 유지한다(REV-010 후속). SystemOnly와 사양 작업 관문 회귀(REV-016·018) 보존.
- Release **0경고·0오류**, 기본 **1314/1314**, 조회 Smoke **23/23**, 소유 fixture pip ToolSmoke **1/1**. 사용자 캐시/설정 변경 없음. Online·GUI 수동·배포 ZIP은 미실시. Task 1 ZIP을 새 코드 배포본으로 표시하지 않는다.
- 신규 실제 조치 어댑터는 등록하지 않았다. 한 프로세스의 공유 관문과 메모리 내 결과 보존까지이며, 강제 종료 뒤 복구 저장소는 T3, 공통 결과/복구 UI는 T4다. T6/T9/T12 플랫폼 평가 조건은 그대로 유지한다. 다른 REV의 독립 검증 대기를 이번 자체 테스트만으로 닫지 않는다.
- 독립 리뷰 인계: 이 원장과 보고서를 읽고 `c2b1d62..cbadd67`을 검토할 것. 변경/검증 기록을 보존하고 자기 검증과 독립 승인을 구분한다.

## 2026-09-27 사용자 진행 방식 변경·SP1 Task 3 대응 (Codex 구현자)

- 사용자 최신 지시 **“계속진행해 독립리뷰없이 진행한다”**를 반영했다. 이후 진행 조건에서 독립 리뷰를 제외하고 서브에이전트/독립 리뷰어를 파견하지 않는다. 과거 ‘독립 재검증 대기’와 발견/검증 이력은 지우거나 독립 승인으로 바꾸지 않는다. T1~3 진행 상태는 **구현·자체 검증 완료 / 독립 리뷰 사용자 지시로 생략**이다.
- 코드 `7c6d64f` (`20077db..7c6d64f`), [Task 3 구현/검증 보고서](2026-09-27-sp1-task3-implementation.md). 파일 목록·명령·TRX·초기 실패·플랫폼 한계는 보고서에 기록했다.
- 복구 저장소의 관리자 소유/전용 ACL, 조상 고정/링크·hardlink 거절, 토큰/프로필/SID 검증, 크기/스키마/전이 상한, Pending·Restoring 원자 저장과 실패 보존을 구현했다. 현재 값이 적용 값과 일치할 때만 원복하며 재시작 후 미완료 목록을 관측한다. 내부 서비스 복구는 사용자 되돌리기와 구분한다.
- REV-010·017 관련 실제 종료 책임: 취소를 무시하는 변경은 공통 관문뿐 아니라 열린 저장소 잠금도 유지한다. 타임아웃 뒤 재진입 차단·늦은 예외·Pending 보존 대역 검증을 추가했다. REV-016 관련 다른 SID/범위/등록 어댑터 실패는 변경 전에 거절한다. 과거 독립 검증 범위를 새 테스트로 확장했다고 표현하지 않는다.
- 자체 검증: Release **0경고/0오류**, 기본 **1340/1340**, 신규 저장소 관리자 Smoke **13/13**, diff 형식 통과. 샌드박스의 관리자/링크 권한 거절은 기록하고 권한 조건을 완화하지 않은 채 승인된 관리자 테스트로 재검증했다. 사용자 설정/실제 캐시 변경 없음.
- 후속: Task 4 공통 미리보기/결과/복구 목록과 앱 시작 관측, Task 6~9 실제 대상 비교/쓰기와 내부 복구 정책, Task 12 실환경 확인. 전체 기존 Smoke/Online/ToolSmoke·GUI·배포 ZIP은 이번 미실시다. 새 실제 OS 조치는 아직 등록하지 않았다.

## 2026-09-27 SP1 Task 4 대응 (Codex 구현자·자체 검증)

- 사용자 지시대로 독립 리뷰는 생략했다. 코드 `68542e8`와 `5eaa5dc`(범위 `a08207e..5eaa5dc`), [파일별 구현·명령·시각 확인·후속 책임](2026-09-27-sp1-task4-implementation.md). 기존 발견/독립 검증 이력은 보존한다.
- `ActionWorkflow`가 실제 T2/T3 조율기로 발급 계획 ID를 실행한다. `ActionCenterViewModel`·`ActionPresentation`·`ActionCenterWindow`와 App/Main 조립에 미리보기·실행 확인·중단 요청·지속 결과·복구 목록·시작 관측을 추가했다. 신규 조치 성공 스텁이나 무조건 가용 판정을 등록하지 않았다.
- REV-010·017·018 관련 수명/관문: 창 닫기/재열기는 모델과 실제 실행 소유권을 해제하지 않는다. 취소 무시 후 늦은 실패도 종료 대기로 표시하고 실제 종료 후 최종 결과와 한 번 재검사를 반영한다. 실제 WPF Dispatcher에서 PropertyChanged/목록/재검사 콜백의 UI 스레드 귀속을 검증했다.
- REV-013·014 관련 결과 정직성: 실행 전 세션 거절을 일부 정리로 표시하지 않고, 이미 원래 값/이미 적용됨도 별도 안내한다. 예상 크기와 실측 여유 변화, null/0/음수를 구별한다. 복구 목록의 확인 불가/지원 불가 이유와 재검사 실패도 숨기지 않는다.
- 자체 검증: 최종 Release **0경고/0오류**, 기본 **1363/1363**, 신규 UI/흐름 **23/23**, 전체 Smoke **36/36**. Smoke 후 AlreadyApplied 문구/회귀만 추가했고 최종 기본/신규 테스트로 재확인했다. TRX와 오프스크린 렌더 위치는 보고서에 있다. 사용자 PC에 키보드 입력을 보내거나 실제 설정/캐시를 변경하지 않았다.
- 범위: 현재 코드 등록 목록은 비어 있다. Task 5~10 실제 어댑터, Task 10 기존 공식 캐시 통합, Task 11 진단 카드 연결, Task 12 실제 모니터/키보드/UAC/다른 계정/.NET 없는 PC 및 배포 검증이 남아 있다. Task 4 자체 테스트 통과를 독립 승인이나 SP1 전체 출시 완료로 표현하지 않는다.
