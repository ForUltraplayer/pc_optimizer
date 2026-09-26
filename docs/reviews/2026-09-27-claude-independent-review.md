# 2026-09-27 독립 리뷰 (Claude 세션) — 범위 `9aa45b3..ad06cca`

검토 방식: diff 파일·커밋된 문서·`.trx` 산출물만 읽음. 빌드·테스트 재실행 없음(동시 작업 중인 세션의 미커밋 변경으로 작업 트리 빌드가 깨져 있어 실행하지 않음). 작업 트리의 미커밋 변경은 범위 밖. 이 문서의 결론은 자체 검증이 아니라 독립 검토이며, 원장의 REV-008~012가 대응 추적 항목이다.

## 실행기 안전 판정

✅ 승인 허용 목록 안에서만 동작하며 관문이 코드에서 강제·재확인됨.

- 명령 형태(`CacheToolProcess.cs:83-92`): npm `[npm-cli.js, cache, clean, --force, --offline, --cache, <경로>]` 정확 일치. pip는 `--disable-pip-version-check`, dotnet은 `--force-english-output`이 스펙 문구보다 추가됨(REV-012). 조회 명령(`config get cache`, `cache dir`, `http-cache --list`)은 읽기 전용. `UseShellExecute=false`, `ArgumentList`, 미지 도구는 예외. 자식 환경에서 `DOTNET_*/CORECLR_*/COR_*/NODE_*/PYTHON*` 제거.
- 도구 탐색(`SystemCacheToolBackend.cs:81-89`): `%ProgramFiles%\nodejs`, `%ProgramFiles%\dotnet`, 그다음 PATH(WindowsApps 제외, 상위 3개), 각 경로의 조상에 reparse/placeholder 없어야 함. 승격 관문은 `LocateAsync:25`, `Inspect:107`, `ClearAsync:56` 세 곳에서 코드로 강제.
- 계획 수명(`CacheCleanupService.cs`): 5분(:42), 일회용 `TryRemove`(:68), 재검증 전후 만료 확인(:68,74), 재-Locate + 실행 파일·npm-cli.js SHA-256 지문 + 캐시 경로 동등성(:70-71), 재-Inspect(:72), `SemaphoreSlim(1)` 직렬화(:65). 미리보기에서만 검사하고 실행 시 생략되는 관문 없음.
- 경로 관문(`SystemCacheToolBackend.cs:103-157`): 승격 거절, 포함 protect.json, ProfileList 기반 다른 사용자 가드(현재 SID), 프로필 밖 거절, 루트·조상·하위 폴더·모든 항목의 reparse/placeholder(0x40000/0x400000) 검사, 10만 항목/15초 초과 시 InspectionIncomplete, NuGet 비-.dat 파일 거절, IO/접근 예외 시 거절.
- 프로세스 제어(`CacheToolProcess.cs`): 정리 2분/조회 15초, 타임아웃 시 트리 Kill, 5초 대기 후 전역 거절 플래그, 출력 16 KiB 상한, 출력 미기록, 도구 종료와 재관측 분리, 실패 시 부분 정리 문구. 종료 중(draining) 도구 창 차단은 UI에서만(`MainViewModel.cs:185`, `MainWindow.xaml.cs:40`).
- 테스트: 가짜 백엔드로 일회용·만료·경로/지문 변경·보호 재검사·Busy·실패≠성공·취소·고정 인자 검증(`CacheCleanupTests.cs`, p7-unit.trx 33건). 실제 도구 스모크는 소유 임시 fixture만 사용, 환경 변수 미설정 시 실제 캐시를 지우는 경로 없음.

## REV 판정

- REV-006 ADDRESSED(잔여: 핸들 기반 TOCTOU 경계 없음 — 원장 명시).
- REV-007 ADDRESSED.
- REV-002 ADDRESSED(마지막 조회 초과 시 TimedOut=true·누락 0 보존). 이월 minor "예산 초과 후 루트 내 조회 계속"은 미변경.
- REV-003 ADDRESSED(레이스 검토: 닫힌 창 이후 이벤트는 `_disposed` 가드, 오래된 ScanId는 (probeId, scanId) 키로 제거).

## Important (출시 전 수정, REV-008~012)

