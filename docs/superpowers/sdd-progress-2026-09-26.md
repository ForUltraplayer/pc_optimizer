# SDD ledger — plan: docs/superpowers/plans/2026-09-26-pc-optimizer-implementation-plan.md
Spec: docs/superpowers/specs/2026-09-26-pc-optimizer-design.md
Branch: feature/p0-skeleton (base 77efa2e)
Ruling: 별도 worktree 대신 메인 체크아웃의 feature 브랜치에서 작업 — 저장소가 신규이고 병행 작업이 없음 — 틀리면 브랜치 격리 부족으로 다른 작업과 섞일 위험(현재 없음)

## Preflight scan
| 검사 | 결과 |
|---|---|
| P1 모델 ↔ P2~P6 소비 | P1이 Finding/ProbeResult/IRule을 정의, 이후 태스크는 소비. 계획·스펙 §3 일치 |
| P4 공유 스캔 ↔ P5 앱 캐시·미분류 | P4 산출 스캔 결과를 P5와 미분류가 재사용. 스펙 §5.2 일치 |
| P2 메모리 문구 ↔ 스펙 §5 메모리(절충안) | 계획 "XMP 필요/적용 단정 금지"는 절충안과 양립. 조건부 Candidate는 스펙이 우선 |
| P3 TRIM(fsutil) 권한 | 스펙은 읽기 전용 호출이지만 fsutil은 관리자 필요 → ElevationRequired 처리. 모순 아님 |
| P0 WPF-UI .NET 10 호환 | 계획이 P0에서 버전 검증을 요구. 미확인 위험이나 P0 범위 내 |
| 각 태스크 자기 일관성 | P0~P7 완료 조건이 항목과 일치. 검사 항목 12분류 ↔ P3~P6 분담 누락 없음 |
스캔 결과: 실행 전 룰링 필요한 충돌 없음.

