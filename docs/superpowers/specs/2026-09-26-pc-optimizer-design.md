# PC Optimizer 설계 문서

- 작성일: 2026-09-26
- 상태: 1차(조회 전용 진단 + 결과 화면) 설계 승인본
- 스택: C# / .NET 8 / WPF

## 1. 목표와 범위

컴퓨터를 잘 모르는 사용자가 자기 PC의 상태를 한 화면에서 이해하고, 무엇을 바꾸면 좋은지와 왜 그런지를 납득한 뒤 스스로(또는 2차부터는 자동으로) 조치할 수 있게 하는 Windows 11용 도구.

차별화 가설: "합쳐진 도구가 없다"가 아니라 "사용자 상황에 맞는 판단과 조치 연결이 부족하다". 기존 도구는 정리(winapp2/BleachBit/PC Manager), 설정 감사(FPS Doctor), 트윅 적용(WinUtil/Winhance), 드라이버(TinyNvidiaUpdateChecker), LLM 폴더 판정(c-cleaner)으로 조각나 있고, 어느 것도 측정·근거·판단·영향을 초보자 언어로 묶어 주지 않는다.

### 1차 범위 (이 문서)

- 조회 전용 진단 엔진과 결과 화면
- 측정 사실 / 권장 판단 / 확인 불가를 구분해 표시
- 자동 조치 없음. `적용` 버튼은 모델에만 존재하고 UI에서 비활성
- LLM 없음. 인터페이스만 예약

### 범위 밖 (2차 이후)

- 관리자 실행기(`PcOptimizer.Elevated`)와 자동 조치. 진단 정확도 검증 후 추가
- LLM 선택 모듈(`PcOptimizer.Llm`): 미분류 폴더 조사, 우선순위, 개인화 설명
- 격리 폴더와 실제 삭제
- Windows 10 지원

### 성공 기준

1. 불필요한 경고가 적다. "차이 발견"을 "문제"로 표현하지 않는다
2. 모르는 것은 `확인 불가`와 이유로 표시한다
3. 모든 권장에 측정값과 근거가 붙어 사용자가 이유를 납득할 수 있다
4. 검사 항목 수는 성공 기준이 아니다

## 2. 솔루션 구조

| 프로젝트 | 종류 | 역할 |
|---|---|---|
| `PcOptimizer.Core` | 클래스 라이브러리 (net8.0) | 데이터 모델, `IProbe`/`IRule` 인터페이스, 규칙 엔진, winapp2 형식 파서, 보호 목록, 리포트 모델. Windows API 의존 없음 |
| `PcOptimizer.Probes` | 클래스 라이브러리 (net8.0-windows) | WMI, 레지스트리, Win32 P/Invoke, NVIDIA 조회, Windows Update Agent 등 수집기 구현 |
| `PcOptimizer.App` | WPF (net8.0-windows) | 결과 화면. MVVM(CommunityToolkit.Mvvm), Fluent 스타일(WPF-UI) |
| `PcOptimizer.Tests` | xUnit | 규칙·파서 단위 테스트, 프로브 스모크 테스트(별도 트레이트) |
| `rules/` | 데이터 | `winapp2.ini`(CC-BY-SA 4.0, 출처·라이선스 표기), `supplement.ini`(우리 보충 규칙, 같은 형식, CC-BY-SA 유지), `protect.json`(보호 경로), `vendor-links.json`(드라이버·OEM 링크 표) |

의존 방향: `App → Probes → Core`, `App → Core`. Probes는 App을 모른다. Core는 어느 쪽도 모른다.

예약 인터페이스(Core에 정의, 1차 구현 없음):
- `IElevatedExecutor`: 작업 계획을 받아 실행하고 결과를 반환
- `IUnknownFolderAdvisor`: 미분류 폴더 요약을 받아 소유 앱 추정과 조사 방향을 반환

## 3. 데이터 모델

모든 검사 결과는 `Finding` 하나로 통일한다.

```
Finding
  Id            : string          예) display.refresh.DISPLAY3
  Category      : enum            Display, Memory, Driver, Power, Graphics, Security, Storage, AppCache, Startup, Unclassified
  Title         : string          한 문장. 예) "모니터 3에서 더 높은 주사율을 쓸 수 있어요"
  Measured      : Measurement[]   { Name, Value, Unit?, Source }  Source는 API/WMI 클래스/레지스트리 키
  Evidence      : string          판단 근거 한 줄
  Verdict       : enum            Ok(정상 확인) | Candidate(개선 후보) | CannotVerify(확인 불가) | Info(정보)
  CannotVerifyReason : string?    ElevationRequired | NetworkFailed | NoRule | Unsupported | ProbeError(요약)
  Recommendation: { Text, Condition }?   예) "같은 해상도에서 130Hz 선택 가능", "노트북은 전원 연결 시"
  Impact        : { Benefit, SideEffect }?  예) "스크롤이 부드러워짐", "일부 캐시는 다음 실행 때 다시 생성"
  Actions       : Action[]        ShowDetails | OpenSettings(uri) | Keep | Apply(2차, UI 비활성) | OpenLink(url)
```

