# HANDOFF — PC Optimizer (2026-09-27)

이 문서의 **재개 안내와 현재 상태가 아래 과거 기록보다 우선**한다. SP1 전체 완료 상태가 아니다. 공개 배포는 0.0.1이며 최신 요청은 0.0.2 이후 고급 탭 기능 조사·추가 구현이다.

## 최신 구현과 다음 작업 — 고급 탭 (2026-09-28)

- [조사·공식 근거·추가 후보](research/2026-09-28-advanced-options.md), [후속 Task 1~12](superpowers/plans/2026-09-28-advanced-options.md), [첫 구현 묶음·자체 검증·제한](reviews/2026-09-28-advanced-foundation.md). **MPO·HAGS·게임 모드 적용/원복, UEFI/고급 시작 요청, 해당 고급 UI**를 소스·Release 빌드에 구현했다. 공개 ZIP은 여전히 0.0.1 이전 기능 그대로이며 새 ZIP/버전은 만들지 않았다.
- 사용자 목록: NVIDIA ReBAR, 여러 GPU 옵션을 묶은 MPO fix, NVIDIA/AMD 동영상 업스케일링, UEFI/안전 모드 재부팅. MPO-GPU-FIX를 조사 기준으로 사용했으나 정확한 저장소 URL 지정이나 묶음 전체 채택 승인은 아니다.
- 핵심 정정: NVIDIA 공식 앱에 게임별 ReBAR 제어가 생겼으므로 펌웨어 상태와 프로필 토글을 분리. AMD 영상 업스케일링은 ADLX 공식 setter가 있고 NVIDIA 전역 VSR 직접 제어는 현행 지원 계약 검증이 남는다. `/o`는 안전 모드 직행이 아니며 메뉴 경유와 직행을 따로 완료 처리한다.
- 자체 검증: Release 경고0/오류0, 기본 1725/1725, 신규 임시 HKCU 키 네이티브 1/1, 고급 화면 100/150/200% 렌더와 좁은 폭 줄바꿈 확인. 실제 GPU 설정·재부팅은 시험하지 않았다. 독립 검증은 아니다.
- 재개 시 원장을 읽고 **T4 NVIDIA ReBAR 프로필 지원 계약 → T5 직접 제어 → T6 AMD 영상 → T7 NVIDIA 영상**으로 이어간다. T1 GPU 대상/JSON 표시 분리, T3 WinRE 조회/실제 진입 평가, T8 묶음 나머지·안전 모드 직행, T10 추가 후보, T11 장치 평가도 남음. HAGS는 명시 DWORD 값이 없는 PC에서 직접 변경을 막고 Windows 설정 확인을 안내한다.
- 사용자 순서인 구현 → UI/UX → 검증, 독립 리뷰·서브에이전트 생략, 기능별 중간 ZIP 생략을 유지한다. 과거 SP1 잔여 평가 항목은 이 계획으로 닫지 않는다.

## 공개 배포 버전 — 0.0.1

- 사용자 요청으로 preview.13 기능의 버전 번호를 **0.0.1**로 변경했다. [릴리즈 노트](releases/v0.0.1.md). GitHub 저장소 공개 전환 및 `v0.0.1` 태그의 ZIP·SHA-256 게시를 완료했다. 릴리즈 커밋 `757bc2f`, 인증 없는 API 조회로 공개 상태 확인.
- 저장소: https://github.com/ForUltraplayer/pc_optimizer — 기본 브랜치 `codex/sp1-actions`. 릴리즈: https://github.com/ForUltraplayer/pc_optimizer/releases/tag/v0.0.1
- 배포본: `dist/releases/PcOptimizer-v0.0.1-win-x64.zip`; 최종 해시는 같은 이름의 `.sha256` 파일과 GitHub 릴리즈 첨부 참조.
- 아래 preview.13 기록은 기능 기준 및 이전 로컬 산출물이다. 현재 공개 다운로드 버전은 0.0.1이다. 이번 변경은 버전/배포 문서이며 재빌드·ZIP 검증을 수행했다. 기존 1700 테스트는 preview.13에서 실행한 결과로 구분한다.

## 최신 UI 수정 — preview.13

