# 고급 탭 조사 — 0.0.2 이후 (2026-09-28)

기준 저장소 `472865a`, 현재 앱/공개 릴리즈는 0.0.1이다. 사용자가 지정한 **0.0.2 이후 후속 작업**으로 조사·구현 순서를 정리한다. 버전 번호를 올리거나 미구현 기능을 출시한 문서가 아니다. 제품 코드·사용자 PC 설정은 변경하지 않았다.

## 결론과 기존 스펙 정정

사용자가 제안한 ReBAR, MPO fix, NVIDIA/AMD 동영상 보정, UEFI/안전 모드 재부팅을 고급 탭 후보로 잡는다. 공식 기능/드라이버 API를 우선 재사용하며, 직접 토글을 만들 수 있는지와 사용자가 실제 효과를 확인할 수 있는지는 별개로 판단한다.

- 이전 SP2 스펙의 “ReBAR는 앱에서 변경 불가”는 **펌웨어 활성화**에는 해당하지만 게임별 NVIDIA 프로필 설정까지 포함하면 부정확하다. NVIDIA App 11.0.9가 게임별 ReBAR 제어를 공식 제공한다. [S1]
- 이 문서의 `직접 구현`은 API/지원 계약 근거가 있는 개발 후보라는 뜻이다. 이 프로젝트에서 실행·복구 검증을 완료했다는 뜻이 아니다.
- 사용자는 “MPO fix”가 **여러 GPU 옵션을 묶은 프로그램**이라고 확인했다. 이 조사에서는 `RedDot-3ND7355/MPO-GPU-FIX`를 기준으로 삼았다. 정확한 저장소 주소까지 사용자가 지정한 것은 아니며, 뒤쪽 커뮤니티 옵션 전체의 채택 확정도 아니다. [S4,S5]

## 1. 요청 항목별 구현 가능성

| 항목 | 확인한 근거 | 구현 방향 | 남은 조건 |
|---|---|---|---|
| NVIDIA ReBAR | 최신 공식 앱에 게임별 제어가 있음. 펌웨어/하드웨어 전제는 별도 | BIOS 활성 상태 + 게임별 프로필 `자동/켜기/끄기` 분리. NVIDIA App 안내와 DRS 기반 직접 제어를 별개 태스크로 진행 | 실제 드라이버별 설정 존재·형식·기본값/상속·읽기/쓰기/복구 검증 |
| MPO | NVIDIA 공식 지원 문서에 끄기/복원 REG 파일 및 재부팅 절차 | `MPO 사용 안 함`과 `Windows 기본 동작`을 명확히 표시하고 원래 값 복원 제공 | 공식 첨부 원문 고정/검증, 원래 값 존재 여부·타입·내용 보존, 재부팅 전후 상태 구분 |
| NVIDIA RTX Video Super Resolution | 공식 앱에 동영상 설정 제공. 2024 NVAPI 담당 답변은 공개 SDK 제어 미지원. 비공개 함수 이용 오픈소스 사례는 있음 | 공식 설정 경로 안내 + 직접 제어 기술 검증을 진행. 함수 탐지/원본 읽기·재조회·복원까지 검증한 드라이버 조합만 직접 토글 | 2024 답변을 2026 현재의 불가능 증명으로 쓰지 않음. 비공개 ABI와 지원 버전 확인이 필요 |
| AMD Video Upscaling | ADLX `IADLXVideoUpscale`에 지원·현재 상태·켜기/끄기·선명도 API 제공(최소 1.4) | AMD 어댑터 선택 후 직접 On/Off, 지원 범위 내 선명도, 원상 복원 | 실제 AMD GPU/드라이버에서 읽기·쓰기·복원. Image Sharpening과의 종속성 |
| BIOS/UEFI로 재부팅 | Windows `shutdown /r /fw /t 0`, GetFirmwareType | UEFI 여부 확인 후 목적/저장 안내와 별도 실행 확인. BIOS 값을 변경하는 기능과 구분 | UEFI라고 진입 지원을 단정하지 않음; 펌웨어 요청 실패·사용자 앱 종료 거절 처리 |
| 안전 모드로 재부팅 | Windows 고급 시작 → 시작 설정 → 안전 모드 절차 | 먼저 `시작 설정으로 재부팅` + 재부팅 후 안전 모드 선택 안내를 제공 | `/o`는 안전 모드 직행이 아니다. 직행은 일회성 부팅 설계/복구 검증을 별도 수행 |