`Verdict`에 "문제" 등급은 없다. 측정값과 권장의 분리가 이 모델의 목적이다.

## 4. 엔진 흐름

수집과 판정을 분리한다.

1. **Collect**: 각 `IProbe { Id, RequiresElevation, RequiresNetwork, RunAsync(ct) }`를 병렬 실행. 프로브별 타임아웃. 실패 시 예외를 밖으로 내지 않고 `CannotVerify` 측정 결과를 반환한다. 검사 전체는 어떤 프로브가 죽어도 끝까지 간다
2. **Evaluate**: `IRule`은 측정값 집합만 입력받아 `Finding[]`을 내는 순수 함수. 가짜 측정값으로 단위 테스트한다
3. **Report**: 화면용 모델 + JSON 내보내기(문의·디버깅용). 검사 시각, 앱 버전, 권한 상태, 프로브별 소요 시간 포함

## 5. 1차 검사 항목

| 분류 | 검사 | 수집 방법 | 판정 |
|---|---|---|---|
| 디스플레이 | 활성 모니터별 현재 모드 vs 같은 해상도에서 GPU가 구동 가능한 최대 Hz | `EnumDisplayDevicesW` + `EnumDisplaySettingsW`(현재 -1, 모드 열거). WMI EDID 목록은 60Hz까지만 보이므로 쓰지 않음 | 더 높은 Hz 있으면 Candidate. 노트북(섀시 종류)이면 조건에 "전원 연결 시" 추가. 해상도를 낮춰야 나오는 Hz는 권장하지 않음 |
| 메모리 | 모듈별 정격(`Speed`) vs 구동(`ConfiguredClockSpeed`) | `Win32_PhysicalMemory` | 일치: Ok "정격 속도로 동작 중". 낮음: Candidate "정격보다 낮게 동작 중, BIOS에서 XMP/EXPO 확인". 프로필 이름 자체는 확인 불가로 명시 |
| GPU 드라이버 | 설치 버전·날짜. NVIDIA는 같은 계열의 최신 버전·날짜·다운로드 링크 | `Win32_VideoController`; NVIDIA 공개 조회(`lookupValueSearch` TypeID=3로 pfid/psid, `DriverManualLookup`로 Version/ReleaseDateTime/DownloadURL/DetailsURL). Game Ready와 Studio 목록을 각각 조회해 설치 버전이 속한 계열 안에서만 비교. 어느 목록에도 없으면 계열 확인 불가 | 최신보다 낮으면 Candidate "업데이트 후보 있음". AMD/Intel은 설치 정보 + `vendor-links.json`의 공식 페이지 링크, 최신 여부는 CannotVerify(Unsupported). 네트워크 실패는 CannotVerify(NetworkFailed) |
| Windows 업데이트 드라이버 | 대기 중인 드라이버 업데이트 목록 | Windows Update Agent COM(`Microsoft.Update.Session`, `IsInstalled=0 and Type='Driver'`) | 있으면 Candidate + 설정 앱 열기. 벤더 무관(칩셋·오디오·LAN 포함) |
| 노트북 OEM | 제조사·모델 → 지원 페이지 | `Win32_ComputerSystem` + `vendor-links.json` | Info. 표에 없으면 CannotVerify(NoRule) |
| 전원 | 활성 전원 계획, 섀시 종류 | `PowerGetActiveScheme` 또는 `powercfg`, `Win32_SystemEnclosure` | Info. 노트북에서 고성능 고정이면 Candidate 조건부 |
| 그래픽 설정 | HAGS(`HwSchMode`), 게임 모드(`AutoGameModeEnabled`) | 레지스트리 | Info |
| 보안 상태 | 메모리 무결성/VBS | `Win32_DeviceGuard` | Info만. 끄기 권장 없음. 보안 섹션에 배치 |
| 저장소 | 볼륨 여유, 디스크 종류·상태, TRIM, `Windows.old`, 표준 임시 위치(`%TEMP%`, Windows Temp, 배달 최적화, 썸네일, 업데이트 캐시) 크기 | `MSFT_Volume`/`MSFT_PhysicalDisk` WMI, TRIM 상태 API, 디렉터리 크기 스캔 | 측정 + 여유 부족 시 Candidate. TRIM은 관리자 필요 시 ElevationRequired |
| 앱 캐시 | winapp2 + supplement 규칙으로 설치 앱 탐지(`Detect*`) 후 `FileKey` 경로 크기 측정 | 규칙 파서 + 크기 스캔. 앱 설정에서 경로 재정의 읽기: Adobe(Media Cache 위치), npm(`cache` 설정), pip(`cache-dir`), Steam(`libraryfolders.vdf`) | 측정만. 규칙별 영향 문구(재생성 비용·재로그인·재다운로드) 표시 |
| 시작 프로그램 | 항목별 사용/비활성, 부팅 영향 | `Run` 키 + `StartupApproved` + 작업 관리자 영향 지표 | 개수로 판단하지 않음. 사용 중이고 영향 높음만 Info로 노출 |
| 미분류 대용량 폴더 | 사용자 프로필·ProgramData 아래 규칙에 안 걸린 상위 N개 | 크기 스캔 + 규칙 매칭 제외 | CannotVerify(NoRule). 크기·확장자 분포·최근 수정만 표시. 2차 LLM 슬롯 |