- [스크롤·시인성 구현 및 자체 검증 기록](reviews/2026-09-27-scroll-visibility.md). 중첩 목록 휠 전달, 탭별 위치 복원(현재 앱 수명), 새 대상 확인/결과만 상단 이동, 남색 제목 표시줄·메뉴와 강화한 카드/텍스트/스크롤바 대비.
- Release 경고0/오류0, 기본 **1700/1700**. 실제 테마 100/150/200% 오프스크린 렌더·휠 이벤트·빠른 탭 전환 재현 통과. 실제 마우스/터치패드·DPI·Snap 확인은 남는다. Smoke/Online/ToolSmoke는 재실행하지 않았다.
- 아래 이전 단계 검증 수치는 당시 실행 기록이다. 독립 리뷰/서브에이전트·실제 PC 변경 시험·master 병합·원격 게시 없음.

## 1. 재개 위치와 작업 원칙

- 실제 저장소: `C:\Users\Administrator\Desktop\pc_optimizer`. 과거 대화의 `Desktop\windows` 경로를 그대로 사용하지 말 것.
- 브랜치: `codex/sp1-actions`.
- 최신 제품 구현: **preview.13 UI 수정(본 문서와 같은 커밋)** ← **`5acc71e`**(도구 카드 보조 링크·Rufus 홍차의 꿈 버튼) ← `41114ab`(유틸리티 탭·DISM 설치 미디어 원본 복구·링크 열기 수정) ← `75f7d65`(UI 내비게이션·스크롤) ← `ee05ec4`(좌측 내비게이션 7페이지) ← `081ecbd`(SP3 드라이버 안내) ← `5da2700`(SP5 문제 해결 도구함) ← `9bc264a`(T10 그래픽 캐시·Resolve) ← `e25aa43`(T6 StartupApproved) ← `9ab6822`(Codex 통합). 상세: [SP5·SP3 구현 기록](reviews/2026-09-27-sp5-sp3-implementation.md), [T6·T10 구현 기록](reviews/2026-09-27-startup-approval-and-graphics-cache.md).
- **UI 다듬기 1차 완료**(`ee05ec4`·`75f7d65`): 확정 배치안대로 좌측 내비게이션 7페이지(추천 조치·전체 결과·문제 해결·내 PC 사양·고급·조치 실행/되돌리기·설정)로 메인 창을 재구성했고, 조치·문제 해결·드라이버 화면을 페이지에 내장했다(창 래퍼는 유지). 상세: [UI 배치 기록](reviews/2026-09-27-ui-navigation.md). 남은 UI: 실기 DPI·키보드·최소 폭 확인, 고급 페이지는 SP2 내용이 들어오면 채움.
- 작성 착수 시 작업 트리 깨끗함. 이 세션이 실행한 백그라운드 작업·서브에이전트 없음. 실행 중인 사용자 앱을 종료/교체하지 않았다.
- `feature/p0-skeleton`은 앞서 로컬 master `32ab51f`로 병합됐다. **이번 SP1 브랜치는 master에 병합하지 않았고 원격 게시도 하지 않았다.**
- 사용자 우선순위: **기능 구현 → UI/UX 개선 → 검증**. 기능 하나마다 프리뷰를 만들고 멈추지 말고 묶어서 진행한다. 기존 도구·Windows 공식 기능의 통합 활용이 제품 방향이다.
- **독립 리뷰와 서브에이전트는 사용자 지시로 생략**한다. 자체 테스트를 독립 승인으로 표시하지 않는다.
- 착수 전 [공유 리뷰 원장](reviews/REVIEW_LEDGER.md)을 읽고 관련 REV-008/013/016/017/018 및 제품 방향 REV-004/015의 대응 기록을 유지한다. 기존 발견·증거를 지우지 않는다.
- 개발 시험은 소유한 임시 파일/레지스트리 또는 대역을 쓴다. 실제 사용자 캐시·시작 등록·서비스·전원·화면 변경 시험은 별도 평가 환경에서 수행한다. 일반 작업 PC에 임의 적용하지 않는다.

## 2. 바로 읽을 문서

