# HANDOFF — PC Optimizer (2026-09-27)

현재 브랜치 `codex/sp1-actions`. 사용자 인계 지시에 따라 `feature/p0-skeleton`을 로컬 `master`에 fast-forward 병합했다(`77efa2e` → `32ab51f`). 기존 브랜치는 보존했고 원격 게시는 하지 않았다. 아래 날짜별 기록은 당시 상태를 보존한 이력이다.

## 현재 작업 — Steam·그래픽 캐시 위치별 검사 / preview.6

- 기준 `9bafe82`. [변경 파일·계약·검증·배포](reviews/2026-09-27-shader-cache-inspection.md). Steam은 승격 검사에서도 제한된 라이브러리 설정을 읽어 각 shadercache만 관측하고 NVIDIA DXCache/GLCache·Windows D3DSCache는 위치별로 나눠 보여 준다.
- 첫 화면 **앱 캐시 위치·용량** → 앱 캐시 분류 → **캐시별 용량·주의사항 보기**. 기존 Resolve/CapCut/Adobe도 같은 분류에서 확인한다. 새 관측이 있을 때 같은 Steam/그래픽 보충 카드만 대체한다.
- Release 0/0, 최종 기본 **1564/1564**(신규 30), 전체 Smoke **44/44**, ZIP 13파일/최상위4개 검증 완료. 실제 PC의 Steam 4라이브러리·합계 12.5 MB, 그래픽 합계 11.9 GB를 관측했다(확보 가능량 아님). 상세·해시는 보고서 완료 기록 참조. 기존 공통 보호·삭제 정책은 유지하고, 전용 Steam 읽기에만 Program Files 보호 출처의 제한적 예외를 적용했다. 중복된 다른 보호 출처는 차단한다.
- 이번 기능은 검사이며 Steam/그래픽 자동 삭제는 아직 미등록이다. T6/T9/T10 실행 계약·T8/T12 실환경 평가가 남는다. 사용자 데이터 변경/독립 리뷰/원격 게시/master 병합 없음. 새 배포본은 `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.6-win-x64.zip`.

## 이전 기록 — DaVinci Resolve·CapCut 캐시 검사 / preview.5

- 구현·테스트·패키징 기록 커밋 `820b53d` (`806bff9..820b53d`, 19파일). 후속 문서 커밋은 이 참조만 보존한다.
- 사용자 요청에 따라 두 앱의 읽기 전용 캐시 후보 검사와 전체 결과 카드의 **캐시 위치·정리 방법 보기**를 추가했다. [변경 파일·범위·예외·검증](reviews/2026-09-27-video-editor-cache-inspection.md). 기준 `806bff9`.
- Resolve는 제한된 config.dat 전역 CacheClip 위치 또는 설정이 없을 때 기본 Videos/CacheClip을 확인한다. CapCut은 기본 User Data/Cache만 관측한다. 프로젝트/원본/DB를 검사하거나 새 자동 삭제를 제공하는 기능은 아니다. 옮겨진 CapCut·Resolve 프로젝트별 재정의는 미확인이다.
- 기존 보호/삭제 정책은 유지한다. 중복 보호 출처를 보존하고 새 inspector에만 Videos 바로 아래 CacheClip 읽기 예외를 뒀다. 승격 시 Resolve의 작은 설정 읽기만 별도로 허용한다. 구체적 계약은 보고서 참조.
- Release 0/0, 최종 기본 **1534/1534**, 전체 Smoke **44/44**. 사용자 데이터/설정 변경 없음. 독립 리뷰는 사용자 지시로 생략했다. 배포·최종 호스트 결과는 보고서 완료 기록 참조.
- 새 ZIP: `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.5-win-x64.zip`. 실행 중인 이전 앱은 교체하지 않는다. **전체 결과**에서 확인할 것. T10 전체·T6/T9·실환경 평가와 UI/UX 개선은 잔여다.

## 이전 기록 — Adobe 기본 미디어·파형 캐시 정리 / preview.4