근거: ReBAR [S1–S3], MPO [S4], NVIDIA 영상 [S7–S9], AMD 영상 [S10–S11], 재부팅 [S13–S15].

### ReBAR: 옛 ID를 고정하면 의미까지 달라진다

NVIDIA Profile Inspector의 현재 원본 `CustomSettingNames.xml`에서 다음을 확인했다. 이 메타데이터는 프로젝트 저자의 구현 근거이며 NVIDIA의 공식 호환성 보증이 아니다. [S3]

| 설정 | ID | 값 |
|---|---|---|
| 기존 rBAR Enable | `0x000F00BA` | 0=끔, 1=켬 |
| rBAR Enable v2 | `0x000BFA21` | 0=끔, **1=자동**, 2=켬. 메타데이터상 최소 드라이버 616.56 |

따라서 “1 쓰기 = 켜기”를 공통으로 쓰면 잘못 구현된다. 드라이버 버전 문자열만 믿지 말고 실제 설정 가용성/형식을 조회한다. 게임별 프로필 변경을 기본으로 설계하고, 전역 강제 적용이나 size limit·CPU exclusion까지 함께 바꾸지 않는다. 기본값 복원과 이전 사용자 값 복원도 구분한다. 프로필 `켜기`는 해당 게임의 실제 실행 중 사용 증명이 아니며 BIOS의 꺼짐을 우회하지 못한다. BAR1 크기만으로 지원/활성을 확정하는 프로브도 채택하지 않는다. [S1–S3]

AMD SAM도 함께 정보 표시 후보로 둔다. **최신 ADLX 문서에서 `IADLXSmartAccessMemory.SetEnabled`는 deprecated이고 BIOS 변경을 지시한다.** 과거 예제에 setter가 있다는 이유로 현행 직접 토글을 약속하면 안 된다. 읽기는 `IsSupported/IsEnabled`를 검토한다. [S12]

### 영상 보정: 기능 이름과 종속성

- AMD의 대응 기능은 **Video Upscaling**이다. 게임용 Radeon Super Resolution(RSR), 고해상도로 렌더하는 Virtual Super Resolution(VSR)과 구분한다. ADLX는 보간한 영상의 선명도 보정으로 설명하므로 NVIDIA RTX Video와 같은 AI 처리라고 표시하지 않는다. [S10–S11]
- ADLX 문서는 해당 GPU에서 Radeon Image Sharpening이 켜져 있으면 Video Upscaling 설정을 무시한다고 명시한다. 단순히 setter가 성공했다고 `현재 영상에 적용됨`으로 표시하지 않는다. [S10]
- NVIDIA FAQ는 **RTX Video Super Resolution/HDR 활성화 시 MPO가 자동 비활성화**된다고 설명한다. 레지스트리의 MPO 강제 비활성 설정과 드라이버의 실제 영상 처리 상태는 별도 필드로 표시한다. [S7]
- NVIDIA의 지원 재생기·GPU 사용 경로·전력·GPU 부하 조건이 있으므로 설정값 On과 영상 처리 Active를 구분한다. 품질 Auto도 유지한다. 사용자 앱 설정 전체나 브라우저 flags를 무조건 고치지 않는다. [S7]
- AMD 24.1.1 문서의 단일 모니터/DX11/브라우저 flags 조건은 **그 버전의 기록**이다. 현행 드라이버/브라우저에도 그대로 적용한다고 쓰지 않는다. 현재 지원 여부는 ADLX 및 해당 드라이버 문서로 다시 판단한다. [S11]

## 2. MPO-GPU-FIX 기능별 검토

아래는 “프로그램에 이런 옵션이 있다”는 조사다. 작성자의 성능/증상 주장을 모든 PC에서 확인한 사실로 쓰지 않는다. 실제 적용 내용은 구현 전 소스/버전으로 고정해 재확인한다. [S5]