1. [공유 리뷰 원장](reviews/REVIEW_LEDGER.md)의 마지막 통합 기록과 관련 리뷰 ID.
2. [통합 구현·자체 검증 보고서](reviews/2026-09-27-sp1-integrated-completion.md): 실행 계약, 변경 경계, 실패 이력, 테스트 명령, 미완료 범위.
3. [Task 1~12 현재 표](superpowers/sp1-progress.md).
4. [원래 SP1 계획](superpowers/plans/2026-09-27-sp1-actions.md), [남은 구현 계획과 실행 결과](superpowers/plans/2026-09-27-sp1-remaining-implementation.md).
5. 특정 기능을 이어갈 때 [업데이트 Download 실행](reviews/2026-09-27-update-download-implementation.md), [Steam·그래픽 계약](reviews/2026-09-27-steam-cleanup-and-graphics-guides.md), [HKLM·수동 앱 위치](reviews/2026-09-27-implementation-first-expansion.md).

## 3. 현재 구현과 배포본

**버전 `0.3.0-preview.13`**(유틸리티 탭·DISM /Source 복구·UAC 꺼진 PC의 링크 열기 수정·Rufus 한국어 안내 링크·스크롤/탭 위치/시인성 개선 포함), 단일 파일/self-contained Windows x64 평가 빌드.

- ZIP: `dist/sp1-integrated/PcOptimizer-v0.3.0-preview.13-win-x64.zip`(60,023,941 bytes).
- 실행 파일: `dist/sp1-integrated/PcOptimizer-v0.3.0-preview.13-win-x64/PcOptimizer.exe`.
- 해시: 같은 ZIP 이름의 `.sha256`.
- SHA-256: `3CA80153DDB0745C549525709BDEAD3578C0ABBCAC66B4CD015245638478AFD6`.
- ZIP 13파일/최상위4개, `tools/verify-package.ps1`로 PE·필수 고지·규칙 원문·원본 해시 확인(이번 실행은 PowerShell 7; 5.1 호환 수정은 이전 단계 기록). 변조 거절 시험은 preview.7 때 결과.
- preview.12는 비교용으로 보존(그 이전은 정리해도 됨). 그 이전 산출물 정리는 이미 완료했으므로 다시 광범위하게 삭제하지 않는다. `dist/`와 `artifacts/`는 로컬 산출물로 Git 커밋에 포함되지 않는다.

| 기능 | 현재 제공 범위 |
|---|---|
| 시작 앱 | HKCU Run·HKLM32/64 Run 등록 해제/원문 복원. 기본 사용자·공용 Startup의 지원 `.lnk`는 Programs 폴더에 보관/복원. 작업 관리자 '사용/사용 안 함'(StartupApproved) 토글·원래 값 복원(Run·Run32·시작 폴더, 관측 12바이트 형식만) |
| 일반 정리 | 사용자·Windows 임시 파일 7일 기준, 공식 npm·pip·NuGet HTTP 도구 실행 |
| 업데이트 | DO 비고정 캐시 ID별 처리, 고정 Windows Download 7일 파일·서비스 원본 기록/중지/복구. wuauserv·BITS 모두 Running 등 제한 조건 |
| Adobe | 기본/직접 선택 Media Cache Files·Peak Files의 90일 `.cfa/.pek`만 정리 |
| Steam | 직접 선택한 현재 라이브러리 shadercache의 AppID 아래 30일 파일. Program Files 보호 유지, Steam·게임 관측 불완전 시 거절 |
| Resolve·CapCut | 기본/지원 설정 또는 수동 CacheClip/Cache 위치의 용량 검사. Resolve는 `RenderCaching.CacheDir` 키가 없으면 첫 저장소의 CacheClip을 기본으로 해석(20.1 실제 설정 근거). 직접 삭제 없음 |
| 그래픽 | NVIDIA DX/GL/NV_Cache·Direct3D 관측, 공식 안내/Windows 기능 연결. 캐시별 30일 이상 미사용 파일 부분 정리(사용 중 파일 건너뜀, 설정 불변). 전체 초기화는 공식 절차 안내 |
| 전원·주사율 | 전원 계획 적용/복원, 같은 해상도 주사율 시험·15초 유지 확인/복구 기록. 실제 변경 평가는 남음 |
| 문제 해결(SP5) | 증상 8개 → 권장 순서 도구 카드. 직접 실행(DISM·DISM 설치 미디어 원본(/Source)·SFC·chkdsk 검사/재부팅 예약·flushdns·winsock reset·복원 지점), 알려진 DISM 오류 코드 한국어 안내, 내장 도구 열기(메모리 진단·디스크 관리·이벤트 뷰어·신뢰성 모니터·문제 해결사), 외부 도구 15종 공식 링크+한국어 절차 |
| 유틸리티 탭 | 외부 도구를 분류별 카드 섹션으로 표시(공식 사이트만 열기). 링크는 `tools/check-links.ps1`로 배포 전 점검 |
| 드라이버 안내(SP3) | 노트북/데스크톱별 제조사·모델 식별, 공식 지원 첫 화면 링크·모델명 복사, 칩셋·유선 랜·Wi-Fi·오디오 설치 버전·날짜와 받을 항목·설치 순서 안내, 그래픽 공식 링크. 최신 여부 판단 없음 |
| UI/UX | 좌측 내비게이션 7페이지(확정 배치안), 추천 조치 타일·3줄 카드, 효과별 진입·필터, 확인/결과 우선 배치, 필터 변경 시 계획·결과 보존, 수동 위치 펼침, 빈 목록 사유, 문제 해결 증상 칩(좁은 폭 대응) |