1. 도구가 보고한 캐시 경로를 8.3 별칭 정규화 없이 문자열 접두 비교(`SystemCacheToolBackend.cs:44,118-122`). `DOCUME~1` 같은 별칭이 프로필 하위 검사를 통과해 Known Folder 보호를 우회할 가능성. 수정: `environment.NormalizePath`(GetFullPath+GetLongPathNameW)로 정규화 후 비교.
2. `Inspect` 관문에 직접 테스트 없음(private/static, 실제 WindowsIdentity·레지스트리·FS 의존). OutsideUserProfile, ProtectedPath, 다른 사용자, 링크/placeholder 하위, InspectionIncomplete, UnexpectedHttpCacheContent, 승격 거절이 미검증. 수정: IPathEnvironment/IRegistryReader/IDirectoryEntrySource/승격 주입 + 관문별 가짜 FS 테스트.
3. Kill 실패 경로가 조용하고 기능을 영구 비활성화(`CacheToolProcess.cs:69-71`): 예외 무시·미기록, 전역 `_unfinishedProcess`로 이후 모든 RunAsync가 false → UI는 "도구 미설치" 오진. 수정: 로그, 별도 코드(ProcessStillRunning) 표시, 거절 플래그 유지.
4. 필수 파일 헤더 누락/삭제: `MainViewModel.Overview.cs:1`, `CacheToolsWindow.xaml:1`(신규, 없음), `MainWindow.xaml:1`(이 범위에서 제거됨). 공개 멤버 한글 XML 주석 누락: `FindingCardViewModel.cs:92,163-166`, `MainViewModel.Overview.cs:14-41`, `MainViewModel.cs:94`.
5. 스펙 문구와 인자 불일치(`CacheToolProcess.cs:89,91`): `--disable-pip-version-check`, `--force-english-output`은 스펙 허용 목록에 없음. 무해하나 사용자 승인 목록과 고정 인자 테스트가 정확히 일치해야 함 — 인자 삭제(pip는 env `PIP_DISABLE_PIP_VERSION_CHECK=1`과 중복) 또는 스펙 개정.

## Minor (최종 리뷰 triage)

- 오류 코드 문자열 리터럴 산재(`CacheCleanupService.cs:53-80`, `SystemCacheToolBackend.cs`, `CacheToolsViewModel.cs:99-106`) → 상수화.
- `CacheToolsViewModel.cs:106` Busy/InspectionFailed/ProtectionUnavailable/UnsafePath를 모두 `Cleanup_Blocked` 문구로, `:72` 미리보기 예외에 `Cleanup_Failed`("일부만 정리") 문구, `CacheToolsWindow.xaml.cs:36` 설정 URI 실패에도 같은 문구 → 전용 문자열.
- `CacheToolsViewModel.cs:84` 도구 실행 여부와 무관하게 `NeedsRescan=true`.
- `MainWindow.xaml.cs:38` `async void` 핸들러 미보호.
- `MainViewModel.Overview.cs:32-35` 취소/부분 검사가 후보 0과 같은 제목(테스트가 이를 단언) → "검사 중단/일부 완료" 제목.
- `MainViewModel.cs:251-256` 일반 실패 시 State=Partial + 이전 LastResult 유지 → 상태 문구와 리포트 모순.
- MainWindow.xaml 온라인 확인 바인딩 중복(L1921, L1963); 하드코딩 밝은 팔레트가 DynamicResource 테마 브러시를 대체 → WPF-UI 다크 테마 미적용.
- Apply 제거 후 고아 리소스/멤버: `Button_Apply`, `Tooltip_Apply`, `Button_DiagnosticExport`, `HasApplyAction/IsApplyEnabled/ApplyTooltip`.
- 파일 구성 순서·using 그룹 주석 누락(`NvidiaUrlAllowlist.cs:16`, `ScanService.cs:34`, `ScanCoordinator.cs:37`, 신규 파일 4개).
- 매직 값: `1_000_000d`(`CacheToolsViewModel.cs:70,91`), `(FileAttributes)0x40000/0x400000` 중복, `FromSeconds(5)/(1)`.
- `PipCacheToolSmokeTests.cs:21` env 미설정 시 skip 아닌 fail; 실제 도구 스모크가 도구의 `--cache`/`NUGET_HTTP_CACHE_PATH` 준수에 의존.
- `CacheToolProcess.cs:23-30` 조회만 해도 `%LOCALAPPDATA%\PcOptimizer\ToolWork` 생성.
- 커밋 제목 영어 `feat:/docs:`, 본문·트레일러 없음(관례 변경 기록 필요). HANDOFF 기본 테스트 수 1113(실제 1114). README가 git-ignored `artifacts/` 경로 안내.
- P6: 지역화된 NVIDIA 상세 URL(`/ko-kr/drivers/…`)은 fail-closed로 숨겨짐. WUA URI 상수 동일성 테스트 없음.
- 수용된 이탈: 좌측 분류 목록이 전체 결과 모드에서만 ComboBox; `적용` 버튼 비활성 대신 제거; 실행기가 별도 exe가 아닌 in-process(09-27 스펙이 대체).
- 이월 minor 상태: #77(Hz 0/1), #112(TestDirectory 정션)는 해결·테스트됨; #56 부분(Scanning 고착만); #130, #79, #106, #54, #170 등은 미해결.

## 문서 대조

`.trx`로 확인: 기본 1113/1113, Smoke 21/21, Online 3/3, ToolSmoke 1/1, cache-boundary 3/3, ui-overview-unit 1114/1114, layout 7/7. `ui-overview.trx`의 213/214(줄바꿈 실패)는 원장이 인정한 회귀로 01:02 레이아웃 재실행 전 기록. 미확인: 빌드 0/0, publish, EXE 종료 코드, 샌드박스 9건 실패, Python/pip 버전. `ui-flow*.trx`(01:09~01:12)는 `ad06cca` 이후 동시 세션의 산출물.

## 출시 판정

**일반 권한 수동 검증 진행 가능. 출시 전에는 REV-008~012 필수.**