| 옵션 | 제안 |
|---|---|
| MPO 끄기/복원 | 공식 NVIDIA 절차가 있으므로 우선 구현 후보. 화면 문제 진단용이며 항상 끄기 권장 아님 |
| HAGS | 기존 SP2 항목과 통합. `Fix 켜기` 대신 `GPU 하드웨어 가속 일정 예약 켜기/끄기`로 표시 |
| Shader Cache | 캐시 사용/크기 설정과 기존 캐시 삭제 기능을 분리. AMD는 ADLX 공식 초기화 API도 별도 후보 |
| ULPS | AMD 세대·전력/절전 영향 확인 뒤 별도 판단. 원 프로젝트도 RX 9000 비활성화 경고를 명시. 일괄 비활성화 제외 제안 |
| TDR Delay/Level | 일반 최적화 토글에서는 제외 제안. Microsoft는 드라이버 개발 테스트 외 일반 앱/사용자가 변경하지 말라고 명시 [S6] |
| OverlayMinFPS / Disable Overlays / Force Direct Flip | Windows 버전별 의미·충돌 근거 확인 전 미채택. MPO와 동일 옵션인 것처럼 합치지 않음 |
| DX MOD / HDAUDBUS MSI 모드 | 드라이버 구성/장치 인터럽트 변경이라 첫 고급 배치에서 제외 제안. 개별 지원 계약 조사 대상으로만 유지 |

MPO restore는 “Windows가 지원 조건에 따라 사용하는 기본 동작으로 복귀”다. 실제 화면에서 MPO가 항상 동작하도록 강제하는 `On`과 동일하게 설명하지 않는다. 사용자 요청 “on/off”를 구현할 때도 이 차이를 버튼/상태 설명에 반영한다.

## 3. 재부팅의 실행 계약

명령은 개발 중 이 PC에서 실행하지 않았다. 다음은 제품 구현 설계이며 사용자에게 지금 실행하라는 지시가 아니다.

- UEFI: System32의 고정 `shutdown.exe`, 인자 `/r /fw /t 0`; 복구 메뉴: `/r /o /t 0`를 후보로 검증한다. `/f`는 추가하지 않는다. **Windows shutdown은 `/t`가 0보다 크면 `/f`를 암묵 적용한다.** 카운트다운이 필요하면 앱 내부에서 취소 가능한 준비 시간을 두고 최종 확인 이후 0초 명령을 보낸다. [S13]
- `shutdown /r /o`는 WinRE/고급 시작 진입이며 안전 모드를 자동 선택하지 않는다. UI도 그렇게 표시한다. WinRE 활성 상태·확인 실패를 구분하고, 이 단계에서 임의 활성화나 복구 파티션 변경을 하지 않는다. [S14–S15]
- 현재 BCD의 `safeboot`를 지속 설정하는 간단 구현은 채택하지 않는다. 안전 모드 직행을 구현하려면 일회성 부팅 대상, 정상 부팅 복귀, 취소/실패/앱 강제 종료/BitLocker 영향을 별도 평가한다. 메뉴 경유 버전을 직행 구현 완료로 집계하지 않는다.
- 저장되지 않은 작업 종료 여부는 Windows 앱 종료 흐름을 존중한다. 다른 검사·정리·복원이 진행 중이면 공통 OperationCoordinator에서 거절한다. 명령 전달 후 실제 부팅 도착을 확인한 것처럼 성공 문구를 쓰지 않는다.
- 암호화 복구 키가 필요할 수 있으며 안전 모드에서 PIN 대신 계정 암호가 필요할 수 있음을 해당 확인 화면에만 안내한다. 복구 키/암호를 수집하거나 보고서에 저장하지 않는다. [S14]

## 4. 별도 추가 후보

아래는 사용자의 기존 목록 외에 조사한 **제안**이다. 채택/구현 완료를 뜻하지 않는다.

