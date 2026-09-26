# HANDOFF — PC Optimizer (2026-09-26, 사용량 한도로 중단)

이 문서는 다음 세션이 이어받기 위한 인계서다. 이 문서 하나로 현재 상태·남은 일·재개 절차를 파악할 수 있어야 한다.

## 1. 현재 상태 한 줄

브랜치 `feature/p0-skeleton`(master 미병합), P0~P5 구현·태스크 리뷰 통과, **P6 구현 완료·리뷰 미실시**, P7 미착수. 마지막 컨트롤러 검증(P5 완료 시점 96a593c): Release 빌드 경고 0/오류 0, 기본 테스트 803/803, 스모크 18/18. P6 구현자 자체 보고: 기본 1085/1085, 스모크 18/18, 온라인 3/3.

## 2. 반드시 먼저 읽을 파일

1. `docs/reviews/REVIEW_LEDGER.md` — 독립 검토자(Codex, 사용자가 별도 세션으로 운영)의 공유 원장. **사용자 지시: 항상 참고하며 작업.** 구현·리뷰·완료 보고 전에 읽고, 고친 항목은 `대응 기록`을 쓰고 상태를 `수정됨·재검증 대기`로만 바꾼다(`검증 완료`는 검토자만).
2. `AGENTS.md`, `CLAUDE.md`(프로젝트) — 위 원장 규칙의 프로젝트 안내.
3. `docs/superpowers/specs/2026-09-26-pc-optimizer-design.md` — 설계 스펙(승인본 + 검토 수정 + §13 부록).
4. `docs/superpowers/plans/2026-09-26-pc-optimizer-implementation-plan.md` — P0~P7 계획. 체크박스는 태스크 리뷰 통과 기준.
5. `docs/superpowers/sdd-progress-2026-09-26.md` — SDD 컨트롤러 원장 사본(모든 Ruling, 라운드별 결과, 이월 minor 목록). 원본은 git-ignored `.superpowers/sdd/2026-09-26-pc-optimizer-implementation-plan/progress.md`.
6. `docs/superpowers/sdd-global-constraints.md` — 서브에이전트 공통 제약 사본(파일 헤더, 모델 불변식, 커밋 규칙, 원장 규칙).

## 3. 즉시 처리할 일 (우선순위순)

1. **P6 태스크 리뷰** — 범위 `6451f31..5231f28`(커밋 8180165 구현, 5231f28 원장). 구현자 보고서: `.superpowers/sdd/.../task-P6-report.md`(없으면 git에는 없음 — 구현자 보고 요약은 아래 §5). 리뷰 시 판단할 구현자 이탈 4건: Studio 목록 요청 실패 시 Game Ready 목록에 있어도 Ambiguous 처리, LinkPolicy가 NVIDIA URL을 호스트만으로 신뢰·정규화된 URL을 열음, 노트북 GPU 이름은 접두 추정 없이 미매칭, `ms-settings:windowsupdate`(스펙 §13은 `-optionalupdates`).
2. **REV-006, REV-007 (P5 결함, 검토자 재현 완료)** — 정션 아래 설정 파일 본문을 사전 차단 없이 읽음 / 접근 거부된 설정 파일을 "설정 없음"으로 처리. 원장에 재현 테스트·수정 요청·완료 조건 기록됨(`docs/reviews/repro/`). P5 fix round 4로 처리(P5 구현자 컨텍스트는 소실, 새 구현자 파견).
3. **REV-002 경계 보류** — 스캔의 마지막 OS 조회에서 예산 초과 시 `TimedOut=false`. P7에서 종료 시점 예산 상태 검증으로 마무리.
4. **REV-003** — "종료 중" 안내가 다음 검사까지 잔존. P7에서 실행 상태 변경 알림으로 수정(룰링).
5. **REV-004 (사용자 결정 3건)** — 스펙 §13 표 기준: 1차 연결(설정 URI·공식 링크) 포함 여부, 2차 자동 조치 대상(검토 규칙 중 실행 검증 조건 정한 것만, Squirrel·NuGet 제외), 커뮤니티 규칙 관측 결과 표시 방식. 결정 전까지 P7 배포 범위 확정 불가.
6. **P7** — 통합 검증·배포(계획 P7 항목 + 위 3·4 + 비관리자 실제 실행, UAC 취소 흐름, 실제 정션, 100/150/200% DPI, self-contained 배포).
7. **최종 전체 브랜치 리뷰** — SDD 절차상 P7 후 최상위 모델로 1회, 이월 minor(sdd-progress의 `minor (deferred)` 줄 전부) triage, 그 뒤 `finishing-a-development-branch`.

