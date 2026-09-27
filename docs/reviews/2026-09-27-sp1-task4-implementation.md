# SP1 Task 4 구현·자체 검증 (2026-09-27)

기준 `a08207e`, 구현 `68542e8`, 결과 문구 보정 `5eaa5dc`, 브랜치 `codex/sp1-actions`. 사용자 지시에 따라 독립 리뷰는 생략하며 아래는 구현자 자체 검증이다.

## 구현 내용

- `Core/Actions/ActionModels.cs`: 미리보기의 대상 표시·안전 조건·예상 논리 크기·재부팅 정보와 실행 후 관측한 여유 공간 변화(음수/null 포함)를 선택 메타데이터로 추가했다. 기존 호출 계약은 기본값으로 유지한다.
- `App/Services/ActionWorkflow.cs`: 코드에 등록한 직접 조치와 T3 복구 조치를 하나의 UI 인터페이스로 연결한다. 서비스가 발급한 계획 ID만 실행하고, 중복 등록과 현재 세션 범위 밖 조치를 가용하다고 표시하지 않는다. 기존 ActionCoordinator/RestoreCoordinator의 일회성·5분 만료·공통 관문·Started/실제 종료 계약을 재사용한다.
- `ActionCenterViewModel.cs`: 앱 수명의 미리보기/실행/중단 요청/늦은 결과/이전 결과/복구 목록. 창 닫기는 실제 작업 취소나 결과 삭제가 아니다. 실제 관문 해제와 최종 결과 확인 뒤 기록을 갱신하고, Started인 조치만 한 번 재검사한다. 후속 재검사 실패도 조치 결과를 지우지 않는다.
- `ActionPresentation.cs`: 실행 전 설명·대상·영향·안전 조건·재부팅·복구·확인 만료를 표시한다. 결과는 실행 전 거절, 이미 원래 값, 일부 변경 가능성, 복구 실패, 종료 대기, 성공으로 구분한다. 예상 논리 크기와 실제 여유 공간 변화를 나누고 null/0/음수를 각각 확인 불가/변화 없음/감소로 표시한다. 공간과 무관한 설정 조치에는 용량 항목을 숨긴다.
- `ActionCenterWindow.xaml/.cs`: 세로 스크롤/줄바꿈, 확인 실행과 실행하지 않기, 중단 요청, Esc로 창 닫기, 현재/이전 결과, 복구 필요 및 확인 불가 기록을 제공한다. 실행 버튼은 기본 Enter 버튼이 아니며, 창을 닫아도 같은 앱 모델로 다시 열린다. 지원되지 않는 복구 항목은 버튼 비활성 사유를 표시한다.
- `App.xaml.cs`, `MainViewModel`, `MainWindow`: 같은 검사/사양 실행 관문으로 조립하고 메인 화면의 ‘조치 기록 · 되돌리기’ 진입 및 상태 안내를 추가했다. 앱 시작 때 복구 기록을 확인한다. 실제 신규 조치 등록 목록은 아직 비어 있다.
- 복구 목록은 ID/조치 종류/시각/상태/가용성만 투영한다. SID·원래 설정 bytes·저장소 원문을 화면 DTO나 익명 내보내기에 추가하지 않았다. 손상/권한 실패는 빈 정상 목록으로 처리하지 않는다.

Task 4의 신규 조치는 소유 대역으로만 실행했다. 기존 공식 캐시 창은 그대로이며 F1의 공통 흐름 편입은 Task 10, 실제 신규 조치 등록은 Task 5~10, 진단 카드별 연결은 Task 11이다. 따라서 `FindingAction`/기존 `IActionAvailability`에 아직 구현하지 않은 신규 조치를 광고하지 않았다. 준비 화면 진입 API와 등록 경계만 마련했다.

## 검증

- Release 빌드: `dotnet build PcOptimizer.sln -c Release --no-restore` — **0경고/0오류**.
- 전체 기본: `dotnet test PcOptimizer.sln -c Release --no-restore --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke" --logger "trx;LogFileName=sp1-t4-final-unit.trx"` — **1363/1363**. T3 1340 + 신규 23건.
- 신규 UI/흐름: `dotnet test tests/PcOptimizer.Tests/PcOptimizer.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~ActionCenter" --logger "trx;LogFileName=sp1-t4-ui-final.trx"` — **23/23**.
- 전체 Smoke: 관리자 토큰으로 `dotnet test PcOptimizer.sln -c Release --no-build --filter "Category=Smoke" --logger "trx;LogFileName=sp1-t4-final-smoke.trx"` — **36/36**(47초). 기존 조회 23건과 T3 소유 임시 저장소 13건.
- `git diff --check`: 통과. TRX는 `tests/PcOptimizer.Tests/TestResults/`에 있다.