| 후보 | 사용자에게 보이는 이득 | 구현 방식/우선순위 |
|---|---|---|
| 게임 모드·HAGS | 게임 실행 환경을 바꾸고 문제 발생 시 원복 | 기존 SP2 항목을 유지. 지원 여부·OS 기본값·재부팅 상태부터 정확히 구분. HAGS는 무조건 켜기/끄기 추천 안 함 [S16] |
| 창 모드 게임 최적화 | DX10/11 창/테두리 없는 창의 표시 지연 개선 가능 | Windows 공식 설정 및 앱별 예외 연결 우선. Auto HDR와 종속성, 게임 재시작 안내. 높은 우선순위 [S17] |
| 앱별 GPU 선택 | 노트북/복수 GPU에서 게임·영상 앱이 의도한 GPU를 사용하도록 설정 | Windows 선택 UI 연결 → 검증된 직접 제어 계약 별도. 일괄 고성능은 배터리 영향 [S7,S17] |
| HDR·Auto HDR·RTX Video HDR와 HDR 보정 | 영상 색/밝기 개선, 지원 모니터 식별 | 게임 Auto HDR와 영상 RTX HDR를 분리. Windows HDR Calibration 연결. 품질 개선이며 FPS 증가로 표시하지 않음 [S7,S18] |
| VRR / G-SYNC / FreeSync | 주사율 변화에 맞춘 화면 찢어짐 완화 | 디스플레이별 지원/드라이버 상태 확인 및 공식 설정 연결. 서로 다른 Windows VRR 옵션과 모니터/드라이버 기능을 하나의 스위치로 합치지 않음 [S19] |
| AMD 공식 셰이더 캐시 초기화 | 문제 발생 시 제조사 API로 초기화 | ADLX `IADLX3DResetShaderCache` 가용성 조회. 기존 파일 삭제와 중복 구분, 재컴파일 비용/복원 불가 표시. 정리 탭에서 구현하고 고급에서 연결 [S20] |

고정 “최적화 모두 적용”, HPET/타이머·TDR 비활성·보안 기능 해제·드라이버 파일 교체 묶음은 이 조사에서 추가 추천하지 않는다. 직접적인 사용자 문제/지원 근거가 있는 기능부터 넣는다.

## 5. UI/엔진에 필요한 구분

- 카드: 현재 **설정값**, 실제 **활성 상태(확인 가능한 경우)**, **지원 여부**, 영향, 적용 방법, 원상 복원, 재시작 필요 상태.
- 읽기 실패/미지원/드라이버 기본값을 Off로 치환하지 않는다. `Windows/드라이버 기본값`, `사용자 설정`, `확인 불가`를 표현한다.
- 토글 변경 시 곧바로 OS에 쓰지 않고 새 값 선택 → 영향/대상 확인 → 실행으로 이어진다. 외부 설정창을 열었다는 이유로 상태를 On으로 변경하지 않는다.
- 등록된 옵션 ID와 형식화된 대상만 실행한다. JSON에서 임의 레지스트리 경로·값·실행문을 받아 쓰는 엔진을 만들지 않는다.
- 원본 존재 여부/타입/값·대상 GPU/프로필·SID/세션·드라이버 버전을 보존하고, 복원 시 현재 값이 앱이 마지막 적용한 값인지 비교한다. 외부 변경을 덮어쓰지 않는다.
- HKCU 옵션은 기존 사용자 범위와 SystemOnly 제한 유지. 시스템 옵션도 실제 세션 및 실행 직전 조건 재확인. NVAPI/ADLX는 보호된 설치 경로 DLL만 사용하고 다운로드 폴더 도구를 관리자 권한으로 실행하지 않는다.
- 재부팅이 필요한 설정은 `저장됨·재부팅 필요`와 `재검사 확인`을 분리한다. 이전 부팅인지 확인할 수 없으면 적용 완료라고 추정하지 않는다.
- 화면은 `게임/그래픽`, `동영상`, `문제 대응`, `다시 시작`으로 묶는다. 기존 탭 위치 기억·중첩 스크롤 규칙을 이어 쓴다.

## 6. 조사 한계와 다음 실행

현재 PC에서 GPU 설정 토글/재부팅/레지스트리 변경은 하지 않았다. 외부 저장소 코드를 복사하거나 실행 파일을 배포물에 동봉하지 않았다. 외부 도구 재사용/동봉 시 고정 버전·라이선스·고지·신뢰 경로를 별도 확인한다.