- 구현·테스트·배포 기록 커밋 `4a7b17c` (기준 `4eb6aac`). 후속 문서 커밋은 제품 코드를 변경하지 않는다.
- 사용자 지시대로 기능 추가를 이어갔다. T10 F2 중 Adobe 기본 두 폴더의 90일 이상 `.cfa`/`.pek` 정리, 프로세스 관문, 카드에서 대상 확인·별도 실행, 부분 결과·재검사를 연결했다. [변경 파일·검증 명령·제한](reviews/2026-09-27-sp1-adobe-cache-implementation.md).
- 사용자 지정 위치/캐시 DB/프로젝트/렌더는 포함하지 않는다. 프로세스 조회는 알려진 이름의 관측이며 새 프로세스 실행 자체를 막는 OS 잠금은 아니다. 정리 중 관련 앱 실행 금지 안내와 파일별 배타 핸들을 함께 사용한다. 실제 Adobe 버전별 재생성/작업 재개 평가는 아직 남는다.
- Release 0경고/0오류, 신규 단위·UI 연결 **32/32**, 전체 기본 **1490/1490**, 전체 Smoke **44/44**. 실제 삭제는 GUID 소유 fixture로만 확인했고 사용자 캐시·설정 변경 없음. 독립 리뷰는 사용자 지시로 생략했다. Online/ToolSmoke는 이번 미실행.
- 배포 경로 `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.4-win-x64.zip`. 해시·패키지 검증은 위 보고서 완료 기록 참조. 기존 실행 중인 앱을 교체하지 않으므로 새 ZIP의 EXE로 실행할 것.
- 다음 잔여: T10 사용자 지정 Adobe 경로·Steam/NVIDIA, T6 시작 프로그램, T9 Update/DO, T8/T12 실환경 평가. 기본 Adobe만 완료했으므로 F2/SP1 전체 완료로 표시하지 않는다. UI/UX 전면 개편은 여전히 후속이다. master 병합·원격 게시 없음.

## 이전 기록 — 주사율 카드에서 직접 시험 연결 / preview.3

- 구현 커밋 `a2646a1` (기준 `0507530`).
- 후속 사용자 피드백: “기능은 동작하네 UI UX는 개선해야겟지만”. 주사율 카드 연결 후 사용자 동작 확인을 받았다. 개별 Hz/시간 초과 원복/재부팅까지 확인한 보고는 아니며, 남은 기능 우선·UI/UX 후속 개선 방침을 유지한다.
- 사용자 지적: preview.2 일반 실행 카드에는 설정 열기만 있어 실제 기능에 접근할 수 없었다. [카드 연결 수정/검증 보고서](reviews/2026-09-27-display-card-action-integration.md).
- 이제 일반 실행 Full 사용자에게 **NHz 시험 적용** 버튼을 제공한다. 카드 클릭 → 같은 모니터/주사율 재조회·사전 확인 → **확인한 내용 실행** → 유지/원복. 별도 실행 인자가 필요 없다. ‘바로 할 수 있는 것’ 건수와 주사율 변경/되돌리기 진입도 연결했다.
- Release 0/0, 신규 카드/연결/XAML **9/9**, 전체 기본 **1458/1458**. 변경은 카드·조립·설명/주의 등급이며 네이티브 실행기는 그대로다. Smoke/Online/ToolSmoke 이번 미실행. 사용자 실제 화면 전환도 하지 않았다.
- 주사율 실제 전환·원복·재부팅 평가는 여전히 남는다. 기본 접근 가능과 실기 검증 완료를 혼동하지 말 것. 아래 preview.2의 ‘인자로만 제공’ 정책은 이번 변경으로 대체되며 과거 기록으로 보존한다.
- 배포: `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.3-win-x64.zip` 13파일/최상위4개 검증. SHA-256 `76364D4C5FF40A82A0D33B0660B220FCBA2B925FE9323A3AC606F25E14629FE8`. 기존 실행 중인 preview.2는 교체하지 않았으므로 새 ZIP으로 실행해야 새 버튼을 볼 수 있다.

## 이전 기록 — 기능 우선 / SP1 Task 8 주사율 평가 기능

- 구현·테스트·보고서 커밋 `a885d22` (기준 `aec6c27`). 후속 기록 커밋은 제품 코드를 바꾸지 않는다.
- 최신 사용자 정정: **UI/UX·설명문 개선은 나중, 기능 추가 구현이 우선**. 독립 리뷰 없이 계속한다. [이번 구현·검증·제한 보고서](reviews/2026-09-27-sp1-display-trial-implementation.md).
- 주사율 후보 선택·사전 시험·임시 적용·15초 유지/원복·유지 후 저장·중단/이전 설정 복구를 구현하고 평가 화면에 연결했다. 공유 관문·SID·모드/출력 재식별·원본 선행 저장을 유지한다.
- `--display-evaluation`으로 실행한 Full 사용자에게만 평가 버튼을 제공한다. **일반 실행의 기본 자동 기능은 그대로이며 실기 화면 전환 검증은 미완료**다. 강제 종료·전원 차단·네이티브 호출 멈춤 중 원복 보장 없음. 재연결/재부팅 경로가 바뀌면 거절할 수 있다.
- 최종 Release 0/0, 기본 **1449/1449**, Smoke **42/42**. 마지막 대화형 환경 공급자 변경 후 기본 전체와 해당 네이티브 Smoke **1/1** 재확인. 이 PC 후보 15개와 CDS_TEST=0만 확인했고 실제 화면 설정은 바꾸지 않았다. Online/ToolSmoke는 이번 미실행.
- 버전 **0.3.0-preview.2**. 테스트/배포 근거와 명령은 보고서에 기록한다. 사용자 UI 입력이나 개인 캐시 삭제 없음. 남은 T6/T9/F2·수동 평가는 아래 기존 범위를 유지한다. 원장 기존 상태를 독립 승인으로 바꾸지 않는다.
- 배포 ZIP: `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.2-win-x64.zip`, 최상위4개/13파일 검증 통과. SHA-256 `9E8AAA4E100B05283E8A824053C624DC989E1C0A54A993FCC61951BD03E93134`. preview.1 ZIP도 남아 있다.

