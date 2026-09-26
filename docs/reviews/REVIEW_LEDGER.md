# 공유 리뷰 원장

사용자 요청에 따라 Codex의 검토 결과를 구현자·리뷰어가 파일로 확인하고 대응하기 위한 원장이다. 마지막 기록일: 2026-09-27. 사용자의 ‘이어서 작업 진행’ 지시 이후 Codex가 구현도 인계받았다. 이후 자기 수정 검증과 별도 독립 리뷰를 구분한다.

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