## Task log
Task P0: implementer DONE (commit 4e54f87). 구현자 노트: nuget.org 소스를 머신 전역에 추가함(기존 소스 없음). supportedOS GUID는 Win10/11 공용 하나만 사용.
Task P0: minor (deferred): 각 csproj가 Directory.Build.props와 중복으로 Nullable/ImplicitUsings 재선언
Task P0: minor (deferred): CoreAssemblyReferenceTests의 [Fact] 메서드에 한글 XML 주석 없음
Task P0: minor (deferred): Tests.csproj와 packages.lock.json 4개 파일 끝 개행 없음
Task P0: complete (commits 77efa2e..4e54f87, review clean; 트레일러·작성자는 컨트롤러가 git show로 확인)
Task P1: implementer DONE_WITH_CONCERNS (commit 813fe72, 54/54 tests). 추가 산출: ProbeSummary.IsStillRunning, ScanResult(Report+Snapshot), 내부 ProbeExecutor/ScanLogEvents 분리(허용).
Task P1: Ruling: 사용자 취소 시 RunScanAsync는 즉시 반환하고, 토큰을 무시하는 프로브는 draining 집합에 남겨 Cancelled + IsStillRunning으로 표시 — 스펙 §4 "취소 시 새 예약 중단·기존 결과는 부분 검사" 및 "종료 중 표시"에 부합 — 틀리면 UI가 실제로는 끝나지 않은 프로브를 다룰 부담이 커짐
Task P1: Ruling: Issue.Summary에 예외 메시지 원문 금지, 예외 형식 이름과 요약 코드만 — 스펙 §8 개인정보 치환 요구 — 틀리면 디버깅 정보 손실(로그는 형식 이름만이라 동일)
Task P1: note for P2: 한국어 템플릿 상수를 리소스로 이동, ProbeSummary.IsStillRunning/DrainingProbeIds를 UI에 표시. note for P4: 볼륨당 순회 1개 제한 미구현(훅 없음)
Task P1: fix (pre-review) 636601c — 취소 즉시 반환(grace 250ms), Issue.Summary 예외 형식명만, 취소 시 슬롯 재확인 레이스 수정. 54/54.
Task P1: review (opus) Needs fixes — Important: 취소 테스트가 기본 grace 250ms 타이머에 의존(ScanCoordinatorTests.cs:177,187). Minor 13건.
Task P1: Ruling: Minor #1(null Measurements/Issues가 전체 스캔을 깨뜨림)은 스펙 §4 "한 프로브 실패가 다른 결과를 버리지 않는다" 보장 위반이므로 Important로 승격해 이번 라운드에 포함 — 틀리면 불필요한 방어 코드 약간
Task P1: Ruling: Minor #7(UserContext.Sid 미사용 필드)은 룰링 범위 밖 Extra이고 §8 민감 식별자이므로 이번 라운드에 제거 — 틀리면 P3 승격 비교 시 다시 추가 필요(그때 추가하면 됨)
Task P1: minor (deferred): 취소 후 Task.Run 직전 재확인 창(ProbeExecutor.cs:107)
Task P1: minor (deferred): IsStillRunning 샘플 시점과 "still finishing" 문구 불일치 가능(ScanCoordinator.cs:271)
Task P1: minor (deferred): "즉시 실패" 테스트가 동기 throw를 검증하지 않음(ScanCoordinatorTests.cs:321)
Task P1: minor (deferred): 테스트가 한국어 문구 속 영어 조각에 의존(:223,349) — P2 리소스 이동 시 상수/플래그 매칭으로 전환
Task P1: minor (deferred): ProbeExecutor 요약 상수가 CannotVerifyTexts와 중복
Task P1: minor (deferred): ProbeStatus 미정의 값 default 분기 무시(ProbeResultConverter.cs:55)
Task P1: minor (deferred): 타임아웃 상한 검사 없음(약 24.8일 초과 시 CancelAfter throw)
Task P1: minor (deferred): 타임아웃 타이머 2개 중복(CancelAfter + Task.Delay)
Task P1: minor (deferred): ScanReport.SchemaVersion이 init으로 덮어쓰기 가능
Task P1: minor (deferred): RuleEvaluator null 반환 분기 미테스트, null Finding 통과
Task P1: minor (deferred): 보고서 §2.13 문구 구식, 줄 수 오기
Task P1: minor (deferred): 한국어 문자열이 Core 상수(P2에서 리소스로 이동 예정)
Task P1: fix round 1/5 dispatched (findings: Important#1, Minor#1 승격, Minor#7 제거)
Task P1: fix round 1/5 implementer DONE (commit ecbe166, 55/55, 30회 반복 통과). 추가로 무시-취소 테스트의 grace 의존도 제거, null 항목 포함 리스트도 격리.
Task P1: fix round 1/5 (3 addressed, 0 open; commits 636601c..ecbe166)
Task P1: complete (commits 4e54f87..ecbe166, review clean after round 1; 13 minors deferred)
Task P2: implementer DONE_WITH_CONCERNS (commit e6d67d4, 166/166 + smoke 3/3). 우려: 메모리 단위를 SMBIOS 주버전으로 추정(3+→MT/s), 설정>보고는 Info, 설정 파일 없음(코드 기본값), 로그 UTC, App csproj 리소스 생성 훅, P1 상수→CoreStrings.resx 이동.
External review (사용자 전달, 다른 세션): P1 결함 4건 — ScanSnapshot 내부 목록 가변, 규칙 실패가 ScanOutcome에 미반영, 규칙의 null Finding이 리포트에 유입, 재검사 시 이전 draining 표시 소실. 컨트롤러가 위치 확인 후 P1 후속 수정으로 처리.
Task P2: review dispatched (opus, diff-only, 빌드·테스트 실행 금지 — P1 후속 수정과 병행)
Task P1: fix round 2/5 dispatched (외부 검증 결함 4건: 스냅샷 불변, 규칙 실패→Partial, null Finding 격리, 이전 검사 draining 표시 유지)
Task P2: review (opus) Approved. ⚠️1 프로브 UI 스레드 차단 → 해소(ProbeExecutor.cs:102 Task.Run 확인). ⚠️3 측정값 JSON 직렬화 → 해소(스모크 내보내기에서 kind/value 확인). ⚠️2 WMI 타임아웃 주석 과장 → minor 이월. ⚠️4 UI 취소·실패 카드는 VM 테스트로만 검증 → 수용. ⚠️5 빌드·테스트 재실행 → P1 round 2 커밋 후 컨트롤러가 실행 예정.
Task P2: minor (deferred): SMBIOS 3.0~3.1 MT/s 경계 미검증, 2.x 반속 보고 시 오탐 가능(MemoryProbe.cs:179)
Task P2: minor (deferred): WMI 타임아웃·로그 경로/보관/상한이 코드 상수(설정 분리 규칙) + WMI_PROVIDER_TIMEOUT 중복
Task P2: minor (deferred): 스크러버가 enum/URI/ID 등 구조 문자열까지 치환 — 짧은 사용자명이면 내보내기 손상(ReportExporter.cs:101, PersonalDataScrubber.cs:62)
Task P2: minor (deferred): FileAppLogger 보관 실패 시 용량 계산 누락, 일별 파일 8개 보관(FileAppLogger.cs:133,186)
Task P2: minor (deferred): 예상 밖 예외 시 VM이 Scanning 상태로 고착, Export가 JsonException 미처리(MainViewModel.cs:188,247)
Task P2: minor (deferred): PowerPlanRule URI와 SettingsUriPolicy 허용 목록을 잇는 테스트 없음
Task P2: minor (deferred): 카드에 내부 측정 이름·batteryLifePercent=255 노출, 메모리 Candidate에 Apply 액션 부적절
Task P2: minor (deferred): csproj ResGen 훅이 MSBuild 내부에 의존, Format 헬퍼 5중 복사, 한국어 예외 메시지 코드 내장, using 배치, PartNumber trim
Task P2: minor (deferred): 금지 문구 목록에 "정격" 단독 없음, 스모크 export 검증 약함, 자동화 중 Tab 키 유출, 승격 셸에서만 E2E 실행(asInvoker 경로 미검증), 스모크 파일 %TEMP% 잔존
Task P2: minor (deferred): HasCards 미사용, WmiClient 타임아웃 주석 과장
Task P2: complete pending — 리뷰 clean(commits ecbe166..e6d67d4); P1 round 2 커밋 후 컨트롤러 빌드·테스트 재실행으로 확정
Task P1: fix round 2/5 implementer DONE (commit b8675e5, 176/176, 20회 반복). API 변경: RuleEvaluator.Evaluate→RuleEvaluationResult, ScanOutcomeResolver.Resolve 3인자, ProbeResult가 null 목록 거부.
Controller verify @b8675e5: Release build 0 warn/0 err, default tests 176/176, smoke 3/3 (P2 ⚠️5 해소)
Task P2: complete (commits ecbe166..e6d67d4, review clean, 12 minors deferred)
Task P1: fix round 2/5 (4 addressed, 0 open; commits e6d67d4..b8675e5)
Task P1: minor (deferred): RuleEvaluationResult/ScanReport 목록이 ReadOnlyListCopy 대신 AsReadOnly 직접 사용(안전하나 비일관), TextListValue with 경로 전용 테스트 없음
Task P1: complete (final; commits 4e54f87..ecbe166 + b8675e5, review clean after round 2)
Ruling: P3를 P3a(디스플레이·GPU 설치정보·그래픽 설정·보안·저장소·시스템 정보 프로브+규칙)와 P3b(시작 프로그램·관리자 재검사/UAC/SID)로 분할해 순차 파견 — P2 diff가 330KB였고 P3 전체는 리뷰 불가 크기 — 틀리면 리뷰 1회 추가 비용
Task P3a: dispatched (opus, base b8675e5)
Task P3a: implementer DONE_WITH_CONCERNS (commit f3c657b, 392/392 + smoke 11/11). 이 PC 관측: 디스플레이 3대, DISPLAY3 120Hz→130Hz Candidate.
Task P3a: Ruling: 내보내기에 남는 장치 식별자(모니터 장치 경로, PnP 인스턴스 ID, 디스크 GUID)는 스펙 §3 위반이므로 리뷰 전 pre-review fix로 ReportExporter/스크러버에 검사 단위 익명 토큰 치환 추가 — 틀리면 P2 파일을 P3a가 건드린 범위 확장
Task P3a: 수용한 판단: 가상 어댑터는 어댑터 장치 경로로 판별, 드라이브 문자 없는 볼륨은 여유 공간 규칙 제외, 시스템 정보는 Driver 분류, 게임 모드 값 부재는 Unsupported. 노트: 스모크는 승격 셸에서만 실행(P7에서 일반 권한 검증), 트레일러는 제약 규칙대로 유지.
Task P3a: pre-review fix 7de6324 — DeviceIdTokenizer(display-N/pnp-N/volume-N/guid-N, 내보내기 단위 순번). 418/418 + smoke 11/11. 노트: 디스크 GUID는 guid-N(텍스트만으로 종류 구분 불가).
Task P3a: review (opus) Needs fixes — Important: DeviceIdTokenizer PnP 패턴이 뒤따르는 구두점을 삼켜 같은 ID가 다른 토큰(DeviceIdTokenizer.cs:111, CoreStrings.resx:121). P/Invoke 구조체 크기 전수 확인 통과.
Task P3a: minor (deferred): IsVirtualAdapter가 어댑터 경로 null을 비가상으로 취급(DisplayRefreshRule.cs:354)
Task P3a: minor (deferred): 현재 Hz 0/1(하드웨어 기본) 미배제 → SignalHz 없으면 1→60Hz 오탐 가능(DisplayRefreshRule.cs:103,135)
Task P3a: minor (deferred): 토크나이저가 root\cimv2, PCI\CC_ 같은 비장치 텍스트도 치환(대소문자 무시 열거자 목록)
Task P3a: minor (deferred): ProcessRunner Kill 실패 시 무한 WaitForExit(ProcessRunner.cs:71)
Task P3a: minor (deferred): SystemInfoProbe가 Win32_BIOS 상수·쿼리 재선언(룰링 4 재사용 요구)
Task P3a: minor (deferred): QueryDisplayConfig 실패 시 원격 세션 플래그 미기록(DisplayProbe.cs:92)
Task P3a: minor (deferred): InstalledGpuProbe가 Win32_PnPEntity 전체 열거(InstalledGpuProbe.cs:169)
Task P3a: minor (deferred): CP949 fixture .bin이 텍스트로 저장돼 바이트 정확성 상실(.gitattributes 없음)
Task P3a: ⚠️ 회전 디스플레이의 EnumDisplaySettingsEx 모드 보고 방식 미검증(보수적 결과) → 수용; UI 카드 렌더링 → P7 검증
Task P3a: fix round 1/5 dispatched (Important#1)
Task P3a: fix round 1/5 implementer DONE (commit a450fac, 428/428 + smoke 11/11)
Task P3a: fix round 1/5 (1 addressed, 0 open; commits 7de6324..a450fac)
Task P3a: complete (commits b8675e5..a450fac, review clean after round 1; 8 minors deferred)
Task P3b: dispatched (opus, base a450fac)
Task P3b: implementer DONE_WITH_CONCERNS (commit a0ab605, 523/523 + smoke 13/13; 이 PC 시작 항목 27개). 우려: ProbeResultConverter가 Skipped+Unsupported의 Issue 요약을 Detail로 노출, 그래픽 설정 프로브 System/User 분리, 시작 프로그램 프로브 User 범위라 다른 SID 승격 창에서 HKLM Run·공용 폴더까지 건너뜀, dotnet.exe 호스트 재실행 거부 가드, 승격 창 자동 검사.
External review (사용자 전달): 검토 복사본 빌드 0/0·429 통과, 모니터3 120→130Hz 재현. 보완: "종료 중" 안내가 다음 검사까지 잔존(→ 계획 §5 메모, 최종 리뷰 triage), 계획 체크박스 미갱신(→ 갱신 커밋), 2차 "기존 도구 통합" 방향(→ 계획 §5 메모).
Task P3b: review dispatched (opus)
Task P3b: review (opus) Approved. ⚠️ 테스트 수치 → 컨트롤러 재실행으로 확인; 실제 UAC 취소 흐름·runas 스레드풀 호출·System 범위 가정 → P7 일반 권한 검증; Probes/Applications 폴더 → 계획과 일치.
Task P3b: minor (deferred): App.xaml.cs 실행 모드→limitToSystemScope 배선 자동 테스트 없음
Task P3b: minor (deferred): 승격 여부 계산이 WindowsElevationState와 ScanService 두 곳
Task P3b: minor (deferred): Win32RegistryReader GetValue 후 GetValueKind 사이 삭제 시 키 전체 Error(:87)
Task P3b: minor (deferred): StartupItemsRule Finding.Id에 경로형 값 이름 원문 포함(:149) — Id 스크럽 미검증
Task P3b: minor (deferred): ProbeResultConverter Skipped+Unsupported Detail 노출이 관례 의존, 테스트 픽스처 이름 오기
Task P3b: minor (deferred): 승격 재검사 버튼이 성공 후 재활성화(중복 창 가능)
Task P3b: minor (deferred): 승격 인스턴스가 --context GUID를 로그에 남기지 않음
Task P3b: complete (commits a450fac..a0ab605, review clean, 7 minors deferred)
Task P4: implementer DONE_WITH_CONCERNS (commit 9fed432, 643/643 + smoke 16/16; 실제 스캔 19.2s Success, 미분류 후보 44개 중 상위 20, 비관리자 토큰에서는 Partial/AccessDenied 확인). 우려: 정션 포함 상위 폴더는 부분 관측 처리(룰링 6 문자 그대로), 정책 무효 시 카드 2장, 경로 토크나이저 광범위(따옴표 안만 치환으로 OneDrive 문구 오치환 수정), 스캔 캐시 메모리(195k 노드), 7개 테스트 클래스 RED 기록 없음(프로세스 이탈, 정확성 문제 아님).
Task P4: review dispatched (opus)
Task P4: review (opus) Needs fixes — Important: 기본 내보내기에 프로필 밖 보호 루트 원본 경로 잔존(FileScanMeasurements.cs:54, FileScanSummaryRule.cs:135). 보호·정션·플레이스홀더·P/Invoke·선택기 규칙은 검증 통과.
Task P4: Ruling: Minor #3(알려진 폴더 미해석 시 기본 Documents가 스캔됨 — 보호 fail-open)은 보호 정책이 2차 실행기 강제 규칙이 되므로 Important로 승격해 이번 라운드에 포함(기본 프로필 위치도 항상 보호) — 틀리면 보호 범위가 약간 넓어질 뿐
Task P4: minor (deferred): PathTokenizer가 ';'를 종결자로 취급, 따옴표 범위 치환의 과치환/누락, 문장 속 경로와 단독 경로의 토큰 불일치(PathTokenizer.cs:111)
Task P4: minor (deferred): ProtectedExcluded가 부모 SkippedEntryCount에 합산돼 선택기 주석·테스트와 불일치(VolumeTraversalRun.cs:217)
Task P4: minor (deferred): ScanRootCatalog.Validate가 Users 폴더 자체/상위 루트를 거부하지 않음(:143)
Task P4: minor (deferred): 볼륨 게이트가 드라이브 문자 기준(subst/다중 문자 매핑), 프로필이 심볼릭 링크면 미스캔(FileSystemScanner.cs:132)
Task P4: minor (deferred): 스캔 결과(195k 노드 확장자 사전)가 다음 검사까지 상주, 선택기가 매 실행 트리 재구성
Task P4: minor (deferred): 정책 무효 시 카드 2장으로 확인 불가 건수 부풀림(FileScanSummaryRule.cs:51)
Task P4: minor (deferred): TestDirectory 정리가 정션을 따라갈 수 있음(TestDirectory.cs:69)
Task P4: minor (deferred): new long[6] 매직 넘버(FileScanSummaryRule.cs:88)
Task P4: minor (deferred): 7개 테스트 클래스 RED 기록 없음(절차 결함, 단언은 구체적)
Task P4: ⚠️ 진단용 내보내기 스텁 비활성 유지·Finding.Id UI 미노출·asInvoker 창 → P7; EnumerationOptions 정션 추종 가정 → 수용
Task P4: fix round 1/5 dispatched (Important#1 + Minor#3 승격)
User instruction: docs/reviews/REVIEW_LEDGER.md(Codex 독립 검토 원장)를 항상 참고하며 작업. global-constraints.md에 필수 규칙으로 추가. 원장·repro 커밋.
REV-001(P1, P4 보호: 드라이브 루트 Known Folder 미보호), REV-002(P2, P4 순회: 단일 폴더 내부 취소·예산 미검사) → P4 fix round 1에 추가 파견.
REV-003(P2, '종료 중' 안내 미갱신) → Ruling: P7 UI 통합 단계에서 실행 상태 변경 알림으로 수정(별도 태스크 항목 추가) — 틀리면 P2 VM 소규모 재작업
REV-004(제품 범위) → 사용자 결정 필요. 진단 항목별 연결 도구·재확인 표 초안을 스펙 부록으로 작성해 제시 예정(P5 이후).
REV-005 → 검증 완료.
Task P4: fix round 1/5 implementer DONE (commits 5399f00 code, c2f23a7 ledger; 655/655 + smoke 16/16; REV-001/002 회귀 테스트 편입, 원장 대응 기록 작성). 우려: 예산 초과 폴더 미계수로 기존 테스트 기대값 변경, %TEMP%가 프로필 밖이면 경로 노출(최종 리뷰 이월), #1/#3 테스트 RED 미기록.
Task P4: fix round 1/5 (3 addressed: Important#1, #3승격, REV-001, 원장 절차; 1 open: REV-002 예산 절반 — 열거 중 예산 초과 시 버퍼 항목 폐기(VolumeTraversalRun.cs:189-217); commits 9fed432..c2f23a7)
Task P4: 새 결함(round 1 diff): 루트가 자체 열거 중 타임아웃되면 Scanned·0바이트로 보고(:161); 열거 예외 후 예산 만료 시 Fail 이중 호출(:198-232) — 같은 경로라 round 2에 포함
Task P4: Ruling: 위 두 Minor를 REV-002와 함께 round 2에 포함 — 같은 함수의 동일 결함군이며 "누락 용량 0바이트 금지" 규칙 위반 — 틀리면 라운드 범위 약간 확대
Task P4: minor (deferred): Known Folder가 C:\로 해석되면 전체 스캔이 비어도 사용자 설명 없음(fail-closed); root/temp path가 프로필 밖이면 원본 노출; "D:" 드라이브 상대 경로 과보호(안전)
Task P4: fix round 2/5 dispatched (REV-002 예산 부분합계 + 루트 타임아웃 0바이트 + Fail 이중 호출)
Task P4: fix round 2/5 implementer DONE (commits 4c901e4 code, c0ad2e3 ledger; 659/659 + smoke 16/16; 되돌린 소스로 5/18 실패 확인). 우려: 협력적 중단이라 예산 약간 초과 가능, 예산 교차 시점에 완전히 읽힌 폴더도 타임아웃 표시(보수적), 대용량 실제 폴더는 fake로만 검증.
Task P4: fix round 2/5 (3 addressed, 0 open; commits c2f23a7..c0ad2e3)
Task P4: minor (deferred): 처리 루프에 예산 검사 없음 — 예산 초과 후 ID/할당 크기 조회가 계속됨(VolumeTraversalRun.cs:219). 원장 남은 제한에 명시 필요
Task P4: minor (deferred): 루트 timedOut 측정값 테스트 없음(FileScanMeasurements.cs:127); 규칙/UI가 루트 timedOut 미사용
Task P4: minor (deferred): 원장 REV-002 1차 정정 문구 중복·누락, VolumeTraversalRun.cs 파일 헤더 문구 구식
Task P4: minor (deferred): 하위 폴더만 받은 채 타임아웃된 루트는 Scanned/0바이트/partial로 표시될 수 있음(UI "0바이트(부분)" 가능)
Task P4: complete (commits 0bce903..c0ad2e3, review clean after round 2; 14 minors deferred)
Controller verify @c0ad2e3: see build/test output above
Task P5: dispatched next (opus)
Task P5: implementer DONE_WITH_CONCERNS (commit fea0eae, 758/758 + smoke 18/18; 스냅샷 파싱 44ms, 3,672/4,068 지원, supplement 7/7, 이 PC 탐지 200 규칙). 룰링 이탈: REMOVESELF를 하위 포함으로 해석(수용: CCleaner 의미상 재귀 포함), %ProgramData% 변수 추가(수용), ExcludeKey를 규칙 전체 대상에 적용(수용: 규칙 단위 의미), IFileScanService 미확장·5초 예산 재열거(리뷰어 판단). 추가: 다른 사용자 프로필·휴지통 진입 차단 가드(수용). 우려: 카드 197장(47장 파일 없음) → Ruling 예정, Slack/GitHub Desktop 레이아웃·metadata URL 미검증, 비승격 실행 미검증.
Task P5: Ruling: 앱 캐시 카드는 탐지 규칙마다가 아니라 앱(섹션)마다 1장으로 묶고, 관측 0바이트 항목은 카탈로그 요약 카드에 건수로 접는다 — 초보자 화면에 197장은 사용 불가 — 틀리면 규칙별 상세가 카드 상세 패널로 내려갈 뿐. P5 fix round에 포함.
External re-verification (Codex, 기준 23f92e9): REV-001 검증 완료. REV-002 부분 수정·추가 재현 실패 — 처리 단계(HandleFile의 파일 ID·할당 크기 조회)에 예산 검사 없음, repro ProcessingBudgetReviewTests 1/1 실패. 원장·repro 커밋. → P4 fix round 3 파견(P4 round 2 이월 minor와 동일 항목).
Task P4: fix round 3/5 dispatched (REV-002 처리 단계 예산)
Task P5: review dispatched (opus, diff-only, 스냅샷 헝크 제외)
Task P4: fix round 3/5 implementer DONE (commits c8d2c59 code, cbcfa2e ledger; 760/760 + smoke 18/18; 예산 소진 후 ID/할당 크기 조회 중단, lookupsSkipped 계수, 논리 크기 보존)
Task P4: fix round 3/5 (1 addressed, 0 open; commits dd59b14..cbcfa2e). Minor 3건: 교차 파일에서 조회 2회 가능(문구 "한 번" 부정확), 스캔 마지막 조회에서 교차 시 TimedOut 미표시, 원장 요약 문구 구식.
Task P4: Ruling: "조회 한 번" 문구를 사실로 만들기 위해 할당 크기 조회 전 예산 재검사 한 줄 + 문구 정정을 round 4로 처리 — 원장 기록의 정확성이 독립 재검증의 근거이므로 — 틀리면 라운드 1회 추가 비용
Task P4: minor (deferred): 스캔 마지막 조회에서 예산 교차 시 TimedOut=false(데이터는 완전)
Task P4: fix round 4/5 dispatched (문구·재검사 한 줄)
Task P5: review (opus) Needs fixes — Important 5: (1) 무결성 검사가 같은 폴더의 sources.json 참조(자기참조, RuleCatalogLoader.cs:95,122), (2) 설정 리더가 탐지된 규칙에서만 실행돼 이동된 캐시 미탐지(AppCacheProbe.cs:209), (3) 다른 사용자 가드가 프로필 부모 폴더 기준(OtherUserLocationGuard.cs:35) — %Public% 차단·드라이브 루트 프로필 오판, (4) 카드 197장(AppCacheRule.cs:62), (5) 커뮤니티 규칙(비밀번호·쿠키·Warning=)에 "캐시·임시 파일 후보" 제목과 저장소 센스 버튼(AppCacheRule.cs:128,303).
Task P5: Ruling: Minor#1(중간 정션 추종, PathPatternExpander.cs:76 — §5.2 위반), Minor#3(supplement.ini의 Squirrel app-* 행이 실제 삭제형 FileKey — 향후 실행기 안전), Minor#2(Steam 키 값 전체 읽기 — 룰링 7 위반)를 Important로 승격해 round 1에 포함 — 틀리면 라운드 범위 확대뿐
Task P5: Ruling(무결성): 규칙 파일 4종을 Probes 어셈블리 EmbeddedResource로 임베드하고 파일 시스템에서 읽지 않음. rules/는 리포 원본으로 유지(Link) — 사용자 쓰기 가능 폴더 우회 원천 차단
Task P5: Ruling(비캐시 규칙): supplement(검토) 규칙만 캐시 문구, 커뮤니티 규칙은 중립 제목·설정 버튼 없음, Warning= 또는 이름에 password/cookie/session/history/credential 포함 규칙은 카드 제외·요약에 건수
Task P5: minor (deferred): 스캔 루트 내 패턴 대상 재열거(≈4s), 미지 앱 이름 Adobe 기본값(AppCacheRule.cs:259), PathTokenizer 정규식 곱셈 비용, ko-captured-cp949.bin 범위 밖 변경, Text()/UNC_PREFIX/Result() 중복
Task P5: ⚠️ Failed/Timeout 프로브의 CannotVerify 변환은 P1 ProbeResultConverter가 담당(확인됨: Skipped/Failed/Cancelled 시 카테고리별 CannotVerify 생성); TryGetDirectoryTotals의 보호·플레이스홀더 제외 → 재리뷰에서 확인 요청
Task P5: fix round 1/5 pending (P4 round 4 완료 후 파견)
Task P4: fix round 4/5 implementer DONE (commits b00fa49 code, 46e0394 ledger; 761/761; 할당 크기 조회 전 예산 재검사, 문구 정정)
Task P4: round 4 re-review dispatched (sonnet); Task P5: fix round 1/5 dispatched (병렬, 구현자 1개)
Task P4: fix round 4/5 (1 addressed, 0 open; commits cbcfa2e..46e0394)
Task P4: complete (final; commits 0bce903..c0ad2e3 + c8d2c59/cbcfa2e + b00fa49/46e0394, review clean after round 4; REV-001 검증 완료, REV-002 수정됨·재검증 대기; 15 minors deferred)
REV-004: 스펙 §13에 진단 항목별 연결 도구·조치 후 재확인 표 초안 작성·커밋. 사용자 결정 3건 대기(1차 연결 포함 여부, 2차 자동 조치 범위, 커뮤니티 규칙 표시). 원장 대응 기록은 사용자 결정 후 기입.
External re-verification (Codex, 기준 1b9a9ad): P4 4차 확인(37/37, 761/761, 빌드 0/0), REV-002 상태 "주요 수정 검증·최종 조회 경계 보류"(P7에서 종료 시점 예산 상태 검증). P5 8건은 다음 독립 리뷰 인계 목록으로 기록 — 리뷰 패키지에 8건 각각의 변경 파일/커밋·재현/회귀 근거 요구. §13 검토: Squirrel·NuGet 자동 격리 제외, 탐색기 열기 1차 보류 요청 → 스펙 정정 커밋.
Ruling: 탐색기 열기는 1차 미포함(§13 도입부와 일관), 2차에 넣으려면 관측 로컬 경로 화이트리스트 계약 선행 — 틀리면 2차 설계 시 재검토
Ruling: 자동 조치 후보는 실행용 검증 조건을 규칙별로 정한 것만, Squirrel 버전 폴더·NuGet 전역 패키지 제외 — 스펙 §5 보장 범위 — 틀리면 2차 범위 축소뿐
P7 항목 추가 예정: 스캔 종료 시점 예산 상태 검증(관측 완료 여부와 예산 준수 여부 구분)
Task P5: fix round 1/5 implementer DONE (commits 99c1804 code, ea23db1 ledger; 782/782 + smoke 18/18; 카드 197→119: 앱 117 + 요약 1 + Squirrel 1; 요약에 파일 없음 45·병합 4·보호 13·개인정보 규칙 16 접힘). 우려: 비관리자·실제 정션 E2E 미검증, Finding 3 RED는 구 가드 에뮬레이션, Squirrel은 Discord만, 개인정보 필터는 이름 단어 기반(예: 'Login Data' 누락 가능), 117장 접기·정렬은 P7.
Task P5: fix round 1/5 (7 addressed, 1 open: Finding 4 — 커뮤니티 규칙 그룹 키가 규칙 이름이라 규칙당 카드(AppCacheCardPlanner.cs:98, AppCacheMeasurements.cs:135); 커버 테스트가 합성 App 라벨 사용; commits 2124c8d..ea23db1)
Task P5: 새 결함(round 1 diff): AccessDenied/TimedOut/Protected 앱이 사유 없이 요약에 접힘(FoldedReason) — 요구 접기 집합(0바이트·병합·부재) 초과; ProfileList 미조회 시 %SystemDrive%\Users만 가드(fail-open 폴백); 혼합 카드 설정 범위 줄 중복(cosmetic, deferred)
Task P5: Ruling: 커뮤니티 규칙 그룹 키 = winapp2 `Section=` 텍스트(숫자 LangSecRef 제외), 없으면 규칙 이름에서 후행 " *" 제거 — 1,837 항목이 Section 보유 — 틀리면 일부 앱이 여전히 분리될 뿐
Task P5: Ruling: 확인 불가(AccessDenied/TimedOut/Protected) 앱은 접지 않고 앱 단위 CannotVerify 카드로 사유 표시 — 스펙 "모르는 것은 확인 불가로 표시"
Task P5: Ruling: ProfileList 미조회 시 기존 부모-폴더 휴리스틱(드라이브 루트 제외)을 추가 폴백으로 유지 — fail-closed 방향
Task P5: Ruling: FileScanService의 protect.json BaseDirectory 읽기는 Finding 1과 같은 자기참조 신뢰 결함이므로 이번 라운드에 EmbeddedResource로 전환(P4 범위지만 동일 결함군) — 틀리면 정책 파일 편집이 재빌드를 요구할 뿐
Task P5: minor (deferred): ProtectionPolicyResolver가 OneDrive Accounts\* 키의 모든 값을 읽음(:150) → ReadStringValue로 교체; ReparseAncestorCheck에서 AccessDenied/placeholder 중간 세그먼트가 default false; 개인정보 키워드 필터 커버리지(Autofill, Web Storage, Sync Data, Bookmark Backups, Unsaved Files는 카드로 남음 — 중립 제목·상세만이라 오분류는 아님)
Task P5: fix round 2/5 dispatched
Task P5: fix round 2/5 implementer DONE (commits aff581b code, 759e8f4 ledger; 797/797 + smoke 18/18; 카드 119→97: 앱 87 + 요약 + Squirrel, CannotVerify 앱 카드 8(보호 폴더), 요약 접힘 11+2, 개인정보 규칙 16 제외). 우려: 보호 폴더 전용 CannotVerifyReason 없음(Unsupported+문구), ProfileList 부재 시 부모 폴더 밖 프로필 미차단, 이번 라운드 내보내기 원본 경로 재확인 안 함.
Task P5: fix round 2/5 (4 addressed, 0 open of assigned; 새 Important: Section=Games(521 규칙)가 게임 12개를 한 카드로 과병합 — 룰링 결함, 컨트롤러가 수정; commits ea23db1..759e8f4). 내보내기 원본 경로 0건 재확인(리뷰어). Minor: 출력 폴더 테스트 잔존 파일 위험, SOURCE_POLICY 문구 구식.
Task P5: Ruling(수정): Section 텍스트는 CATEGORY_SECTIONS={Games, Adobe} 제외·해당 Section 공유 규칙 ≤100일 때만 앱 키, 아니면 규칙 이름 — 스냅샷 분포(Games 521, Adobe 31, 그 외 ≤30) — 틀리면 일부 앱이 분리·병합될 뿐
Task P5: fix round 3/5 dispatched (Section 카테고리 과병합 + 출력 폴더 테스트 잔존 위험 + SOURCE_POLICY 문구)
Task P5: fix round 3/5 implementer DONE (commits 18ea323 code, 96a593c ledger; 803/803 + smoke 18/18; 카드 97→108: 앱 106(Info 93·CannotVerify 13), Games 카드 해체, 내보내기 C:\Users\·VEN_ 0건). 이탈: 출력 폴더 tamper 테스트 삭제 대신 '출력 폴더에 protect.json 없음 + 정책 유효' 테스트로 대체.
Task P5: fix round 3/5 (Important 1 addressed — 스냅샷·내보내기 교차확인; Minor 1 open; commits 759e8f4..96a593c)
Task P5: minor (deferred): protect.json 미읽기 회귀 가드가 "출력 폴더 비어 있음"에 의존 — IPolicySource/Func<Stream> 심을 두고 변조 페이로드 무시를 직접 단언하도록 최종 리뷰에서 결정(리뷰어 제안)
Task P5: minor (deferred): CATEGORY_SECTIONS는 현재 스냅샷 기준 수기 목록 — 스냅샷 갱신 시 Section 분포 재확인 필요(sources.json 갱신 절차에 포함)
Task P5: complete (commits 23f92e9..96a593c, review clean after round 3; Minor 1 parked)
Controller verify @96a593c: see build/test output above
Task P6: dispatched next (opus)
Task P6: implementer DONE_WITH_CONCERNS (commits 8180165, 5231f28; 자체 보고 1085/1085 + smoke 18/18 + online 3/3). 리뷰 미실시 — 사용량 한도로 중단. HANDOFF: docs/HANDOFF.md
External review (Codex): P5 기본 803·스모크 18 독립 확인; REV-006(정션 아래 설정 파일 본문 읽음), REV-007(접근 거부 설정 파일을 설정 없음 처리) 재현 — P5 fix round 4 필요. 앱 캐시 카드 수는 106장(97은 이전 라운드 수치).
SESSION END 2026-09-26: 사용량 한도. 재개는 docs/HANDOFF.md 기준.