시작 폴더 보관 파일과 복구 기록을 모두 유지해야 복원할 수 있다. 파일 지문(ID/본문/ACL 등) 변경이나 원래 이름 충돌 시 덮어쓰지 않는다. 이 기능을 Task Manager의 사용/사용 안 함 토글과 동일하다고 설명하지 않는다. 자세한 제약은 통합 보고서와 `tools/README-in-zip.txt`를 따른다.

## 4. 검증 상태와 실행 환경

| 항목 | 마지막 실제 결과 |
|---|---|
| Release 솔루션 빌드 | 경고 0 / 오류 0 |
| 기본 | **1694/1694** |
| Smoke | **58/58** |
| ToolSmoke | **4/4**(Codex 마지막 실행, 이번 배치에서 재실행 안 함), 소유 임시 캐시에 고정한 npm·pip·NuGet |
| UI | WPF 100/150/200% 오프스크린 회귀, 메인/좁은 창 PNG 시각 확인 |
| Online | 미실행. 온라인 수집 코드가 바뀌지 않아 재실행하지 않음 |

증거: 기본·Smoke는 2026-09-27 UI 내비게이션 배치 뒤 콘솔 실행 결과(`reviews/2026-09-27-ui-navigation.md`), 오프스크린 렌더 `artifacts/ui-nav/`, ToolSmoke는 `artifacts/sp1-completion/`의 Codex 실행 결과.

- SDK: `C:\Program Files\dotnet\dotnet.exe` 10.0.401. PowerShell 사용.
- ToolSmoke 변수: `PCOPTIMIZER_TEST_NODE=C:\Program Files\nodejs\node.exe`, `PCOPTIMIZER_TEST_PYTHON=C:\ProgramData\Anaconda3\python.exe`, `PCOPTIMIZER_TEST_DOTNET=C:\Program Files\dotnet\dotnet.exe`.
- 화면 증거 저장: `PCOPTIMIZER_UI_ARTIFACTS=<저장소>\artifacts\sp1-completion\ui`.
- native Smoke의 조상 디렉터리 핸들, NuGet 자식 프로세스, 패키징의 NuGet.Config 읽기는 샌드박스 제한 밖 실행이 필요했다. 접근 거절을 제품 결함/미설치로 오해하거나 보호 코드를 완화하지 않는다. 권한은 도구의 정상 승인 경로를 사용한다.
- 명령 전문과 초기 실패·수정은 통합 보고서에 있다. 새 코드가 없으면 수치 갱신만을 위해 전체 테스트를 반복하지 않는다. 변경 시 관련 회귀부터 실행한다.

## 5. 남은 작업과 재개 순서

**미지원 기능은 영구 제외로 승인된 것이 아니다.** 근거가 없어서 추측 구현하지 않은 상태다. 아래 순서로 지원 계약과 fixture를 확보한 뒤 구현 → UI → 검증한다.

