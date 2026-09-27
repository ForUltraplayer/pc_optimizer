# SP5 문제 해결 도구함 · SP3 드라이버 안내 — 구현 기록 (2026-09-27, Claude)

기준 `9180979`(T6·T10 배치 후). 구현 커밋 **`5da2700`**(SP5 본체), `00e6089`(실행기 Smoke), `904992c`(창 레이아웃 회귀), **`081ecbd`**(SP3). 사용자 지시: SP5 → SP3 순서로 구현 우선, 독립 리뷰·서브에이전트 없음, 검증 최하위. 구현자 자체 기록이며 독립 승인이 아니다. 계획: [SP5 계획](../superpowers/plans/2026-09-27-sp5-troubleshooting.md). 스펙: 2차 개선 §5·§5A.

## 1. SP5 — 문제 해결 도구함

### 데이터
- `rules/troubleshooting-tools.json`(Probes 어셈블리 임베드 `PcOptimizer.Rules.troubleshooting-tools.json`, 파일 시스템에서 읽지 않음). 도구 27개, 증상 8개(느려요·부팅/블루스크린·화면/게임·인터넷·저장장치·메모리·발열/소음·새로 설치). 도구마다 언제/무엇/주의 한 줄, 실행 방식, 안전 수준, 절차(번호 목록, 끝에 "도구 화면이 바뀌면 다를 수 있음"), 경고(되돌릴 수 없음은 필수), 재부팅 여부.
- 외부 도구 링크 15개는 `rules/vendor-links.json`에 `kind=tool·key=troubleshooting`으로 추가해 기존 `LinkPolicy`(호스트+경로 접두 허용)로 연다. 카탈로그는 `linkId`로 참조만 하며, 파서가 링크 표에 없는 ID를 거절한다(부분 적용 없음).

### 실행 방식(스펙 표)
| 방식 | 구현 |
|---|---|
| 직접 실행 | `Core/Troubleshooting/RepairCommandCatalog`의 닫힌 목록: DISM /Online /Cleanup-Image /RestoreHealth, sfc /scannow, chkdsk {SystemDrive} /scan, chkntfs /C {SystemDrive}(재부팅 예약), ipconfig /flushdns, netsh winsock reset. `RepairCommandRunner`가 System32 절대 경로·고정 인자·셸 없음·작업 폴더 System32·주입 가능 환경 변수 제거(기존 `CacheToolProcess` 규칙)로 실행하고 출력을 32KiB 상한 안에서 줄 단위로 전달. 취소·시간 상한(DISM 45분, SFC/chkdsk 20분, 나머지 2분) 시 프로세스 트리 종료. 출력 인코딩은 널 바이트 비율로 UTF-16(sfc)·콘솔 코드 페이지를 구분. 시스템 복원 지점은 WMI `SystemRestore.CreateRestorePoint`(1058 = 시스템 보호 꺼짐을 별도 안내). 실행은 `OperationCoordinator` Apply 관문으로 검사·조치와 상호 배제. |
| 내장 도구 열기 | `BuiltInToolCatalog`: MdSched.exe, mmc diskmgmt.msc, mmc eventvwr.msc, perfmon /rel, 설정 `ms-settings:troubleshoot`(SettingsUriPolicy 허용 목록에 추가). |
| 외부 도구 안내 | 링크만 열고 파일을 받지 않음. 버전 미안내. 스트레스·DDU·설치 USB는 '주의'/'되돌릴 수 없음' + 경고 첫 줄. |

### 화면
- `TroubleshootingWindow`: 왼쪽 증상 버튼(+모든 도구), 오른쪽 순서별 카드(N단계·안전 배지·언제/무엇/주의·경고·절차 펼치기·실행/열기/공식 사이트), 실행 중 출력 패널(진행·취소·결과·재부팅 안내). 메인 화면 "문제 해결 도구 · 증상별 절차" 버튼. 실행 중에는 창을 닫지 않는다.

### 검증(구현자)
- `TroubleshootingTests` 10건(임베드 카탈로그 전체 검증, 링크 표 없으면 전체 거부, 파서 참조 거절 4종, 시작 정보 고정·환경 정리, 이 PC System32 실행 파일 존재, 서비스 복원 지점 코드·내장 도구 고정 인자·Busy, 화면 모델 라우팅), `TroubleshootingLayoutTests` 1건(오프스크린 렌더), `RepairCommandSmokeTests` 1건(실제 `ipconfig /flushdns` 실행: 시작·출력·종료 코드 0).
- 한계: DISM/SFC/chkntfs/netsh/복원 지점은 이 PC에서 실제로 돌리지 않았다(시스템 변경). 명령별 실제 출력 인코딩·진행 표시·시간 상한은 평가 PC에서 확인해야 한다. 복원 지점은 Windows가 24시간 안에 만든 지점이 있으면 새로 만들지 않고 성공을 돌려줄 수 있어 문구로 알린다.

## 2. SP3 — 드라이버 안내