## 이전 기록 — SP1 지원 조치 통합·평가 ZIP 완료

- 제품 코드 기준 `4cf8cb2` (T5 `f337121`). 후속 커밋은 인계/원장 문서만 갱신한다.

- 사용자 요청대로 독립 리뷰 없이 T5부터 T7·T9 일부·T10 F1·T11 통합·T12 자동 검증/패키징까지 이어서 처리했다. **SP1 전체 자동 기능 완료는 아니다.** [지원 범위/검증/미완료 근거 보고서](reviews/2026-09-27-sp1-supported-actions-validation.md).
- 실제 등록: 사용자 Temp/Explorer 캐시, Windows Temp, npm·pip·NuGet HTTP, 전원 계획 변경·되돌리기. 모두 선택→미리보기→별도 확인→결과·재검사. 공통 창에서 효과와 되돌릴 수 없는 정리를 구분한다.
- Release 경고/오류 0, 기본 1408/1408, Smoke 41/41, ToolSmoke 4/4. 마지막 기본 명령은 읽기 전용 전원 1건을 합쳐 1409/1409. 전원 쓰기는 대역 17건으로 검증했고 실제 호스트 설정은 바꾸지 않았다.
- 새 평가 ZIP: `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.1-win-x64.zip`(59,872,145 bytes), SHA-256 `A87A7E1B78904F50A67D279628EB421862DBD9F38ED62727F53F0A6652793E1F`. verify-package 통과. 구 0.2.0 ZIP도 보존되어 있으므로 새 버전을 정확히 선택할 것.
- **남은 자동 조치**: T6 StartupApproved 빌드별 형식/로그인 검증, T8 주사율 실제 시험·원복·크래시 경계, T9 Update/DO 활동 배타성·서비스 복구·cmdlet 대상 계약, T10 F2 앱별 옮긴 경로/idle 계약. 미검증 상태에서 활성화하지 않으며 Windows 설정/앱 자체 기능으로 안내한다. 계획의 미완료 체크는 그대로 남겼다.
- **남은 실환경 평가**: UAC/다른 계정 SystemOnly, 비승격 브라우저 성공, 전원 실제 변경·복원/정책 거절, .NET 없는 PC, 실제 모니터 DPI·키보드·SmartScreen. 평가 PC/VM이 필요하다. 현 사용자 PC 설정을 테스트 대상으로 바꾸지 않았다.
- 실행 중인 에이전트/제품 작업 없음. 도구 설치 목록은 앱 시작 스냅샷이므로 설치 변경 후 앱 재시작 필요(이월). 다음 세션은 원장/보고서/계획의 미완료 항목부터 이어가며 전체 SP1 완료로 오인하지 말 것. master 병합·원격 게시 없음.

## 이전 기록 — SP1 Task 5 완료, Task 7 구현 중

- 사용자 요청대로 단계 사이에 멈추지 않고 독립 리뷰 없이 진행. T5 `f337121`, 기본 1381/1381, 전체 Smoke 39/39 + 최종 파일 경계 4/4. 상세는 원장/Task 5 보고서.
- T6 자동 쓰기는 지원 OS의 StartupApproved 형식 검증 부재로 미등록을 유지한다. T7 전원 계획 변경·복구 구현을 진행 중이며 이후 T8~12의 독립적으로 가능한 범위를 계속한다.
- 현재 선택 UI에는 기본 사용자 Temp/탐색기 캐시 정리만 등록했다. 실제 사용자 파일/설정은 테스트에서 변경하지 않았다. 배포 ZIP은 여전히 Task 1 버전.

## 이전 기록 — SP1 Task 4 구현 완료 (Codex, 2026-09-27)

