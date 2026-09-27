# SP1 Task 2 구현·자체 검증

기준 `c2b1d62`, 코드 `cbadd67`, 브랜치 `codex/sp1-actions`. 구현자 Codex의 자체 검증이다. 독립 리뷰 승인은 대기다. 신규 OS 변경 어댑터는 등록하지 않았다.

## 구현 범위

| 파일/범위 | 결과 |
|---|---|
| Core `Actions/OperationCoordinator.cs` | Scan·Specification·Prepare·Apply·Restore가 공유하는 즉시 거절 관문. 소유 lease 반환과 실제 작업 완료를 구분하며 남은 Task가 있으면 Draining. 늦은 실패 관측, 이전 lease 중복 해제 방지, UI 알림 오류 격리 |
| Core `Actions/ActionModels.cs` | 닫힌 조치 ID/대상 종류, 사용자/시스템 범위, 실제 SID/Windows 세션, 계획·미리보기·Started 결과, 코드 등록 어댑터 계약. 대상 식별 키는 어댑터가 재식별하며 파일 스냅샷/복구 데이터는 후속 Task 책임 |
| Probes `ActionCoordinator.cs` | 서비스가 발급·보관한 ID만 실행. 단조 시간 기준 5분 만료, 일회성 소비, 세션/SID/현재 범위와 변경 직전 재검증. 기본 2분 준비/실행 대기 제한(생성 시 지정 가능), 사용자 취소 후 실제 종료까지 관문 유지. 예외/늦은 결과 이력 보존 |
| App `ScanService`, `PcSpecService`, Core `ScanCoordinator`, `ProbeExecutor` | 서비스 진입에서 공통 소유권 획득, 실제 Task.Run 프로브를 등록. 동기 WMI 차단·타임아웃·취소 무시에도 실제 프로브가 끝날 때까지 상호 배제. 기존 내부 검사 관문은 단독 ScanCoordinator 사용 보호용으로 유지 |
| Probes `CacheCleanupService`, `CacheToolProcess`, `SystemCacheToolBackend`, `SystemActionSession` | 기존 npm/pip/NuGet 준비·실행도 공유 관문과 세션 귀속 사용. 공식 프로세스의 종료 관측을 별도로 기다려 Kill/Wait 실패 뒤 결과만 반환됐다는 이유로 새 검사를 허용하지 않음. 기존 실행 인자·허용 목록 유지 |
| App 구성/두 ViewModel/두 창/문구 | App에서 Scan의 동일 관문을 Spec과 정리 창으로 전달. 상태 변경 시 검사·사양 새로 고침·정리 명령 재평가, 정리 창 종료 뒤 남은 실행 사유 표시. ViewModel Dispose는 알림을 해제하며 서비스 소유 Task를 해제하지 않음 |

새 ActionCoordinator는 후속 Task 4에서 UI와 조립할 기반이며, 현재 제품의 신규 전원/주사율/시작 프로그램/파일 삭제 명령을 활성화하지 않는다. 기존 공식 캐시 실행기는 이 단계에서 공통 관문만 먼저 연결했다.

## 검증

