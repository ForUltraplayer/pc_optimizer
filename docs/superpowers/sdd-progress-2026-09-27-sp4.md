# SDD ledger — plan: docs/superpowers/plans/2026-09-27-sp4-format-and-admin.md
Spec: docs/superpowers/specs/2026-09-27-improvement-phase2-design.md
Branch: feature/p0-skeleton (base e1c965c). Codex 세션과 저장소 공유 — 파견 전 git status 확인.
Ruling: 기존 브랜치에서 계속(별도 worktree 없음) — 1차와 동일 저장소·검토 흐름 유지 — 틀리면 브랜치 분리 재작업

## Preflight scan
| 검사 | 결과 |
|---|---|
| Task1 모델 ↔ Task2/3/4 소비 | Explanation/SafetyLevel/Finding 선택 매개변수 시그니처 동일. Task3에서 불변식 전환 시 Task2 완료 필수(순서 보장) |
| Task5 IActionAvailability ↔ Task8/9 MainViewModel 생성자 | 생성자 매개변수가 Task5(availability), Task8(spec), Task9(userScope, relauncher 제거)에서 순차 변경 — 각 태스크가 이전 시그니처를 읽고 갱신 |
| Task6 계약 ↔ Task7 빌더 | SystemDetailsProbeContract 상수명 동일 |
| Task9 삭제 파일 ↔ Task10 CacheTools 승격 조건 | Task9는 IElevationState 유지, Task10이 NormalUserRequired 제거 — 충돌 없음 |
| UI 참고 배치(스펙 §3.1) | Task4·5·8 전에 사용자 확정 필요 — 진행 중 |
스캔 결과: 실행 전 룰링 필요한 충돌 없음.