- 사용자 지시대로 독립 리뷰 없이 진행한다. 구현 `68542e8`, 결과 문구 보정 `5eaa5dc`. [Task 4 자체 검증 보고서](reviews/2026-09-27-sp1-task4-implementation.md).
- 최종 Release **0경고/0오류**, 기본 **1363/1363**, 신규 UI/흐름 **23/23**, 전체 Smoke **36/36**. Smoke 뒤 변경은 AlreadyApplied 결과 문구와 회귀 한 건뿐이며 최종 빌드/기본 테스트로 재확인했다. Online·ToolSmoke·새 배포 ZIP은 미실시다.
- 앱 수명의 `ActionCenterViewModel`과 `ActionWorkflow`, 공통 확인/실행/결과/복구 창을 연결했다. 메인 화면 ‘조치 기록 · 되돌리기’와 앱 시작 기록 확인이 추가됐다. 창을 닫아도 실행/늦은 결과가 유지되고, 실제 종료 후 기록 갱신·한 번 재검사가 이어진다.
- 확인한 내용 실행은 발급 계획 ID만 사용한다. 실행 전 거절/부분 변경 가능성/이미 원래 값/이미 적용됨/복구 실패/종료 대기를 구분한다. 예상 논리 크기와 실측 여유 공간 변화(null/0/음수)를 분리하고 공간과 무관한 설정에는 용량 항목을 숨긴다.
- **새 실제 조치 등록은 아직 비어 있다.** 기존 공식 캐시 창은 유지한다. Task 5~10에서 검증한 어댑터를 등록하고 Task 10 F1 통합, Task 11 카드별 연결을 진행한다. 이번 UI 예시/테스트는 소유 대역이며 실제 설정/캐시를 변경하지 않았다.
- 다음 작업: **Task 5 — A1 사용자 임시 파일·공통 파일 삭제 엔진**. 고정 미리보기 파일 집합, 실행 직전 핸들/ID/보호 정책 재검증, 새 파일/다른 사용자/정션/8.3/hardlink/placeholder 제외, 잠긴 파일 건너뜀, 부분 결과와 실제 볼륨 여유 변화 관측을 구현한다. 테스트는 소유 fixture에서만 한다.
- T4 렌더 PNG는 `artifacts/sp1-task4-ui/`(git-ignored)다. 440×720 논리 픽셀의 100/150/200% 레이아웃과 실제 WPF 디스패처를 검증했다. 실제 모니터 DPI·키보드 입력·UAC·다른 계정·.NET 없는 PC는 여전히 T12다. `dist/sp1-evaluation`은 Task 1 배포본 그대로다.
- [원장](reviews/REVIEW_LEDGER.md)과 [진행 기록](superpowers/sp1-progress.md)을 다음 구현 전에 읽는다. T3 저장소의 소유권/스키마/현재 값 비교/실제 종료 잠금은 그대로 유지한다.

## 이전 기록 — SP1 Task 3 구현 완료 (Codex, 2026-09-27)

- 최신 사용자 지시: **독립 리뷰 없이 진행**. 기존 대기/승인 이력은 보존하지만 독립 리뷰를 진행 게이트로 요구하지 않는다. 아래 검증은 구현자 자체 검증이다.
- 코드 `7c6d64f`, [Task 3 자체 검증 보고서](reviews/2026-09-27-sp1-task3-implementation.md). 최종 Release 0경고·0오류, 기본 **1340/1340**, 신규 실제 저장소 Smoke **13/13**. 기존 전체 Smoke/Online/ToolSmoke와 GUI/배포는 이번에 재실행하지 않았다.
- 관리자 소유·전용 ACL·조상/파일 링크 거절·SID/프로필 검증·원자 Pending/Restoring 저장·현재 값 비교 복구·30일 보존·중단 기록 관측·저장소 프로세스 간 잠금을 구현했다. 실제 Windows 설정/사용자 캐시는 변경하지 않았다.
- 다음 작업: **Task 4 — 공통 미리보기·결과·되돌리기 UI**. `RestoreCoordinator`는 코드 어댑터를 받아 T2 조율기에 연결하고 `InspectAsync`로 미완료/손상 기록을 제공한다. 앱 시작/목록 연결은 아직 없으며 새 실제 OS 변경 어댑터와 자동 복구 정책도 등록하지 않았다. T4는 소유 대역으로 UI 흐름을 먼저 검증한다.
- 주의: JSON 원문/실제 SID/원래 설정 bytes는 익명 내보내기에 넣지 않는다. 원래 값과 일치하는 복구는 `AlreadyOriginal`(Started=false), 다른 값은 `CurrentValueChanged`, 완료 전 끊긴 실행은 Pending/Restoring으로 표시해야 한다. 늦은 네이티브 종료 전에는 공통 관문·저장소 잠금을 풀지 않는다.
- `RollbackStore`는 현재 HKLM 등록 프로필과 기본 LocalAppData가 일치해야 한다. 다른 위치로 이동된 프로필 캐시/느슨한 기존 ACL은 지원 미확인으로 거절한다. 관리자 공격자/오프라인 변조 방어 또는 실제 전원 차단 실험 통과를 주장하지 않는다.
- T6/T9 플랫폼·T12 UAC/다른 계정/.NET 없는 PC/DPI 조건 유지. 배포 ZIP은 Task 1 평가본 그대로다. 다음 작업 전 [공유 원장](reviews/REVIEW_LEDGER.md)과 [진행 기록](superpowers/sp1-progress.md)을 읽는다.