1. **T10 앱 설정 자동 탐지 확대**: Resolve 기본 위치는 해결(20.1 실제 설정). 프로젝트별 재정의는 DB 안이라 텍스트로 읽을 수 없어 범위 밖. **Adobe·CapCut은 이 PC에 설치돼 있지 않아 fixture 미확보(2026-09-27 확인)** — 설치된 PC의 설정 파일(민감 값 제거)을 받으면 진행한다. 현재 수동 위치 선택을 자동 탐지라고 표시하지 않는다.
2. **T6 StartupApproved 토글**: 구현 완료(`e25aa43`). 이 PC 26200에서 관측한 12바이트 형식(0x02+0 / 0x03+FILETIME)만 쓰고 다른 형식은 거절한다. 남은 것은 별도 평가 PC에서 다음 로그인 효과·작업 관리자 표시 동기화 확인.
3. **T10 NVIDIA 직접 정리**: 30일 미사용 파일 부분 정리로 구현(`9bc264a`, 기존 파일 엔진·독점 핸들·보호 경계 재사용, 설정 불변). 전체 초기화(캐시 끄기·재부팅)는 공식 안내로 유지. 남은 것은 게임/드라이버 실행 중 실기 거동 확인.
4. **T8/T9/T12 평가 Windows 11/VM**: UAC 승인/거절·다른 계정 SystemOnly·비승격 브라우저, 실제 다음 로그인, 전원 변경/복원, 화면 유지/시간 초과/분리/종료, Update COM/BITS ABI·다운로드 중 거절·서비스 중간 실패/복구·다음 실행 회복, 게임/편집 재개, .NET 없는 PC·SmartScreen·물리 DPI/키보드를 시험한다. 시험별 원상 복구와 실제 관측 근거를 남긴다. 별도 평가 환경이 확보되지 않아 아직 수행하지 않았다.
5. 지원 범위별 증거를 원장/Task 표에 반영한 뒤 통합 배포를 갱신한다.
6. **SP5·SP3 평가 항목(2026-09-27 추가)**: 평가 PC에서 SFC·chkntfs·netsh·복원 지점의 실제 출력 인코딩·진행 표시·시간 상한, mdsched 재부팅 흐름, 노트북에서 드라이버 안내 식별, DISM /Source(같은 빌드 ISO). 이 PC에서는 `ipconfig /flushdns`와 DISM(사용자 실행, 0x800f0915로 실패 → 원본 복구 변형 추가)만 실제 실행됐다. UAC를 켠 PC에서 링크가 비승격 셸로 열리는지도 확인해야 한다(이 PC는 UAC 꺼짐).
7. **UI 다듬기**: 1차 완료(좌측 내비게이션). 남은 것은 실기 DPI 100/150/200%·키보드 탐색·최소 폭 760px 확인과, SP2가 들어오면 고급 페이지 채우기. 대역 서비스 시험을 실제 서비스 복구 검증으로, 렌더 시험을 실제 DPI/키보드 검증으로, 파일 지문 보존을 다음 로그인 효과 검증으로 대신하지 않는다. 원격 게시/브랜치 병합을 이번 인계 작성 요청에 포함시키지 않는다.

T1~5와 T11은 구현·자체 회귀 완료. T7/8은 구현됐으나 실제 설정 변경 시험이 남고, T6/9/10/12는 위의 지원 계약 또는 평가 항목이 열려 있다. 전체 계획 완료/정식 출시를 선언하지 말 것.

## 6. 주요 코드와 회귀 진입점