## Task log
Task 1: dispatched (sonnet, base e1c965c)
UI 참고 배치 목업(scratchpad/ui-layout-mockup.html)을 사용자에게 전달 — 참고: Microsoft PC Manager(좌측 내비+홈 카드), Windows 설정(좌측 목록+우측 카드), CCleaner(큰 주 버튼). Task 4·5·8 착수 전 확정 대기.
Task 1: implementer DONE (commit aa3ff7d, 1175/1175)
UI 배치 사용자 확정: 목업대로 진행, 단 내 PC 사양은 fastfetch식 한 열 나열(칸 없음). 계획 Task 8·스펙 §3.2 반영 커밋.
Task 1: review (sonnet) Approved. Important(plan-mandated): ArgumentException 메시지 한국어 하드코딩 vs 리소스 규칙.
Task 1: Ruling: 예외 메시지는 사용자 화면에 노출되지 않는 개발자 진단 문자열(스펙 §8: 원시 예외를 화면에 내지 않음)이므로 리소스 대상 아님 — 기존 FindingInvariantException도 동일 — 틀리면 리소스 이동 소규모 작업
Task 1: minor (deferred): 공백 전용·61자 경계 테스트 케이스 없음
Task 1: complete (commits e1c965c..aa3ff7d, review clean; 트레일러 컨트롤러 확인)
Task 2: dispatched (sonnet, base 334acbd)
사용자 지시: 사양 목록에 CPU·메인보드·GPU·RAM·SSD·HDD·모니터 이름/해상도 전부 나열. Task 6에 Win32_BaseBoard 추가, Task 7 빌더 규칙 보강. 브리프 6·7 재생성.
Task 2: implementer DONE (commit 0113f99, 1183/1183; xUnit1026 회피로 name 단언 추가)
사용자 지시: 구버전 산출물 정리 → artifacts/win-x64, win-x64-ui 삭제(최신 win-x64-review-fixes 유지, 스크린샷·trx 유지). GitHub zip 포터블 배포·파일 수 최소·아이콘 → 스펙 §7A, 계획 Task 12. Codex 유휴 확인. 사용자 지시: 5시간 세션 한도 유의, 한도 시 HANDOFF 남기고 대기 → 각 작업 완료마다 docs/HANDOFF.md 갱신.
Task 2: review (sonnet) Approved w/ Important: 5개 테스트 파일에서 CandidateSnapshot() 복제(추출 아님). fix round 1/5 dispatched (구현자 재개).
Task 2: fix round 1/5 implementer DONE (commit fb5fae3, 1183/1183)
Task 2: fix round 1/5 (1 addressed, 0 open; commits c9e9df1..fb5fae3)
Task 2: complete (commits 334acbd..fb5fae3, review clean after round 1)
Task 3: dispatched (sonnet, base fb5fae3)
Task 3: implementer DONE (commit 332ffb0, 1184/1184; 레이아웃 테스트 fixture의 마스킹 결함도 수정)
Task 3: review (sonnet) Approved. minor: Finding.cs @brief '선택적으로' 문구 구식; 권고: 레이아웃 테스트에 FailedRuleCount==0 가드 → Task 4 파견에 포함
Task 3: complete (commits 6e1a382..332ffb0, review clean)
Task 4: dispatched (opus, base 332ffb0)
Task 4: implementer DONE (commit 4b0b868, 1186/1186). 이탈: 레이아웃 테스트 줄 수(카드당 3줄로 일반화), 규칙 실패 가드는 rule-failure Finding 접두 부재로 대체, CanShowDetails에 HasImpact 포함, 트레일러를 실제 모델(Opus 5.5)로 기록(제약 문서 규칙).
Task 4: review (opus) Approved. minor (deferred): 배지 AutomationName이 '주의' 단독(설명 라벨과 혼동) → '안전 수준: 주의' 형식 권고; 배지 색 리터럴 중복; 본문에서 Evidence/SideEffect 제거를 단언하는 테스트 없음, Irreversible 매핑 미테스트; 그리드 행 간격 불균일; VM 테스트가 CreateCard 헬퍼 미사용
Task 4: complete (commits 3d007b0..4b0b868, review clean)
Task 5: dispatched (opus, base 4b0b868)
Task 5: implementer DONE_WITH_CONCERNS (commit e58cae9, 1205/1205). 이탈: IActionAvailability.CacheToolsAvailable 추가, OnlineComparisonComplete를 driver-update: Finding으로 한정+회귀 테스트, 정리 버튼 별도 행, 앱 id npm/pip/nuget(대소문자 무시). 노트: AppCacheRule이 Info만 내므로 현재 DoNow 타일은 항상 숨김(SP1에서 실행 정의 시 채워짐); 보호 위치 검사가 nodejs/dotnet만 → Task 10에서 보강.
Smoke 실패 분석: ScanServiceSmokeTests.실제_검사_결과를_익명화해_내보낸다 — 익명화 JSON에 원본 식별자 0건, 디스플레이 ID는 display-refresh:adapter:pnp-1|target:4353으로 정상 토큰화. 실패 원인은 테스트가 모니터 경로 토큰 'display-1'을 가정한 것(이 PC 모니터 상태 변화: 1대만·모니터 경로 없음·59Hz). 코드 회귀 아님 → Task 6 파견에 테스트 가정 완화(디스플레이 finding 존재 + 원본 VEN_/DISPLAY# 부재 단언) 포함.
Task 5: review (opus) Approved. minors (deferred): 실시간/캐시 도구 검사 불일치(Lazy 권고), IActionAvailability에 CacheToolsAvailable 혼재(SP1 분리 고려), 라벨 기반 검토 판정 문서화, 보호 위치 검사 범위(pip/x86) → Task 10, 검사 전 '0건' 문구, 죽은 코드(ShowSettings/ShowDrivers/ResultsAnchor/Overview_EmptyAfter), 알림 테스트 약함. 원장 문구 정정은 컨트롤러가 처리.
Task 5: complete (commits 031e9da..e58cae9, review clean)
Task 6: dispatched (sonnet, + 스모크 단언 완화)
Task 6: implementer DONE (commit 6297834, 1209/1209, smoke 22/22 — 스모크 단언 완화 포함)
Task 6: review (sonnet) Approved. minor (deferred): Win32_BIOS 쿼리가 SystemInfoProbe와 중복(공유 BiosReader 권고, 이월 minor 누적), 물리 NIC 0개는 Issue 아님(비대칭·문서화 필요), AddInteger의 Convert 예외는 실행기가 흡수.
Task 6: complete (commits c5e4692..6297834, review clean; Smoke 22/22 복구)
Task 7: dispatched (opus, base 6297834)
Task 7: implementer DONE_WITH_CONCERNS (commit 3049044, 1218/1218). 우려: 단위 없는 메모리 속도를 MT/s로 표기, 무명 모니터 fallback 미사용, 추가(전원 공급 줄·제조사 대체 마커·드라이브 문자 없는 볼륨), App 미배선(Task 8), 프로브 인스턴스 공유로 검사와 동시 실행 가능 → Task 8에서 검사 중 새로 고침 차단 룰링.
Task 7: review (sonnet) Approved w/ Important: PcSpecService.cs 711줄 → partial 분할. minor: 0값 가드 테스트 공백, Disks/Volumes 카운트 0 처리 비대칭. fix round 1/5 dispatched.
Task 7: fix round 1/5 implementer DONE (commit 3a60fe4, 1219/1219; 분할 401+339줄, 0값 테스트, 카운트 0→없음 통일)
Task 7: fix round 1/5 (2 addressed, 0 open; commits 3049044..3a60fe4)
Task 7: complete (commits 63baae9..3a60fe4, review clean after round 1)
Task 8: dispatched (opus, base 3a60fe4)
Task 8: implementer DONE (commit fb16e3f, 1231/1231; 실제 앱 PNG 1124x1138·TXT 9섹션 33줄 익명화 확인). 이탈: SaveFileDialogExportPathPicker에 TXT/PNG 필터 추가, PNG 렌더 오프셋 결함 발견·수정(테스트 강화), 트레일러 Opus.
Task 7 data (deferred, Task 11 또는 최종 리뷰): 메모리 모듈 슬롯 라벨이 DeviceLocator만 써서 동일 표시('DIMM 1' 2개) → BankLabel 병기; 보드 리비전에 벤더 자리표시 값('Rev 1.xx' 류) 노출 → 자리표시 목록 확장.
Task 8: review (opus) Needs fixes — Important: 첫 사양 로드 동안 토글 버튼 비활성(최대 2분). fix round 1/5 dispatched (Important + Minor#1 테스트 가드, #2 busy 해제 시 자동 로드, #3 OpenCacheTools CanExecute 가드).
Task 8: minor (deferred): 익명화 출력에 볼륨 사용자 라벨 포함(Task 7 데이터, 개인정보 후속), PcSpecViewModel 424줄·MainViewModel 512줄(partial 분리 권고), 헤더 타임스탬프 CurrentCulture, CopyText/SaveText 재계산, 테스트 상수 위치, 'C: 드라이브:' 이중 콜론, 미사용 ShowSpec/ShowResults 명령
Task 8: fix round 1/5 implementer DONE (commit 1d24cbf, 1234/1234)
Ruling: 커밋 트레일러는 실제 작성 모델을 기록(sdd-global-constraints 추가 규칙) — 이후 파견에서 Fable 강요 안 함 — 틀리면 트레일러 표기 불일치뿐
Task 8: fix round 1/5 (4 addressed, 0 open; commits fb16e3f..1d24cbf)
Task 8: complete (commits 6aa8629..1d24cbf, review clean after round 1)
SESSION PAUSE 2026-09-27: 사용량 한도. 다음: Task 9(항상 관리자 권한 전환) → 10 → 11 → 12. 파견 전 git status 확인.
