# 공유 리뷰 원장

사용자 요청에 따라 Codex의 독립 검토 결과를 구현자·리뷰어가 파일로 확인하고 대응하기 위한 원장이다. 대화의 인라인 코멘트만 읽었다고 가정하지 않는다. 마지막 기록일: 2026-09-26.

## 읽기와 대응

1. 구현 시작·리뷰 시작·단계 완료 보고 전에 관련 항목을 확인한다.
2. 대응 후 항목의 `대응 기록`에 변경 파일/커밋, 실행한 검증 명령, 결과, 남은 제한을 적는다. 보류·비반영은 이유를 적는다.
3. 상태는 `미해결 → 수정됨·재검증 대기 → 검증 완료`로 구분한다. 설계 범위는 `범위 결정 필요`로 별도 표시한다.
4. 이후 독립 리뷰도 이 파일에 ID를 추가한다. 이미 확인된 문제를 다른 리뷰어의 승인만으로 닫지 않는다.

## 현재 항목

| ID | 중요도 | 상태 | 대상 | 요약 |
|---|---|---|---|---|
| REV-001 | P1 | 수정됨·재검증 대기 | P4 보호 정책 | 드라이브 루트로 지정된 Known Folder가 보호 목록에서 빠짐 |
| REV-002 | P2 | 수정됨·재검증 대기 | P4 파일 순회 | 단일 폴더 열거·처리 중 취소 및 시간 예산 확인 누락 |
| REV-003 | P2 | 미해결 | P2 UI / P7 | 작업이 실제 종료돼도 ‘종료 중’ 안내가 다음 검사까지 남음 |
| REV-004 | 제품 범위 | 범위 결정 필요 | 구현 계획 | 사용자가 기대한 기존 도구 통합이 계획에서 2차로 연기됨 |
| REV-005 | P2 | 검증 완료(장치 ID 범위) | P3a 내보내기 | 장치 내부 ID 원문 노출 보완 확인 |

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

## REV-002 — 폴더 내부에도 취소·시간 예산 검사 필요

- 발견/확인: 2026-09-26, P4 작업본에서 재현. `9fed432`에서 해당 두 루프의 누락이 유지됨을 소스로 확인했다.
- 위치: `src/PcOptimizer.Probes/Storage/VolumeTraversalRun.cs`, `ListDirectory`의 열거 및 처리 루프.
- 조건: 하위 폴더 없는 단일 폴더의 열거 도중 취소하거나, 수동 시간 공급자를 121초 진행시킨다(예산 120초).
- 실제: 취소해도 스캐너가 정상 반환한다. 예산 초과도 `TimedOut=false`로 반환한다. 폴더 외부 루프에만 검사가 있어 파일이 많은 폴더의 내부 작업이 중단 조건을 확인하지 않는다.
- 기대: 항목 열거·처리 중에도 협력적 취소와 예산을 확인한다. 예산 초과는 관측된 부분 합계와 시간 초과로 기록한다. OS 호출 한 번 자체를 강제로 중단할 수 있다는 뜻은 아니다.
- 재현: [IndependentReviewTests.cs](repro/IndependentReviewTests.cs)의 `CancellationInsideOnlyDirectoryIsObserved`, `BudgetExceededInsideOnlyDirectoryIsReported`. 임시 복사본에서 두 테스트 모두 실패.
- 완료 근거: 두 재현 테스트 통과, 큰 단일 폴더와 열거 중 부분 결과 처리를 검증.
- 대응 기록 (2026-09-26, P4 리뷰 1차 수정, 구현자):
  - 변경: `src/PcOptimizer.Probes/Storage/VolumeTraversalRun.cs` `ListDirectory` — 항목 열거 루프와 처리 루프에서 항목마다 `ThrowIfCancellationRequested`와 볼륨 예산을 확인. 예산 초과 시 그때까지 관측한 항목만 집계하고 해당 디렉터리를 `Timeout`으로 기록, 실행기·루트의 `TimedOut=true`. 커밋 `5399f00`.
  - 회귀 테스트: `IndependentReviewTests.CancellationInsideOnlyDirectoryIsObserved`, `BudgetExceededInsideOnlyDirectoryIsReported` 통과. 기존 `FileSystemScannerTests.시간_예산을_넘기면_부분_집계로_멈춘다`는 열거 도중 예산을 넘긴 폴더(d2)가 이제 관측 항목 없이 Timeout으로 남도록 기대값을 갱신(합계 41, Timeout 2, d2 열거 실패 사유 Timeout).
  - 명령(작업 루트, Release):
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~IndependentReviewTests"` — 수정 전 `실패: 3, 통과: 0`(Expected True / Actual False, OperationCanceledException 기대 불일치), 수정 후 전부 통과.
    - `dotnet test tests/PcOptimizer.Tests -c Release --filter "FullyQualifiedName~ProtectionPolicyResolverTests|FullyQualifiedName~FileScanRulesTests|FullyQualifiedName~PathTokenizerTests|FullyQualifiedName~ReportExporterTests|FullyQualifiedName~FileScanServiceTests|FullyQualifiedName~FileScanProbeTests|FullyQualifiedName~IndependentReviewTests|FullyQualifiedName~FileSystemScannerTests|FullyQualifiedName~ScanRootCatalogTests"` — `통과: 80, 실패: 0`.
    - `dotnet build -c Release` — 경고 0개, 오류 0개.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke"` — `통과: 655, 실패: 0`.
    - `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category=Smoke"` — `총 16, 통과 16`(관리자 셸, 실제 파일 스캔 Success, timedOut=False).
  - 남은 제한: 확인은 협력적이다. OS 열거 호출 한 번(FindNextFile 묶음)이나 파일 ID·할당 크기 조회 한 번은 중간에 끊을 수 없으므로 그 호출이 끝난 뒤 다음 항목에서 멈춘다. 큰 단일 폴더의 실제 대량 항목(수십만 개) 성능 측정은 하지 않았다(가짜 열거로만 검증).

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
- 대응 기록: 아직 없음.