## 이전 기록 — SP1 Task 2 구현 완료 (Codex, 2026-09-27)

- 코드 `cbadd67`, [Task 2 자체 검증 보고서](reviews/2026-09-27-sp1-task2-implementation.md). Release 0경고·0오류, 기본 **1314/1314**, 조회 Smoke **23/23**, 소유 임시 캐시 pip ToolSmoke **1/1**. Task 1·2 모두 독립 리뷰 대기다.
- 검사·사양·기존 공식 캐시 정리를 같은 실행 관문에 연결했다. 화면 밖에서 직접 호출해도 겹치지 않으며 실제 작업/프로세스 종료 전에는 Draining으로 남는다. 창 해제는 실제 작업의 소유권을 해제하지 않는다.
- 신규 조치 모델은 코드 등록·닫힌 대상·SID/세션/범위·5분 만료·일회성 소비·Started/늦은 결과 보존까지 구현했다. **신규 실제 OS 변경 어댑터는 아직 등록하지 않았다.** 기존 공식 도구 명령/허용 목록은 유지했다.
- 다음 작업: **Task 3 — 되돌리기 저장소와 실패 복구 계약**. 원자 Pending 기록, SID·프로필·경로/링크·스키마 경계, 앱이 적용한 현재 값 일치 조건, 재시작 후 미완료 복구를 구현한다. 공유 관문은 한 프로세스 안의 인스턴스이며 외부 도구/다중 앱 인스턴스 잠금이나 프로세스 강제 종료 후 복구를 보장하지 않는다.
- 관련 원장: REV-017 서비스 경계 잔여는 `수정됨·독립 재검증 대기`. REV-010·016·018 회귀 통과는 자체 검증이다. [플랫폼 계약/이월 표](reviews/sp1-platform-contracts.md)의 T6/T9/T12 조건도 그대로다.
- 이번에는 ZIP을 재생성하지 않았다. `dist/sp1-evaluation`은 Task 1 평가본이다. Online·일반 셸 UAC·브라우저·.NET 없는 PC·DPI/GUI 수동 검증은 미실시. 실제 사용자 캐시나 Windows 설정은 변경하지 않았다.

## 이전 기록 — SP1 Task 1 구현 완료 (Codex, 2026-09-27)

- 코드 `11959e0`, [Task 1 자체 검증 보고서](reviews/2026-09-27-sp1-task1-implementation.md). 기본 **1288/1288**, Smoke **23/23**, Debug/Release 빌드 0경고·0오류. 솔루션/4개 프로젝트/publish locked restore 및 실제 ZIP 검증 통과.
- 링크 COM 위임과 승격 셸 거절, 도구별 실행 가능 표시, 사양 볼륨 이름 익명화, 문구·죽은 코드, publish profile·라이선스·해시 검증 구현. 독립 리뷰 대기이며 SP1 전체 구현 완료는 아니다.
- 현재 호스트 Explorer가 승격 상태라 정상적인 비승격 COM 성공·브라우저 토큰은 검증하지 못했다. Smoke는 이 환경의 **거절 경로**를 통과한 것이다. 새 Explorer/브라우저를 승격 실행하는 폴백은 없다.
- [플랫폼 계약 및 이월 책임표](reviews/sp1-platform-contracts.md)를 먼저 읽을 것. 시작 프로그램 쓰기·Update/DO 정리는 문서 조사만으로 활성화하지 않으며 각 Task의 평가 조건을 유지한다.
- 다음 작업: **Task 2 — 공통 조치 모델과 실제 작업 수명 공유**. Scan·Spec·Action·Restore의 서비스 직접 호출 상호 배제, 늦은 종료까지 관문 보존, 계획 소비/만료/세션·SID 경계를 구현한다. 실제 신규 삭제/설정 명령은 Task 2에 등록하지 않는다.
- 평가 ZIP은 `dist/sp1-evaluation/PcOptimizer-v0.2.0-win-x64.zip`(git-ignored). 실제 사용자 캐시 정리·설정 변경·원격 게시 없음. 기존 일반 권한 UAC·다른 관리자 계정·.NET 없는 PC·DPI 검증은 T12 대기.