| 작업 | 파일/디렉터리 |
|---|---|
| 시작 폴더 native | `src/PcOptimizer.Probes/Actions/Startup/StartupFolderPlatform.cs`, `StartupRunActionAdapter.cs` |
| 시작 출처/보존 | `src/PcOptimizer.Core/Actions/StartupRegistration.cs`, `StartupSelection.cs`, `RollbackRecord.cs`; `Probes/Actions/RollbackCodec.cs` |
| 업데이트 정리/복구 | `src/PcOptimizer.Probes/Actions/SystemCleanup/WindowsUpdateCleanupAdapter.cs`, `UpdateCleanupGuard.cs`, `UpdateServiceMaintenance.cs`, `UpdateServiceRecoveryAdapter.cs` |
| 앱 위치/정리 | `src/PcOptimizer.Probes/Actions/Files/AdobeCacheLocationCatalog.cs`, `SteamCacheLocationCatalog.cs`; `Applications/VideoCacheLocations.cs`, `Applications/ConfigReaders/` |
| 효과 UI | `src/PcOptimizer.App/ViewModels/ActionCenterViewModel.cs`, `ActionPresentation.cs`; `Views/ActionCenterWindow.xaml`, `MainWindow.xaml`/`.cs`; 등록은 `App.xaml.cs` |
| 추가 회귀 | `tests/PcOptimizer.Tests/Unit/App/IntegratedActionTests.cs`, `UpdateMaintenanceTests.cs`, `ActionCenterLayoutTests.cs`; `Smoke/StartupFolderSmokeTests.cs` |
| 패키징 | `tools/package.ps1`, `verify-package.ps1`, `test-package-verifier.ps1`, `README-in-zip.txt` |

새 조치는 기존 공통 관문·실제 작업 종료 대기·대상/세션 재대조·복구 기록 경계를 통해 연결한다. 보호 정책/사용자 경계·새 파일 제외·관측 크기와 실제 효과 구분을 유지한다. 최신 원장 확인과 대응 기록 갱신이 각 작업의 마무리다.

---

## 아래는 이전 이력 (당시 상태)

현재 브랜치 `codex/sp1-actions`. 사용자 인계 지시에 따라 `feature/p0-skeleton`을 로컬 `master`에 fast-forward 병합했다(`77efa2e` → `32ab51f`). 기존 브랜치는 보존했고 원격 게시는 하지 않았다. 아래 날짜별 기록은 당시 상태를 보존한 이력이다.

## 이전 기록 — T10-B Steam 선택 정리·그래픽 공식 안내

- 구현 커밋 **`6b163e8`** (`9687ff4..6b163e8`, 18파일). 후속 문서 커밋은 이 참조만 기록한다.
- 기준 `9687ff4`. [구현 계약·변경 파일·공식 근거·검증 이월](reviews/2026-09-27-steam-cleanup-and-graphics-guides.md). Steam 라이브러리의 shadercache 명시 선택 → 현재 설정 대조 → 30일 경과 AppID 파일 미리보기 → 실행/재검사를 연결했다. Program Files 삭제 보호 유지, Steam/게임 관측·불완전 조회 거절, 파일별 기존 보호 엔진 재사용.
- NVIDIA `NV_Cache`를 추가 검사하고 공식 재부팅/정리 절차로 연결한다. Direct3D는 Windows 저장소, Steam 다운로드 캐시는 별도 공식 안내로 연결한다. NVIDIA 직접 삭제를 활성화하거나 그래픽 전체 유휴를 보장하지 않는다.
- 최종 Release 앱 빌드 경고 0/오류 0. 최초 CS8602 수정 이력은 보고서에 보존. 사용자 순서에 따라 테스트 작성/실행·실기 삭제/게임/브라우저 시험은 미실시. 새 ZIP/버전 없음, preview.6는 이번 구현 미포함.
- 다음 구현은 T6 시작 폴더의 원본 보존/복원 계약 및 앱별 설정 탐지 확장. 이후 T11 전면 UX → T12 종합 검증/통합 배포. 독립 리뷰/서브에이전트 없음. T10 전체 완료가 아니다.

## 이전 기록 — T9-B 업데이트 다운로드 캐시 실행 연결