- 새 프로브 `hardware.deviceDrivers`(`DeviceDriverProbe`): Win32_PnPSignedDriver에서 NET(가상 어댑터·Bluetooth·디버그 제외, 이름으로 유선/무선 구분), MEDIA(Realtek 등 오디오만, 그래픽카드 HDMI·가상·Bluetooth 제외), SYSTEM(Intel/AMD 플랫폼 장치: Chipset/SMBus/PCI Express Root/GPIO/I2C 등)을 분류해 이름·버전·날짜·제공자만 기록(장치 ID·일련번호 미기록). 분류당 8개 상한.
- 이 PC 관측(2026-09-27): 유선 Intel I225-V, 무선 Qualcomm FastConnect 7800, 오디오 Realtek HDA, 칩셋 AMD GPIO/I2C/SMBUS. Bluetooth Hands-Free 오디오가 '오디오'로 잡혀 제외 목록에 Bluetooth·Hands-Free를 추가했다. USB DAC(TOPPING E1x2)는 이름에 오디오 표기가 없어 잡지 않는다(범위 밖으로 둠).
- `Core/Drivers/DriverGuideBuilder`: 섀시(노트북/데스크톱)에 따라 시스템 제조사·모델 또는 메인보드 제조사·제품명을 고르고, 기존 `VendorLinkCatalog.FindOem`으로 공식 지원 첫 화면 링크를 붙인다. 표에 없으면 "모델명 복사 → 제조사 검색창" 안내. 받을 네 가지(칩셋 → 유선 랜 → 무선 랜 → 오디오)에 한국어 이름·영어 표기·메뉴 위치·설치 순서·현재 설치 버전(날짜)을 붙인다. 그래픽은 검사된 GPU 제조사의 공식 링크(없으면 NVIDIA·AMD·Intel 모두). 최신 여부는 판단하지 않는다고 명시.
- `DriverGuideWindow`/`DriverGuideViewModel`: 검사 결과(`MainViewModel.LastResult`) 변경 시 자동 갱신, 모델명 복사(클립보드), 지원 페이지·그래픽 링크는 `LinkPolicy` 허용 목록만. 메인 화면 "드라이버 안내 · 무엇을 받을지" 버튼.
- 검증: `DriverGuideTests` 14건(식별 우선순위·OEM 링크·설치 항목·검사 전·분류기 10케이스·날짜·화면 모델 명령). `ScanServiceTests` 프로브 범위 표에 추가. 한계: 벤더별 모델 페이지 URL 규칙은 만들지 않았다(표 정책: 지원 첫 화면만). 노트북 실기 확인 없음.

## 3. 전체 검증

- Release 솔루션 빌드 경고 0/오류 0(각 커밋 시점). 기본 필터·Smoke 전체 실행 수치는 HANDOFF 4절과 원장에 적는다. Online·ToolSmoke 미실행(수집·도구 코드 변경 없음).
- 새 ZIP: preview.9로 갱신(HANDOFF 3절).

## 4. UI 다듬기 전 남은 것(사용자 지시: SP5·SP3 뒤 UI)
- 메인 화면에 창 진입 버튼이 4개(조치·주사율·문제 해결·드라이버)로 늘어 배치 정리가 필요하다. 좌측 내비게이션 구조(사용자 확정 배치안)로 옮기는 것이 다음 UI 작업이다.

## 5. 후속(2026-09-27 저녁, 사용자 시험 뒤)

- **링크 버튼이 안 먹던 문제**: 로그에 `LinkOpenFailed … InvalidOperationException`. 원인은 이 PC가 UAC를 끈 상태(EnableLUA=0, 내장 Administrator)라 탐색기 자체가 관리자 토큰이고, 실행기가 "비승격 셸이 아니면 거절"했기 때문. 세션의 데스크톱 셸(같은 세션·같은 창·같은 프로세스)이면 승격 여부와 무관하게 셸의 ShellExecute에 위임하도록 완화(`b5b8992`). 앱이 브라우저를 직접 시작하는 경로는 여전히 없다. 승격된 셸이었는지는 `ShellWasElevated`로 남긴다. 같은 COM 경로를 이 PC에서 직접 호출해 Chrome이 열리는 것을 확인했고 `DesktopShellSmokeTests`는 승격 셸도 세션 셸로 인정하도록 바꿨다.
- **외부 링크 실제 확인**: 15개 중 14개 200, HWiNFO는 스크립트에 403(봇 차단)이지만 브라우저에서는 정상. DDU·OCCT는 리디렉션 최종 주소로 정정(`2895c4a`). 점검 스크립트 `tools/check-links.ps1` 추가(배포 전 실행 권장).
- **유틸리티 탭**(`960fe62`): 좌측 내비게이션에 '유틸리티' 추가. 외부 도구를 분류별 카드 섹션(언제/무엇/주의·안전 배지·절차 펼치기·공식 사이트 열기)으로 보여 준다(`UtilitiesView`, `TroubleshootingViewModel.UtilityGroups`).
- **DISM 0x800f0915**: 사용자가 이 PC에서 실행해 '복구 콘텐츠를 찾을 수 없음'으로 실패. 진단(읽기 전용): WU/BITS/DO 서비스 실행 중, 차단 정책 없음, dism.log에 CBS 0x800f0915. 프리뷰 빌드(26200)라 WU에 복구 페이로드가 없거나 AdGuard가 막았을 가능성. 대응: (1) 알려진 종료 코드(0x800f0915/081f/0906/0954)를 한국어 안내로 바꿈(`ExplainExitCode`), (2) 새 직접 실행 명령 `dism-restorehealth-source` = `/Online /Cleanup-Image /RestoreHealth /Source:{WIM|ESD:…\sources\install.wim|esd:1} /LimitAccess`. 사용자가 고른 폴더는 Core `InstallMediaSource`가 sources\install.wim/esd 존재·절대 경로·UNC/와일드카드 금지로 검증한 뒤에만 인자로 바뀐다(명령 목록은 여전히 닫혀 있음). 카탈로그에 도구·절차와 증상 순서를 추가.
- 검증: Release 0/0, 기본 1693/1693, Smoke 58/58, 유틸리티·문제 해결 오프스크린 렌더 확인, preview.11 ZIP 검증 통과. DISM /Source 실제 실행은 같은 빌드의 ISO가 없어 이 PC에서 시험하지 못했다.