## 4. 재개 절차

```powershell
cd C:\Users\Administrator\Desktop\pc_optimizer
git status; git log --oneline | head -20
"C:\Program Files\dotnet\dotnet.exe" build PcOptimizer.sln --configuration Release
"C:\Program Files\dotnet\dotnet.exe" test PcOptimizer.sln --configuration Release --no-build --filter "Category!=Smoke&Category!=Online"
"C:\Program Files\dotnet\dotnet.exe" test PcOptimizer.sln --configuration Release --no-build --filter "Category=Smoke"
# 온라인(네트워크 필요, 명시 실행): --filter "Category=Online"
```

SDD 방식으로 이어갈 때: `.superpowers/sdd/2026-09-26-pc-optimizer-implementation-plan/`가 없으면 `docs/superpowers/sdd-progress-2026-09-26.md`를 `progress.md`로, `sdd-global-constraints.md`를 `global-constraints.md`로 복사해 재생성한다. 태스크 브리프는 계획 문서의 `## P6.`/`## P7.` 절을 그대로 쓴다. 리뷰 패키지는 superpowers 스킬의 `scripts/review-package PLAN BASE HEAD`.

## 5. P6 구현자 보고 요약 (리뷰 전, 미검증 주장)

- NVIDIA 조회: `lookupValueSearch`(TypeID=3)로 psid/pfid, `DriverManualLookup`로 Game Ready 목록, Studio는 `upCRD=1` + `isWHQL=0` 조합에서만 반환(라이브 확인). 이 PC 설치 616.56이 두 목록 모두에 있어 Ambiguous → 617.14를 후보로 표시하지 않음(스펙대로).
- WUA: late-bound COM 비동기 검색, 실제 검색 결과 드라이버 후보 3개. 다운로드·설치 API 없음.
- vendor-links.json 임베드, OemSupportRule, LinkPolicy(https + 허용 호스트), OpenLink 버튼, 온라인 확인 시각.
- 온라인 실행 후 "온라인 미요청 시 스킵" 가드를 프로브에 추가했고 그 뒤 온라인 테스트는 재실행하지 않음.
- 기본 필터가 `Category!=Smoke&Category!=Online`으로 바뀜(계획 §3 명령 갱신 여부 확인 필요).

## 6. 주요 룰링 (전체는 sdd-progress 참고)

- 메모리: 설정 속도 < 보고 속도일 때만 조건부 Candidate, "정격 미달·XMP 꺼짐" 단정 금지.
- 취소: 즉시 반환, 토큰 무시 프로브는 draining + IsStillRunning.
- Issue.Summary에 예외 원문 금지(형식 이름만).
- P3 분할(P3a/P3b), 규칙·정책 파일은 Probes 임베드 리소스(파일 시스템 미읽기).
- 앱 캐시 카드: 앱 단위(Section 텍스트; `Games`·`Adobe`와 100개 초과 Section은 카테고리로 간주해 규칙 이름), 0바이트·병합·부재만 요약에 접고 관측 실패는 앱 단위 CannotVerify.
- 자동 격리 후보(2차): 실행 검증 조건을 정한 규칙만, Squirrel 버전 폴더·NuGet 전역 패키지 제외. 탐색기 열기 1차 보류.

## 7. 알려진 미검증 사항

비관리자 실제 실행·UAC 취소 흐름, 실제 NTFS 정션·하드링크 E2E(스모크 일부만), ProfileList 없는 머신, 대용량 실제 폴더 타이밍, WPF 화면 수동 조작·배치, 메모리 단위 SMBIOS 3.0~3.1 경계, rule-metadata 참조 URL, Slack/GitHub Desktop Squirrel 레이아웃(Discord만 검증).

## 8. 환경

.NET SDK 10.0.401(winget 설치), Visual Studio 없음, nuget.org 소스는 P0 구현자가 머신 전역에 추가. 셸은 관리자 권한. 커밋 정체: `-c user.name=rudals252 -c user.email=jwr300028@gmail.com`, 트레일러 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