## 계획 작성 당시 기록 — SP1 계획 (Codex, 2026-09-27)

- [SP1 실행 계획](superpowers/plans/2026-09-27-sp1-actions.md)을 작성했다. A1·A2·B·C·D·F1·F2와 공통 확인·결과·되돌리기, 총 12개 Task. **계획 완료, 제품 코드 구현 미착수**.
- 다음 착수: **Task 1 — SP4 이월 정리와 실행 API 근거 확정**. 링크 쉼표/셸 없는 경우, 도구별 실행 가능 판정, 문구/죽은 코드, 게시·RID/lock·고지·SmartScreen을 먼저 처리한다. 서비스 공통 직렬화는 Task 2, 복구는 Task 3, 실환경 한계는 Task 12로 명시했다.
- [SP1 진행 기록](superpowers/sp1-progress.md)과 [공유 원장](reviews/REVIEW_LEDGER.md)을 읽고 작업한다. 구현자 자체 검증과 독립 재검증을 구분한다. 새 서브에이전트 생성은 이 인계의 필수 조건이 아니다.
- 승인된 단일 프로세스·항상 관리자 전제를 유지한다. 전원/디스플레이는 사용자 대상 API이므로 SystemOnly에서 자동 조치를 허용하지 않는다. 실제 삭제·설정 변경 테스트는 사용자 데이터가 아닌 소유 fixture/별도 평가 환경에서 한다.
- 이전 SP4 검증 결과를 SP1 구현 검증으로 재사용하지 않는다. 이번 작업은 같은 트리의 로컬 병합과 문서 작성이므로 제품 빌드/테스트를 다시 실행하지 않았다. 브랜치 조상 관계·병합 결과·문서 링크·diff 형식을 확인했다.
- 아직 남은 실환경 검증: 실제 일반 권한 브라우저, 다른 계정 UAC, .NET 없는 PC, DPI/키보드/SmartScreen. 배포 완료로 판정하지 않았다.

## 2026-09-27 독립 리뷰 수정 후속

- REV-008~014 구현 커밋 `4c1e93d`. 원장 상태는 **수정됨·재검증 대기**. 재현 테스트·제약·명령은 [수정 기록](reviews/2026-09-27-rev008-014-fixes.md)을 먼저 읽는다.
- 최종 검증: Release 0경고/0오류, 기본 **1169/1169**, Smoke **22/22**, ToolSmoke **1/1**. Online은 이번에 재실행하지 않았다.
- 현재 배포 실행 파일은 `tools/package.ps1`로 만든 `dist/PcOptimizer-v<버전>-win-x64.zip` 안의 `PcOptimizer.exe`(단일 파일, SP4 Task 12). REV-008~014 평가 당시 실행 파일은 `artifacts/win-x64-review-fixes/PcOptimizer.App.exe`(Task 12 이전 이름, 폴더 전체 필요). 아래 `win-x64-ui`와 1122개 수치는 이전 UI 변경의 이력이다. 실행 중인 구버전은 그대로 두었다.
- REV-012는 추가 인자 두 개를 삭제해 기존 승인 스펙으로 맞췄다. 사용자에게 같은 범위 승인을 다시 요구하지 않는다.
- 다음 작업: 수정분 독립 재검증(`5457807..4c1e93d`), REV-015/제품 구성 논의, 수동·별도 환경 검증. Codex는 이번 수정의 구현자이므로 자기 검증을 독립 승인으로 기록하지 않는다.

## SP4 진행 상태 (Claude 세션, 2026-09-27 — 세션 한도 대비 갱신)

- **독립 재검증 후속(Codex, 기준 `0fa2bf5`)**: [최종 재검증 보고서](reviews/2026-09-27-sp4-final-revalidation.md). 기본 1279/1279·Smoke 22/22·ToolSmoke 1/1·이전 독립 재현 원문 4/4, Release 0/0. 원장 REV-010·014~018을 보고서에 명시한 범위에서 검증 완료로 갱신했다. 아래 ‘재검증 대기’는 구현 세션 당시 기록이다. 병합·게시하지 않았고 수동 검증·기존 이월은 남아 있다. SP1 브리프 작성 전에 보고서의 남은 확인·인계 목록을 읽을 것.