## REV-005 — 장치 ID 익명화 검증 이력

- 최초: P3a `f3c657b` 내보내기에 모니터 장치 경로, GPU PnP ID, 물리 디스크 제공자 ID 원문이 남았다.
- 보완: `7de6324`에서 내보내기 단위 토큰화 도입. 후속 `a450fac`에서 문장 부호 처리 보완.
- 독립 확인: 보완 작업본을 임시 복사본에 적용한 429개 테스트 통과, 실제 내보내기의 모니터/어댑터/PnP/디스크 ID 토큰 치환 확인. P3b 커밋의 기본 523개 및 스모크 13개도 재실행 통과.
- 한계: 장치 ID 범위의 확인이다. P4 경로, 시작 명령의 인자 등 향후 추가되는 모든 데이터의 익명화를 포괄 승인하지 않는다.

## 독립 검증 기록

- P3b: `0bce903`(구현 `a0ab605` 포함)을 `git archive`한 별도 임시 복사본에서 Release 빌드 경고 0/오류 0, 기본 523/523, Smoke 13/13 확인.
- 명령: `dotnet build PcOptimizer.sln --configuration Release`, `dotnet test PcOptimizer.sln --configuration Release --no-build --no-restore --filter "Category!=Smoke"`, 같은 명령의 `Category=Smoke` 필터.
- 실제 UAC 승인/취소 UI, 다른 계정 자격 증명 입력, 화면 배치 전체는 위 결과로 검증됐다고 간주하지 않는다.
- P4: 완료 승인 전 작업본을 별도 복사해 추가 재현 테스트 3개 실행, 3개 실패. 실제 개인 폴더를 스캔하거나 수정하지 않은 fake 환경 테스트다.
- 재현 파일은 `docs/reviews/repro`에 보관하므로 일반 테스트 빌드에 자동 포함되지 않는다. 검증용 복사본의 `tests/PcOptimizer.Tests/Unit/Probes/`에 복사 후 `dotnet test PcOptimizer.sln --configuration Release --filter "FullyQualifiedName~IndependentReviewTests"`로 실행한다. 수정 시 정식 회귀 테스트로 편입할 수 있다.