- 구현 커밋 **`4de30d1`** (`3456a75..4de30d1`, 16파일). 후속 문서 커밋은 이 참조만 보존한다.
- 기준 `3456a75`. [T9-B 구현 계약·파일·한계·검증 이월](reviews/2026-09-27-update-download-implementation.md). 고정 Windows Download의 7일 경과 파일 미리보기 → 서비스별 원본 저장/중지 → 파일 재대조/삭제 → 서비스 복구 → 재검사를 조치 목록에 연결했다.
- WUA/BITS/DO·CBS/재부팅·알려진 업데이트 프로세스 확인, 서비스 재진입 시 중단, 부분 파일 처리와 서비스 복구 결과 구분을 추가했다. 이미 정지된 서비스를 정리 목적으로 켜지 않으며 **wuauserv·BITS 모두 Running 조건**이다. OS 전체 작성자의 배타적 유휴를 보장하는 기능은 아니다.
- Release 앱 빌드 경고 0/오류 0. 사용자 순서에 따라 테스트 작성/실행·실기 서비스/파일 변경은 하지 않았다. 기존 preview.6에 포함되지 않으며 새 ZIP/버전 변경도 없다.
- 다음은 T10-B Steam/그래픽 실행 계약, T6/T10 잔여 지원 범위 → T11 UI/UX → T12 종합 검증/통합 배포다. T9 동작 검증도 T12에 남는다. 독립 리뷰/서브에이전트 없음.

## 이전 기록 — 구버전 정리·T9-A 업데이트 상태/복구 기반

- 구현 커밋 **`2451878`** (`0baa42c..2451878`, 18파일). 후속 문서 커밋은 이 참조만 기록한다.

- 기준 `0baa42c`. [남은 구현 실행 계획](superpowers/plans/2026-09-27-sp1-remaining-implementation.md): T9-A 상태/서비스 복구 기반 → T9-B Download 실행기 → T10 Steam/그래픽 → 잔여 지원 범위 → T11 UI/UX → T12 종합 검증/배포. 기존 Task 번호는 유지한다.
- 구버전 패키지·ZIP·해시·publish 25개 경로(바탕화면 preview.2 포함), 파일 논리 합계 1,403,897,294 bytes 정리. preview.6와 현재 소스/빌드·리뷰 증거 보존. 상세 대상은 `artifacts/maintenance/old-packages-20260927.json`.
- 로컬 업데이트 설치 진행/재시작 필요/서비스 상태 진단과 설명 카드 추가. false를 전체 유휴·삭제 가능으로 판정하지 않는다. 네트워크 업데이트 검색 없음.
- wuauserv/BITS 허용 목록 네이티브 제어, 서비스별 Pending 선행 저장, 중지 관측, 취소와 별개의 복구 기한·실패 기록 보존, 다음 실행 복구 전용 어댑터/UI를 구현했다. **중지 워크플로는 아직 제품에서 호출하지 않으며 Download 파일 삭제도 미연결**이다. T9-A 기반 구현과 T9 전체 완료를 구분한다.
- Release 앱 빌드 경고 0/오류 0. 사용자 우선순위대로 테스트 작성/실행·실기 서비스 제어는 후순위. 실제 호스트 서비스/사용자 캐시/설정 변경 없음. 새 ZIP/버전 변경 없음.
- 다음은 T9-B: 설치 외 다운로드/BITS/CBS·재진입 조건, 고정 Download 대상·원본 파일 집합, 서비스 종료 동안만 파일 처리 및 서비스 복구. [구현 상세와 이월](reviews/2026-09-27-update-maintenance-foundation.md).

## 이전 기록 — 구현 우선 배치: 시스템 시작 등록·사용자 지정 캐시

- 구현 커밋 **`2b32a8e`** (`1aabef1..2b32a8e`, 27파일). 후속 문서 커밋은 이 참조만 보존한다.

