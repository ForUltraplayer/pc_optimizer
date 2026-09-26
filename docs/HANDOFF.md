# HANDOFF — PC Optimizer (2026-09-27)

브랜치 `feature/p0-skeleton`, master 미병합. 이전 인계는 커밋 `9aa45b3`에 보존되어 있다. Codex가 사용자 지시로 구현을 인계받았으며 독립 리뷰 역할만 수행하던 이전 상태와 구분한다.

## 2026-09-27 독립 리뷰 수정 후속

- REV-008~014 구현 커밋 `4c1e93d`. 원장 상태는 **수정됨·재검증 대기**. 재현 테스트·제약·명령은 [수정 기록](reviews/2026-09-27-rev008-014-fixes.md)을 먼저 읽는다.
- 최종 검증: Release 0경고/0오류, 기본 **1169/1169**, Smoke **22/22**, ToolSmoke **1/1**. Online은 이번에 재실행하지 않았다.
- 현재 배포 실행 파일은 `tools/package.ps1`로 만든 `dist/PcOptimizer-v<버전>-win-x64.zip` 안의 `PcOptimizer.exe`(단일 파일, SP4 Task 12). REV-008~014 평가 당시 실행 파일은 `artifacts/win-x64-review-fixes/PcOptimizer.App.exe`(Task 12 이전 이름, 폴더 전체 필요). 아래 `win-x64-ui`와 1122개 수치는 이전 UI 변경의 이력이다. 실행 중인 구버전은 그대로 두었다.
- REV-012는 추가 인자 두 개를 삭제해 기존 승인 스펙으로 맞췄다. 사용자에게 같은 범위 승인을 다시 요구하지 않는다.
- 다음 작업: 수정분 독립 재검증(`5457807..4c1e93d`), REV-015/제품 구성 논의, 수동·별도 환경 검증. Codex는 이번 수정의 구현자이므로 자기 검증을 독립 승인으로 기록하지 않는다.

## SP4 진행 상태 (Claude 세션, 2026-09-27 — 세션 한도 대비 갱신)

- 2차 개선 스펙: `docs/superpowers/specs/2026-09-27-improvement-phase2-design.md`(사용자 승인). 계획: `docs/superpowers/plans/2026-09-27-sp4-format-and-admin.md`(Task 1~12).
- SDD 원장(룰링·라운드 기록): `.superpowers/sdd/2026-09-27-sp4-format-and-admin/progress.md`(git-ignored). 브리프는 같은 폴더 `task-N-brief.md`, 보고서 `task-N-report.md`.
- 진행: **Task 1~11·13 완료. 다음은 Task 12(포터블 zip·아이콘)뿐이다.** 실행 순서는 10 → 13 → 11 → 12였다. Task 10(0c80a8b..f432d5a — SystemOnly/사양 읽기 중 정리 진입 차단, 실행기 UserScopeExcluded, NormalUserRequired 제거→보호 위치 도구만(시스템 폴더 API), 도구 작업 폴더 System32 고정, 노출 판정을 실행 규칙과 통일). Task 13(3d286be·2d140d7·be6e99a — 사양 프로브 재진입 차단·IsDraining·CacheProcessGuard AggregateException, 결과 화면에 사양 종료 대기 사유 표시). Task 11(문서·원장·검증 마무리 및 이월 minor 편입: `Cleanup_ToolUserWritable` 문구, 스펙 §0 정정, 원장 REV-016~018 재확인 절)에서 빌드 0/0·기본 1247/1247·Smoke 22/22·publish·배포 EXE 실행(창 생성, ExitCode=0, `AppStarted elevated=True scope=Full sessionUserResolved=True`)을 구현 세션 자체 검증으로 확인했다. 모든 REV 상태는 구현자 자기 검증이며 `수정됨·재검증 대기`이고, 독립 재검증 전에는 `검증 완료`로 바꾸지 않는다.
- SDD 원장 사본: `docs/superpowers/sdd-progress-2026-09-27-sp4.md`(룰링·라운드·이월 minor). 원본 `.superpowers/sdd/2026-09-27-sp4-format-and-admin/progress.md`.
- 재개 시: 원장 마지막 `Task N: complete` 줄 다음 순서로 파견. 다음은 Task 12다. 미검증 수동 항목: 일반(비관리자) 셸에서의 UAC 프롬프트, 표준 계정이 다른 관리자 자격 증명으로 승격했을 때 SystemOnly 배너·정리 버튼 비활성화, Program Files에 Python만 있는 PC에서 정리 버튼 노출, .NET 미설치 PC, 모니터 DPI·키보드 전환.
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