Smoke 완료 후 최종 자체 점검에서 `AlreadyApplied`가 일반 실패 문구로 떨어지는 경우를 보정했다(`5eaa5dc`, 문구 두 줄/회귀 한 건). 이후 Release 빌드와 기본 1363/1363·신규 23/23을 다시 확인했다. 네이티브 경로 변경이 없어 Smoke는 반복하지 않았다.

테스트는 성공 스텁만 호출하는 대신 실제 `ActionWorkflow`/`ActionCoordinator`/`RestoreCoordinator`에 소유 설정 대역을 등록했다. 확인 취소 시 변경 없음, 세션 변경 거절, 종료 전 재진입 차단, 취소 무시 후 늦은 실제 실패, 관문 해제 후 한 번 재검사, 재검사 실패 시 결과 유지, 실제 복구 흐름·목록 갱신·중복 방지, 손상 목록/지원 불가 이유, 원문 비노출, null/0/음수 공간 변화가 포함된다.

WPF 실제 Dispatcher 테스트에서는 실행 중 첫 창을 닫고 같은 모델로 다시 열었다. 늦은 완료 후 PropertyChanged/결과 목록/재검사 콜백이 UI 스레드에서 실행되는지 확인했다. 키보드 검증은 IsDefault=false·IsCancel=true와 명령 계약 검증이며 실제 키 입력을 PC에 보내지는 않았다.

## 시각 확인과 한계

`PCOPTIMIZER_UI_ARTIFACTS=artifacts/sp1-task4-ui`로 440×720 논리 픽셀의 확인/결과 화면을 100/150/200% PNG로 렌더링했다. 긴 대상은 줄바꿈하고 내용은 세로 스크롤로 접근한다. `action-preview-100.png`, `action-result-150.png`를 직접 확인했다. 초기 렌더는 Window 배경을 제외한 루트가 투명하고 디스패처 바인딩 갱신이 남아 있어 배경과 종료 안내가 부정확했다. 루트 배경을 명시하고 렌더 전에 실제 바인딩 큐를 소진한 뒤, 완료 시 진행 안내가 Collapsed인지 단언했다. 최종 이미지에서 텍스트 대비/줄바꿈/버튼/결과 구분을 확인했다.

PNG는 **대역 조치 예시**이며 실제 전원 계획 변경이나 실제 확보 용량을 의미하지 않는다. 실제 모니터 DPI/키보드 조작/UAC/.NET 없는 PC 검증은 T12에 남아 있다. 앱을 화면에 띄워 사용자 입력을 보내지 않았고 실제 사용자 설정/캐시를 변경하지 않았다. Online·ToolSmoke·새 배포 ZIP은 이번에 실행/생성하지 않았다.

## 다음 구현자에게

- Task 5는 A1 사용자 임시 파일과 파일별 삭제 엔진이다. `ActionWorkflow`의 direct 목록에 검증된 어댑터를 등록하고 발급 미리보기에 실제 대상/안전 조건/예상 크기/복구 불가를 제공한다. 결과의 Effect는 실제 관측값일 때만 설정한다. 사용자 PC의 실제 삭제로 테스트하지 않는다.
- Task 6~9의 복구 가능한 조치는 reversible 목록에 등록한다. 미완료 내부 서비스/표시 복구 정책은 아직 활성화되지 않았다. JSON을 실행 지시로 신뢰하지 않는 T3 경계를 유지한다.
- Task 10에서 기존 공식 캐시 조치를 공통 창으로 편입하고 Task 11에서 카드/추천/선택 흐름을 연결한다. 현재 UI에 조치 버튼이 없다는 이유로 가짜 성공 어댑터나 무조건 가용 판정을 넣지 않는다.
- 앱 시작의 기록 확인과 조치 후 재검사는 공통 관문을 점유한다. 다른 작업과 경합하여 후속 재검사를 시작하지 못하면 명시적으로 안내하며 중복 자동 재시도를 만들지 않는다.