- 사용자 최신 작업 순서: **구현 → UI/UX 개선 → 검증**. 독립 리뷰·서브에이전트는 생략한다. 기능별 프리뷰/ZIP은 만들지 않는다. 아래 변경은 구현 완료/컴파일 확인이며 동작 검증 완료가 아니다.
- T6: 기존 HKCU Run에 HKLM Run 64/32비트를 추가했다. `MachineStartup` 시스템 범위, 출처별 복구 키, 모든 사용자 영향 표시, 원문 저장/등록 해제/복원/복원 전 기록 보존을 연결했다. SystemOnly 검사에서는 HKLM·공용 폴더만 조회한다. StartupApproved/RunOnce/시작 폴더 쓰기는 미지원이다.
- T10 Adobe: 사용자가 지정한 `Media Cache Files`·`Peak Files`를 세션 한정 카탈로그로 등록한다. 고정 로컬 드라이브, 기존 보호·프로필·링크 경계, 앱 실행 상태 확인, 90일 이상 `.cfa`/`.pek`만 기존 파일 실행기에 전달한다. 환경설정 자동 파싱으로 찾았다고 표현하지 않는다.
- T10 Resolve·CapCut: 사용자 선택 CacheClip/Cache 경로를 다음 검사에 전달한다. 기존 보호·다른 사용자·시간 예산·부분 관측을 유지하고 선택 경로를 해석 못할 때 기본 위치로 대체하지 않는다. 용량 조회만 하며 삭제하지 않는다. 기본 위치로 돌아가는 버튼도 연결했다.
- Release 앱 빌드 경고 0/오류 0. 전체 테스트/Smoke/실기 검증/새 테스트 작성은 사용자 순서에 따라 뒤로 이월했다. 이전 1620/48 수치는 이번 변경의 검증 수치가 아니다. 실제 사용자 캐시·레지스트리·서비스는 변경하지 않았다. 새 ZIP/버전 변경 없음.
- 상세 계약·파일·검증 대기: [구현 배치 기록](reviews/2026-09-27-implementation-first-expansion.md). 현재 Task 표: [SP1 진행 기록](superpowers/sp1-progress.md).
- **남은 구현**: T9 Windows Update Download/서비스 중지·복구, T10 Steam/NVIDIA 자동 정리 및 앱 설정 자동 탐지 확장, T6 시작 폴더/StartupApproved(지원 계약 미확정). 이후 T11 UX 개선 → T12 검증. 이 배치로 SP1 전체 완료를 주장하지 않는다.

## 이전 기록 — 시작 등록·배달 최적화 실행 통합 (프리뷰 생성 안 함)

- 구현·테스트·보고서 커밋 **`2438648`** (`e7413ea..2438648`, 27파일). 후속 문서 커밋은 이 참조만 기록한다.

- 사용자 최신 지시: **기능 하나마다 프리뷰를 만들고 턴을 끝내지 말고 남은 기능을 묶어서 진행**. 기능 우선·독립 리뷰/에이전트 생략 유지. 이번 버전 변경/ZIP 생성 없음.
- 기준 `e7413ea`. [구현 파일·계약 변경·테스트·실패 이력](reviews/2026-09-27-startup-delivery-actions.md). 현재 사용자 HKCU Run64 등록 해제·원문 복원·개별 카드/선택 목록, 복원 전 원본 기록 자동 만료 방지, Windows 배달 최적화 비고정 캐시의 개별 ID 정리를 구현했다.
- StartupApproved 값을 바꾸지 않고 Run 등록 자체를 해제한다. 다른 시작 출처는 여전히 Windows 설정에서 관리한다. 원본 선행 저장·현재 값 재비교·링크 거절·Full 사용자 범위·재검사를 연결했고 외부 앱과의 원자적 CAS 보장은 없다.
- DO는 공식 도구가 쓰는 같은 로컬 Windows 제공자를 호출한다. pin/다운로드/일시 중지 제외, 계획 이후 새 ID 제외, 호출 직전 재확인·후속 실제 상태 확인, 부분 결과와 실제 작업 종료 대기. 서비스/시스템 폴더 직접 조작 없음. 실제 PC에서는 조회만 했고 삭제는 대역 검증이다.
- Release 0/0, 기본 1620/1620, 최종 전체 Smoke 48/48. 명령·실패 이력은 위 보고서 참조. 실제 Run 쓰기는 GUID 전용 키에만 수행했다. 원장 독립 상태는 유지한다.
- 잔여: Task Manager 형식/다른 시작 출처, Update 서비스·Download, 사용자 지정 Adobe·Steam/NVIDIA 자동 삭제와 UAC/다른 계정/실제 로그인·DO 삭제/화면 복구 등 실기 평가. 기존 preview.6 ZIP에는 이번 변경이 없으며 제품 소스·Release 빌드만 갱신했다.

## 이전 기록 — Steam·그래픽 캐시 위치별 검사 / preview.6

- 구현·검증·배포 기록 커밋 `40ae676` (`9bafe82..40ae676`, 23파일). 후속 문서 커밋은 이 참조를 남긴다.
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