다음 구현 순서와 파일/완료 조건은 [고급 탭 구현 계획](../superpowers/plans/2026-09-28-advanced-options.md)에 기록한다. 이번 문서는 조사와 계획이며 사용자 추가 후보 채택이나 실제 장치 호환성을 대신하지 않는다.

## 출처 (2026-09-28 직접 확인, 공식/프로젝트 원본)

후속 구현에서 NVIDIA ReBAR/영상의 실제 조회 ABI와 AMD 공식 인터페이스 연결을 확인했다. 고정 커밋·실제73프로필/3출력 결과·Enabled/품질 필드 정정·실기 쓰기 미검증 및 배포 고지는 [GPU 베타 기록](../reviews/2026-09-28-gpu-beta.md)을 따른다. 이전 문단의 ‘검증 남음’은 조사 당시 기록이다.

- [S1 NVIDIA App 최신 릴리즈 안내 — 11.0.9 게임별 ReBAR](https://www.nvidia.com/en-us/software/nvidia-app/release-highlights/)
- [S2 NVIDIA ReBAR 하드웨어·BIOS·게임별 적용 안내](https://www.nvidia.com/en-us/geforce/news/geforce-rtx-30-series-resizable-bar-support/)
- [S3 NVIDIA Profile Inspector 설정 원본](https://raw.githubusercontent.com/Orbmu2k/nvidiaProfileInspector/master/nvidiaProfileInspector/CustomSettingNames.xml), [프로젝트·프로필 백업 안내](https://github.com/Orbmu2k/nvidiaProfileInspector)
- [S4 NVIDIA 공식 MPO 끄기/복원](https://nvidia.custhelp.com/app/answers/detail/a_id/5157)
- [S5 MPO-GPU-FIX 원 프로젝트](https://github.com/RedDot-3ND7355/MPO-GPU-FIX)
- [S6 Microsoft TDR 레지스트리 용도](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/tdr-registry-keys)
- [S7 NVIDIA RTX Video FAQ](https://nvidia.custhelp.com/app/answers/detail/a_id/5448)
- [S8 NVIDIA NVAPI 담당 답변(2024)](https://forums.developer.nvidia.com/t/implement-feature-to-switch-nvidia-video-super-resolution-via-nvapi/289524)
- [S9 비공개 VSR 함수 사용을 명시한 LocalVSR 프로젝트](https://github.com/vyomanaut/local-video-upscaler)
- [S10 AMD ADLX IADLXVideoUpscale](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/multimedia/iadlxvideoupscale/)
- [S11 AMD 24.1.1 릴리즈 기록 — Video Upscaling](https://www.amd.com/en/resources/support-articles/release-notes/RN-RAD-WIN-24-1-1.html)
- [S12 AMD ADLX SmartAccessMemory — SetEnabled deprecated](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/gpu-tuning/iadlxsmartaccessmemory/)
- [S13 Microsoft shutdown](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/shutdown)
- [S14 Microsoft Windows 시작 설정](https://support.microsoft.com/en-au/windows/experience/startup-boot/windows-startup-settings), [안전 모드 로그인](https://support.microsoft.com/en-us/windows/security/identity-signin/troubleshoot-problems-signing-in-to-windows)
- [S15 Microsoft GetFirmwareType](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getfirmwaretype)
- [S16 Microsoft HAGS 설계](https://devblogs.microsoft.com/directx/hardware-accelerated-gpu-scheduling/)
- [S17 Microsoft 창 모드 게임 최적화와 앱별 GPU 선택](https://support.microsoft.com/en-us/windows/hardware/display-graphics/optimizations-for-windowed-games-in-windows-11)
- [S18 Microsoft HDR Calibration](https://support.microsoft.com/en-us/windows/hardware/display-graphics/calibrate-your-hdr-display-using-the-windows-hdr-calibration-app)
- [S19 Microsoft 그래픽 설정·VRR 설명](https://devblogs.microsoft.com/directx/navigating-the-redesigned-graphics-settings-page/)
- [S20 AMD ADLX 셰이더 캐시 초기화](https://gpuopen.com/manuals/adlx/adlx-sdk-references/adlx-interfaces/3d-graphics/iadlx3dresetshadercache/)