- 2차 개선 스펙: `docs/superpowers/specs/2026-09-27-improvement-phase2-design.md`(사용자 승인). 계획: `docs/superpowers/plans/2026-09-27-sp4-format-and-admin.md`(Task 1~12).
- SDD 원장(룰링·라운드 기록): `.superpowers/sdd/2026-09-27-sp4-format-and-admin/progress.md`(git-ignored). 브리프는 같은 폴더 `task-N-brief.md`, 보고서 `task-N-report.md`.
- 진행: **SP4 전체 완료(Task 1~13, 최종 전체 브랜치 리뷰 후속 수정 포함)**. 마지막 코드 커밋 `ec3547e`(링크 비승격 실행·사양 프로브 백그라운드 실행·검사 전 타일 숨김·범위 판정 불가 배너·자식 프로세스 환경 정리), 원장 `d3d4641`. 실행 순서는 1~9 → 10 → 13 → 11 → 12 → 최종 리뷰 → 후속 1라운드였다.
- 검증(최종 후속 시점): Release 빌드 0경고/0오류, 기본 필터 1279/1279, Smoke 22/22. Online·ToolSmoke는 이번 단계에서 미실행(수집 코드 변경 없음).
- 배포: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package.ps1` → `dist/PcOptimizer-v0.2.0-win-x64.zip`(최상위 4개: `PcOptimizer.exe`·`실행방법.txt`·`LICENSES/`·`rules/`). dist/는 git-ignored이며 GitHub Release에 첨부한다. 아이콘은 `tools/make-icon.ps1` 산출물 `src/PcOptimizer.App/Assets/app.ico`(교체 시 이 파일만).
- 원장 상태: REV-014~018 모두 `수정됨·재검증 대기`(구현 세션 자체 검증). 독립 재검증(Codex 등)이 남아 있다. 최종 리뷰 보고서 사본: `docs/superpowers/sdd-final-review-2026-09-27-sp4.md`.
- 이월(최종 리뷰 6~12 + 각 Task 이월): 도구별 실행 가능 판정, 게시 속성의 일반 빌드 적용·Core/Probes lock 파일 win-x64 섹션 churn, SmartScreen 안내, 요약 타일 제거 후 죽은 코드, 예전 전제 문구·말투 혼용, `%TEMP%\.net` 네이티브 DLL 추출(코드 서명 도입 시 재검토), PackagingTests 경로 탐색, Spec_BusyScanning 문구, 서비스 계층 상호 배제, Entra 계정 SID 변환 실패 시 셸 토큰 기반 대안, explorer.exe 인자에 쉼표가 있는 URL 방어, 셸(explorer) 미실행 시 GetShellWindow 검사·IShellDispatch2 전환, Banner_ScopeUnknown 시제, 실패 안내 AbsoluteUri 표기, MainWindow.xaml 중복 주석.
- 수동 검증 미완: 일반(비관리자) 셸에서 UAC 프롬프트, 표준 계정+다른 관리자 자격 증명 승격(SystemOnly 배너·정리 버튼 비활성), 판정 불가 배너, 링크가 실제로 일반 권한 브라우저로 열리는지, Program Files에 Python만 있는 PC의 정리 버튼 노출, .NET 미설치 PC에서 단일 파일 exe 실행, 탐색기 아이콘 육안, SmartScreen 경고, DPI 100/150/200%·키보드 조작, WMI가 멈춘 PC의 사양 화면.
- 다음: 사용자 결정 — (1) `feature/p0-skeleton`을 master에 병합할지(원격 게시 없음), (2) SP1(조치 A1·A2·B·C·D·F1·F2) 계획 작성 착수. SP2 고급 카탈로그는 커뮤니티 권장 옵션 목록을 사용자가 검토·확정한 뒤 진행.
- SDD 원장 사본: `docs/superpowers/sdd-progress-2026-09-27-sp4.md`(룰링·라운드·이월 minor). 원본 `.superpowers/sdd/2026-09-27-sp4-format-and-admin/progress.md`.
- 재개 시: SP4는 끝났다. 사용자에게 병합 여부를 묻고, SP1 계획(writing-plans)부터 시작한다. 잔여 이월 minor는 SP1 첫 Task로 편입.
- 사용자 확정: UI 배치안(scratchpad 목업, 좌측 메뉴 7개·타일 2개·설명 3줄 카드), 내 PC 사양은 fastfetch식 한 열 나열(CPU·메인보드·GPU·RAM·SSD·HDD·모니터 이름 전부), 배포는 GitHub zip 포터블(단일 파일 exe·아이콘, Task 12).
- 재개 방법: 원장의 마지막 `Task N:` 줄을 보고 그 다음 작업을 subagent-driven-development로 파견. 파견 전 `git status`로 Codex 미커밋 변경 확인(현재 Codex 유휴).
- 검증 명령 기본 필터: `Category!=Smoke&Category!=Online&Category!=ToolSmoke`.

## 현재 상태

- 사용자 피드백에 따라 첫 화면을 추천 조치 중심으로 개편했다. `MainViewModel.Overview.cs` 및 두 XAML이 주 변경 범위이며, 전체 결과와 원본 리포트는 보존한다. 상세 근거는 원장의 2026-09-27 UI 대응 기록을 읽을 것.

- P6 소스 검토·기본/온라인 검증 및 링크 정책 수정 완료.
- REV-002/003/006/007 수정 및 관련 회귀 통과. 원장 상태는 **수정됨·재검증 대기**(별도 독립 리뷰 없음).
- REV-004 사용자 결정 완료: **1차 자동 조치 포함**, 범위는 **npm·pip·NuGet HTTP 공식 캐시 정리와 Windows 정리 도구 연결**. 다른 자동 조치까지 승인받았다고 확대하지 말 것.
- P7 통합 코드·자동 검증·self-contained 평가 배포물 생성. 실제 수동/별도 환경 검증은 남아 있음.
- 배포 실행 파일: `tools/package.ps1` → `dist/PcOptimizer-v<버전>-win-x64.zip`의 `PcOptimizer.exe`(단일 파일, 런타임 포함). 이전 UI 평가 배포 폴더 `artifacts/win-x64-ui`는 이력이며 지금은 없다.
- 최신 UI 흐름 검증: 기본 **1122/1122**, 실제 조회→화면 모델→익명화 스모크 **1/1**. 전체 Smoke 21·Online 3·ToolSmoke 1은 이전 실행 수치이며 이번 변경에서 전부 재실행한 것은 아니다.
- 목적별 바로가기, 온라인 미조회 구분, 캐시 실행 직전/직후 결과 비교 및 메인 화면 유지 추가. 테스트 중 개인 캐시는 정리하지 않았다. 원장의 UI 후속 대응 기록을 읽을 것.

## 먼저 읽기

1. `AGENTS.md`, `docs/reviews/REVIEW_LEDGER.md` — 관련 REV와 대응 기록을 항상 확인.
2. `docs/superpowers/specs/2026-09-27-first-release-actions.md` — 사용자 변경이 기존 조회 전용/자동 조치 이월 문구에 우선한다.
3. `docs/reviews/2026-09-27-validation.md` — 검증 명령/범위/미검증 항목.
4. 기존 설계·계획과 `docs/superpowers/sdd-progress-2026-09-26.md` — 이월 minor 이력. 전부 해결된 것으로 취급하지 말 것.

## 다음 순서

1. 새 실행기 `src/PcOptimizer.Probes/Actions`와 UI `CacheTools*` 및 REV 수정분을 별도 검토한다. 현재 사용자 프로필/일반 권한 제한, 일회성 계획·만료·실행 전 재검증·실행 후 결과, 실패/시간 초과 경계를 우선 확인한다.
2. 실제 일반 권한 UI(확인 취소 포함), UAC/다른 SID, 키보드·DPI, .NET 없는 별도 PC에서 평가한다. 사용자 캐시를 테스트 목적으로 임의 정리하지 않는다.
3. 기존 minor를 분류해 P7 최종 리뷰에 기록한다. 특히 익명화 구조 문자열/경로 경계와 프로세스 종료 실패 경로를 검토한다.
4. 모든 조건이 갖춰진 뒤 출시·브랜치 마무리를 판단한다. 현재는 평가 빌드이며 master 병합/원격 게시 없음.

## 명령

기본 필터는 이제 **`Category!=Smoke&Category!=Online&Category!=ToolSmoke`**. 필터 없는 test는 실제 PC·온라인·도구 실행까지 포함하므로 기본 검증에 쓰지 않는다. `ToolSmoke`는 테스트용 `PCOPTIMIZER_TEST_PYTHON` 지정이 필요하다. 실행 예시는 루트 README 참고.

외부 규칙은 어셈블리 포함 리소스로만 읽는다. 배포 폴더의 rules 사본은 출처/라이선스 확인용이다. 자동 실행은 외부 규칙 문자열을 명령으로 쓰지 않는다. 테스트의 실제 정리는 소유한 임시 fixture로만 제한한다.