실제 설정·사용자 캐시는 변경하지 않았다. .NET SDK 10.0.401, 의존성 변경 없음.

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build PcOptimizer.sln -c Release --no-restore
& 'C:\Program Files\dotnet\dotnet.exe' test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp1-t2-final-unit.trx'
& 'C:\Program Files\dotnet\dotnet.exe' test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category=Smoke' --logger 'trx;LogFileName=sp1-t2-final-smoke.trx'
$env:PCOPTIMIZER_TEST_PYTHON='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& 'C:\Program Files\dotnet\dotnet.exe' test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category=ToolSmoke' --logger 'trx;LogFileName=sp1-t2-toolsmoke.trx'
```

- Release 빌드: **경고 0·오류 0**. 최종 기본 **1314/1314** (Task 1 대비 +26).
- 조회 Smoke **23/23** (61초). 검사/사양/기존 실제 조회 경로 검증. 이후 수정은 신규 미등록 ActionCoordinator의 준비 만료/결과 보존 보강, UI 거절 문구, 단위 테스트와 주석이며 해당 실제 조회 코드는 바뀌지 않았다.
- 실제 pip ToolSmoke **1/1**. 테스트가 만든 HTTP/wheel 캐시만 정리했고 설치 패키지 fixture 보존, 추가한 실제 프로세스 종료 대기 통과. 제품의 Program Files 검색 허용 조건을 통과한 실설치 검증은 아니다.
- TRX는 `tests/PcOptimizer.Tests/TestResults/`(git-ignored). Online·GUI 수동·배포 ZIP 재생성은 미실시. Task 1의 평가 ZIP을 Task 2 산출물로 취급하지 않는다.
- `git diff --check` 통과. 마지막 커밋 전 주석 두 줄만 보정했으며 실행 코드는 검증본과 같다.

## 회귀 조건과 초기 실패 처리

- `OperationLifetimeTests`: 다섯 작업 종류의 모든 쌍, 오래된 lease의 재해제, 반환 뒤 부모/자식/늦은 실패, UI 없는 Scan↔Spec 양방향 직접 호출, 동기 차단 프로브의 타임아웃 뒤 재진입 거절·실제 종료 후 회복, 캐시 결과 반환 후 네이티브 drain, 기존 캐시 계획의 SID/세션/범위 변경 거절.
- `ActionCoordinatorTests`: 서비스 간 계획 ID 불인정, 일회성, 미등록/잘못된 대상 거절, SystemOnly/Unknown, 시스템 조치의 알려진 세션 요구, Power를 System 범위로 잘못 등록하는 우회 거절, 5분 만료·변경 직전 재검증, 취소/시간 초과 뒤 Apply/Restore 소유권·Started 보존·늦은 실패 이력, 준비 취소 뒤 계획 미발급·실제 네이티브 종료 대기.
- `MainViewModelTests.SharedOperationNotifiesUiAndOutlivesViewModel`: 서비스를 통한 관문 점유만으로 UI 명령/안내 변경, 창 해제 뒤에도 소유권 유지.
- 초기 기존 테스트 3건 실패: 사양 타임아웃 후 재호출이 ‘프로브 건너뜀’에서 ‘서비스 호출 거절’로 강화되어 REV-017 테스트 두 개의 기대를 바꿨다. **프로브 호출 횟수 1회 단언과 실제 종료 후 재실행 단언은 보존**했다. 기존 캐시 동시 실행 테스트는 Task.Run 도입 후 즉시 실행됐다는 가정 대신 ClearEntered 신호로 실제 진입을 기다리도록 바꿨다. 이 실패를 새로운 독립 RED 검증이라고 주장하지 않는다.

## 제한과 다음 Task

- 공통 관문은 **한 프로세스의 공유 인스턴스**다. App의 조립 지점에서 하나를 전달한다. 단독 테스트용 기본 생성자는 개별 관문을 만들므로 외부 조립 시 Scan.Operations를 반드시 공유해야 한다. 다른 앱 인스턴스/외부 Windows 도구와의 프로세스 간 잠금을 보장하지 않는다.
- 작업이 취소에 응하지 않거나 종료 관측이 계속 실패하면 관문은 계속 Busy/Draining이다. 신규 어댑터의 종료 관측 오류는 재시도하며 종료를 추정하지 않는다. 프로세스 강제 종료/전원 차단 후 복구는 이 메모리 내 소유권의 보장 범위가 아니다.
- Started는 어댑터가 변경 직전 호출하는 MarkStarted 기록이다. 성공은 어댑터의 후속 관측 결과에 따라야 하며 ‘설정이 개선됐다’는 뜻으로 바꾸지 않는다. 신규 실제 어댑터는 아직 없고 관련 검증은 소유 대역이다.
- 결과는 현재 서비스 메모리에 보존된다. **Task 3**에서 원자적인 Pending/복구 저장소와 재시작 처리를 구현한다. **Task 4**에서 공통 UI/결과/복구·drain 이후 재검사를 연결한다. 파일 집합 스냅샷과 보호/복구 재검증은 T5 이후 실제 어댑터 책임이다.
- REV-017의 기존 `검증 완료(현재 UI 경로)` 이력은 유지한다. 서비스 직접 호출 잔여 대응은 이 커밋으로 **수정됨·독립 재검증 대기**다. REV-010·016·018 기존 회귀도 통과했으나 독립 승인으로 승격하지 않는다.
- 다음 검토자는 [공유 원장](REVIEW_LEDGER.md)을 읽고 `c2b1d62..cbadd67`, 특히 공유 관문 조립·lease/실제 Task 수명·새 계획의 취소 및 결과 보존을 재검증할 것.