supplement.ini 1차 항목: Adobe Media Cache Files, npm cache, pip cache, NuGet `~/.nuget/packages`(winapp2는 `%LocalAppData%\NuGet\*Cache`만 다룸), Steam 셰이더 캐시, NVIDIA DXCache/GLCache, Squirrel 계열 앱 구버전(`app-*` 잔여) 탐지.

`protect.json` 보호 경로: 사용자 문서·사진·바탕화면, `Program Files*` 실행 파일, `Windows\System32`, `Windows\Installer`, `WinSxS`, 클라우드 동기화 폴더(OneDrive 등). 1차는 표시에만 쓰이나 2차 실행기의 강제 규칙이 된다.

## 6. 권한 모델

- 앱은 일반 권한으로 시작한다
- `RequiresElevation` 프로브는 권한이 없으면 CannotVerify(ElevationRequired)
- 상단 "관리자 권한으로 다시 검사" 버튼은 앱을 `runas`로 재실행한다. 이때도 조회만 한다
- 2차 실행기는 별도 exe. 작업 계획 JSON을 받아 실행하고 결과를 반환하며, 앱은 해당 프로브를 재실행해 상태 변화를 검증한다. 격리 용량(복원 가능)과 실제 확보 용량은 구분해 표시한다

## 7. 화면

창 하나. 왼쪽 분류 목록과 건수, 가운데 카드 목록, 상단 검사 시작 버튼과 요약(정상 n, 후보 n, 확인 불가 n, 정보 n).

카드: 한 문장 제목 → 측정값 → 근거 → 판정 배지 → 권장(조건 포함) → 영향 → 버튼 [자세히] [설정 열기] [유지]. `적용`은 비활성 표시.

문구 톤 예시:
- "모니터 3에서 더 높은 주사율을 쓸 수 있어요 / 현재 120Hz · 같은 해상도에서 130Hz 선택 가능 / 스크롤과 마우스 이동이 부드러워짐"
- "그래픽 드라이버 업데이트 후보가 있어요 / 설치 616.56(8월 20일) · 같은 계열 최신 617.14(9월 22일) / 공식 다운로드 링크로 이동"

한국어 문자열은 리소스 파일로 분리한다.

## 8. 오류 처리와 로깅

- 프로브 실패·타임아웃·네트워크 실패는 모두 CannotVerify로 흡수. 검사는 중단되지 않는다
- 로그: `%LocalAppData%\PcOptimizer\logs\`, 일 단위 롤링. 프로젝트 공용 로거 래퍼 하나로 통일하고 `Console.WriteLine` 직접 사용 금지
- 리포트 JSON에 프로브별 성공/실패/소요 시간 기록

## 9. 테스트

- 규칙: 가짜 측정값 → 기대 Finding. 정상/후보/확인 불가 경계 모두
- 파서: winapp2 샘플 항목, NVIDIA 응답 샘플(저장본), `libraryfolders.vdf` 샘플
- 프로브: 실제 PC 스모크 테스트를 `Category=Smoke` 트레이트로 분리
- 검증 기준 PC: 이 PC(RTX 4080 SUPER, DDR5 6000, 모니터 3대)에서 알려진 결과 재현

## 10. 코딩 규칙

- CLAUDE.md에 C# 항목이 없어 Java 규칙을 준용: 파일 헤더는 Java 형식 주석 블록, 상수 UPPER_SNAKE_CASE, 클래스 PascalCase. 메서드는 C# 관례인 PascalCase
- 매직 넘버 금지. 환경별로 달라지는 값(타임아웃, 로그 경로 등)만 설정 파일로 분리
- 모든 공개 클래스·메서드에 한글 XML 문서 주석

## 11. 조사 근거 요약

- 2026-09-26 이 PC에서 조회 전용 프로브로 검증: 메모리 6000/6000, 모니터 3은 120Hz 구동·130Hz 가능, NVIDIA 설치 616.56 vs 최신 617.14(다운로드 직링크 포함), HAGS·게임 모드·TRIM 켜짐, VBS 실행 중, 시작 항목 28개
- 발견한 함정: WMI EDID 모드는 60Hz까지만 보임 → Win32 API 필수. PowerShell은 `$null`을 빈 문자열로 넘김(C#에서는 해당 없음). 콘솔 한글 인코딩(C#에서는 해당 없음)
- winapp2.ini: 4,068 섹션, CC-BY-SA 4.0. NuGet 항목 존재하나 `~/.nuget/packages` 미포함. npm·Maven·Docker·Adobe Media Cache·Unreal DDC 없음
- 참고 프로젝트: FPS Doctor(검사 항목, Apache-2.0), c-cleaner(3단계 판정·격리 설계, MIT), TinyNvidiaUpdateChecker(NVIDIA 조회 방식), PC-AI(로컬 LLM 슬롯 참고)
