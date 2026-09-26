# PC Optimizer 설계 문서

- 작성일: 2026-09-26
- 상태: 1차(조회 전용 진단 + 결과 화면) 검토 수정본, 구현 계획 작성 가능
- 스택: C# / .NET 10 LTS / WPF (1차 배포: Windows 11 x64)
- 검토일: 2026-09-26. .NET 8은 2026-11-10 지원 종료 예정이므로 신규 개발 기준을 .NET 10으로 변경한다. SDK와 NuGet 버전은 구현 시작 시 호환성을 확인해 고정한다.

## 1. 목표와 범위

컴퓨터를 잘 모르는 사용자가 자기 PC의 상태를 한 화면에서 이해하고, 무엇을 바꾸면 좋은지와 왜 그런지를 납득한 뒤 스스로(또는 2차부터는 자동으로) 조치할 수 있게 하는 Windows 11용 도구.

차별화 가설: "사용자 상황에 맞는 판단과 조치 연결이 부족하다". 정리(winapp2/BleachBit/PC Manager), 설정 감사(FPS Doctor), 트윅 적용(WinUtil/Winhance), 드라이버(TinyNvidiaUpdateChecker), LLM 폴더 판정(c-cleaner)을 참고한다. 기존 제품이 이러한 통합 경험을 전혀 제공하지 않는다고 단정하지 않으며, 사용성 검증으로 가설을 평가한다.

### 1차 범위 (이 문서)

- 조회 전용 진단 엔진과 결과 화면
- 측정 사실 / 권장 판단 / 확인 불가를 구분해 표시
- 자동 조치 없음. `적용` 버튼은 모델에만 존재하고 UI에서 비활성
- LLM 없음. 인터페이스만 예약
- 조회 전용은 대상 파일 삭제·이동, 레지스트리 변경, 드라이버 다운로드·설치, 업데이트 설치를 하지 않는다는 뜻이다. 앱 자체 로그·설정·사용자가 내보낸 리포트 저장은 허용한다. 네트워크 조회와 관리자 재검사는 각각 명시적 사용자 동작으로 시작한다

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
| `PcOptimizer.Core` | 클래스 라이브러리 (net10.0) | 데이터 모델, `IProbe`/`IRule` 인터페이스, 규칙 엔진, winapp2 형식 파서, 보호 정책 모델, 리포트 모델. Windows API·실제 파일 시스템·네트워크 I/O 의존 없음 |
| `PcOptimizer.Probes` | 클래스 라이브러리 (net10.0-windows) | WMI, 레지스트리, Win32 P/Invoke, 파일 스캔·앱 설정 해석, NVIDIA 조회, Windows Update Agent 등 수집기 구현 |
| `PcOptimizer.App` | WPF (net10.0-windows) | 결과 화면과 실행 조율. MVVM(CommunityToolkit.Mvvm), Fluent 스타일(WPF-UI). 테마 라이브러리에 진단 모델을 종속시키지 않음 |
| `PcOptimizer.Tests` | xUnit (net10.0-windows) | Core 단위 테스트, Windows 어댑터 계약 테스트, 실제 PC 스모크 테스트(별도 트레이트). Windows 대상인 이유는 Probes 참조 때문이며 Core 자체는 Windows 의존 없음 |
| `rules/` | 데이터 | `winapp2.ini`(사용한 스냅샷·출처·라이선스 표기), `supplement.ini`(같은 형식, 프로젝트 정책으로 CC-BY-SA 4.0), `rule-metadata.json`(검토한 규칙별 영향·출처), `protect.json`(보호 경로), `vendor-links.json`(공식 드라이버·OEM 링크 허용 표), `sources.json`(규칙 버전·커밋·해시·라이선스) |

의존 방향: `App → Probes → Core`, `App → Core`. Probes는 App을 모른다. Core는 어느 쪽도 모른다.

예약 인터페이스(Core에 정의, 1차 구현 없음):
- `IElevatedExecutor`: 작업 계획을 받아 실행하고 결과를 반환
- `IUnknownFolderAdvisor`: 미분류 폴더 요약을 받아 소유 앱 추정과 조사 방향을 반환

둘 다 1차에 호출·구현·별도 프로젝트 생성하지 않는다. LLM은 수집→판정의 필수 경유 단계가 아니다. `IElevatedExecutor`의 계획은 명시적인 작업 종류와 검증된 매개변수 모델로 정의하며 임의 명령 문자열 실행을 계약에 포함하지 않는다.

## 3. 데이터 모델

모든 검사 결과는 `Finding` 하나로 통일한다.

```
Finding
  Id            : string          규칙 ID + 장치/경로의 내부 식별자. DISPLAY3 같은 표시 순번을 영속 ID로 쓰지 않음
  Category      : enum            Display, Memory, Driver, Power, Graphics, Security, Storage, AppCache, Startup, Unclassified
  Title         : string          한 문장. 예) "모니터 3에서 더 높은 주사율 후보를 찾았어요"
  Measured      : Measurement[]   { Name, Value, Unit?, Source, ObservedAtUtc, Quality }
                                  Value는 숫자/불리언/문자열/목록 구분. Quality = Observed | Reported | Estimated | Partial
  Evidence      : string          판단 근거 한 줄
  Verdict       : enum            Ok(정상 확인) | Candidate(개선 후보) | CannotVerify(확인 불가) | Info(정보)
  CannotVerifyReason : enum?      ElevationRequired | NetworkFailed | NoRule | Unsupported | ProbeError | Timeout | Cancelled | AccessDenied | PartialData | Ambiguous | NotRequested
  Detail        : string?        사용자용 사유. 원시 예외·전체 개인 경로를 기본 화면에 노출하지 않음
  Recommendation: { Text, Condition }?   예) "설정에서 130Hz 후보 확인", "동일 해상도·HDR/색상 조건 확인 후 선택"
  Impact        : { Benefit, SideEffect }?  예) "스크롤이 부드러워짐", "일부 캐시는 다음 실행 때 다시 생성"
  Actions       : Action[]        ShowDetails | OpenSettings(uri) | Keep | Apply(2차, UI 비활성) | OpenLink(url)
```

`Verdict`에 "문제" 등급은 없다. 측정값과 권장의 분리가 이 모델의 목적이다.

`CannotVerifyReason`은 CannotVerify일 때 필수, 그 외에는 null이다. Candidate에는 Recommendation과 근거가 필수다. 부재·0·false·접근 실패를 구분한다. 일부 측정만 성공하면 성공한 사실은 Info로 보존하고, 불가능한 비교는 별도 CannotVerify로 표시한다. 조회 실패를 빈 목록이나 Ok로 변환하지 않는다.

수집 반환형은 `ProbeResult { ProbeId, Status, Measurements, Issues, StartedAtUtc, Duration, UserContext }`이다. `Status = Success | Partial | Failed | Skipped | Cancelled`, Issues는 사유 코드와 요약을 가진다. 프로브는 Finding/Verdict를 만들지 않고 규칙 엔진이 이를 변환한다. 장치·경로 내부 ID는 내보내기 시 익명화한다.

## 4. 엔진 흐름

수집과 판정을 분리한다.

1. **Collect**: 각 `IProbe { Id, RequiresElevation, RequiresNetwork, RunAsync(context, ct) }`를 제한된 병렬도로 실행한다. 기본 동시 실행 4개, 디스크 순회는 볼륨당 1개로 제한하고 앱 캐시·저장소·미분류는 공유 스캔 결과를 재사용한다. 프로브별 타임아웃·취소를 지원하며, 오류를 ProbeResult에 담아 다른 검사에 전파하지 않는다. 권한 요구가 다른 수집은 프로브를 나눠 가능한 부분까지 반환한다
2. **Evaluate**: `IRule`은 측정값과 프로브별 상태/Issues를 담은 불변 스냅샷을 입력받아 `Finding[]`을 내는 순수 함수다. I/O는 하지 않으며 가짜 스냅샷으로 단위 테스트한다. 상태가 있어야 미실행·실패·빈 성공 결과를 구분할 수 있다
3. **Report**: 화면용 모델 + JSON 내보내기(문의·디버깅용). 스키마 버전, 검사 ID·시각, 앱/규칙 버전, 권한·사용자 컨텍스트, 프로브별 상태·소요 시간·스캔 완전성 포함

기본 타임아웃은 로컬 정보 15초, NVIDIA 요청 30초, WUA 검색 120초, 파일 순회 120초/볼륨이다. 환경 설정으로 조절 가능하며 초과하면 부분 결과와 Timeout을 반환한다. 타임아웃된 동기 WMI/COM 호출을 `Task.WhenAny`로 숨기는 것만으로 취소 완료라고 간주하지 않는다. WUA는 비동기 검색/취소 API, WMI는 제공자 타임아웃을 사용한다. 취소 불가능한 호출은 한 번만 격리 실행하고 종료 전 중복 실행을 금지한다. UI는 해당 작업이 아직 종료 중임을 표시한다. 새 검사 ID와 다른 늦은 결과는 버린다.

사용자 취소 시 새 작업 예약을 중단하고 기존 결과를 '부분 검사'로 남긴다. 로컬 검사는 기본 제공하고 NVIDIA·WUA는 `온라인 업데이트 확인` 동작으로 실행한다. 네트워크 없는 기본 검사에서 해당 최신 비교는 NotRequested이며 로컬 설치 정보는 유지한다.

## 5. 1차 검사 항목

| 분류 | 검사 | 수집 방법 | 판정 |
|---|---|---|---|
| 디스플레이 | 활성 모니터별 현재 모드와 같은 해상도의 드라이버 보고 모드 비교 | `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)`로 활성 대상·현재 신호/가상 주사율을 구분하고, `EnumDisplayDevicesW` + `EnumDisplaySettingsW`로 모드 열거. EDID/WMI는 보조 정보이며 '항상 60Hz까지만 나온다'고 가정하지 않음 | 같은 해상도·방향·픽셀 형식·주사 방식에서 의미 있게 높은 모드(차이 1Hz 초과)가 있으면 Candidate '더 높은 주사율 후보'. 59.94/60은 개선으로 분류하지 않음. 복제·원격·가상 화면·DRR로 비교가 불명확하면 CannotVerify. 연결 전원 상태를 함께 표시. HDR/색심도 등 모든 조건과 안정성은 검증한 것이 아니므로 '구동 가능 확정' 문구 금지 |
| 메모리 | 모듈별 SMBIOS 보고 속도(`Speed`)와 설정 속도(`ConfiguredClockSpeed`) | `Win32_PhysicalMemory`; 원시 값과 제공자 단위를 보존 | 유효하고 비교 가능한 설정 속도 < 보고 속도: Candidate '메모리 속도 설정 확인 가능'. 두 속도 일치: Info '보고 속도와 설정 속도 일치'. 누락·0·단위 불명확: CannotVerify. 후보 설명에 "CPU·메인보드·메모리 구성에 따른 정상 제한일 수 있으며, XMP/EXPO 활성 여부는 확인되지 않았습니다"를 반드시 붙임. `Speed`를 광고상 정격이나 이 PC에서 보장되는 속도로 표현하지 않고, '정격 미달'·'XMP 꺼짐'으로 단정하지 않음. 무조건적인 XMP 활성화 권고 없음. XMP/EXPO 활성 여부 자체는 별도 CannotVerify(Unsupported) |
| GPU 드라이버 | 어댑터별 설치 버전·날짜·하드웨어 ID, NVIDIA의 조건에 맞는 최신 후보 | `Win32_VideoController`와 PnP 장치 정보로 연결. NVIDIA 공개 웹 조회는 보장된 SDK가 아닌 변경 가능한 어댑터로 취급. pfid/psid는 정확한 GPU/OS 매핑을 검증하고, 응답 버전·날짜·제품·URL을 검증 | Game Ready/Studio 양쪽에 같은 버전이 있거나 어느 쪽에도 없으면 계열은 Ambiguous. 사용자가 계열을 선택하거나 설치 계열을 독립적으로 확인했을 때만 동일 계열 비교. 버전 문자열은 수치로 비교하고 Windows 드라이버 버전 변환도 테스트. 최신 후보가 있어도 '이 기기 권장/설치 호환성 검증 완료'로 표현하지 않음. AMD/Intel은 설치 정보 Info + 공식 링크, 최신 비교 Unsupported. 실패 시 설치 정보 보존 |
| Windows 업데이트 드라이버 | 구성된 업데이트 서비스에서 검색된 미설치·비숨김 드라이버 | Windows Update Agent COM의 비동기 검색(`IsInstalled=0 and IsHidden=0 and Type='Driver'`), 취소 지원. 기존 관리 정책·업데이트 서비스를 변경하지 않음 | 있으면 Candidate 'Windows Update에서 제공되는 드라이버 후보' + 설정 앱 열기. 모든 제조사 최신 드라이버를 포괄한다고 주장하지 않음. 성공 결과 0건은 Info '해당 서비스에서 후보 없음'. 정책 차단·부분 검색·재부팅 관련 상태는 별도 사유 표시 |
| 노트북 OEM | 제조사·모델 → 지원 페이지 | `Win32_ComputerSystem` + `vendor-links.json` | Info. 표에 없으면 CannotVerify(NoRule) |
| 전원 | 활성 전원 계획, 섀시 종류, 실제 AC/배터리 상태 | `PowerGetActiveScheme`, `Win32_SystemEnclosure`, `GetSystemPowerStatus` | Info. 고성능을 정상/최적으로 단정하지 않음. 섀시 불명은 Unknown이며 데스크톱으로 추정하지 않음. 배터리 동작 중 고성능 사용에만 소비전력 관련 조건부 Candidate. 배터리 없는 UPS를 노트북으로 단정하지 않음 |
| 그래픽 설정 | HAGS(`HwSchMode`), 게임 모드(`AutoGameModeEnabled`)의 설정값 | 레지스트리의 존재·형식·값 구분 | 알려진 값은 Info '설정값'. 키 부재는 꺼짐이 아닌 CannotVerify(Unsupported). 설정값만으로 현재 지원·실행 중·재부팅 적용 완료를 확정하지 않음 |
| 보안 상태 | 메모리 무결성/VBS의 설정 상태와 실행 상태 | `Win32_DeviceGuard`의 관련 설정/실행 필드 | 확인한 상태는 Info만, 조회 실패/미지원은 CannotVerify. 메모리 무결성과 VBS를 동일한 값으로 취급하지 않음. 끄기 권장 없음. 보안 섹션에 배치 |
| 저장소 | 볼륨 여유, 제공자 보고 디스크 종류·상태, TRIM 정책, `Windows.old` 및 표준 임시 위치의 관측 크기 | `MSFT_Volume`/`MSFT_PhysicalDisk`와 볼륨별 조회, `fsutil behavior query DisableDeleteNotify`의 읽기 전용 호출, 공유 디렉터리 스캔 | 여유율 10% 미만 또는 여유 10GiB 미만이면 Candidate '여유 공간 적음'(제품 휴리스틱임을 명시). Healthy는 'Windows가 보고한 상태'이며 SMART 전체 정상·고장 없음으로 확장하지 않음. TRIM은 OS 삭제 알림 정책이며 실제 장치 지원/수행 여부와 구분. 폴더 크기는 삭제 가능량이 아님 |
| 앱 캐시 | 지원하는 winapp2 + supplement 규칙과 설정 경로로 후보 위치 탐지 | 검토한 규칙·버전별 앱 설정 리더 + 공유 스캔. Adobe Media Cache 설정, npm 캐시 설정, pip cache-dir 설정, Steam 라이브러리/shadercache 경로를 지원 | Info '캐시/잔여 파일 후보의 관측 크기'. 규칙별 검증된 영향 표시, 미검토 규칙은 영향 미확인. 실행 중인 앱의 미사용 캐시로 확정하지 않음. 사용자 설정을 해석하지 못하면 기본 위치만 확인했다는 범위를 표시 |
| 시작 프로그램 | Run/RunOnce 키와 시작프로그램 폴더의 등록 항목, 매칭 가능한 활성 상태 | HKCU/HKLM의 관련 32/64비트 뷰, Known Folder 시작프로그램 폴더. StartupApproved는 비공식 상태 포맷이므로 알려진 값과 정확한 항목 연결만 해석 | 발견한 항목은 활성/비활성/알 수 없음과 함께 Info. 1차 부팅 영향은 Unsupported로 표시하며 작업 관리자 비공개 지표를 긁거나 개수로 추정하지 않음. 예약 작업·서비스·모든 패키지 앱 자동 실행을 포괄하지 않는다고 명시 |
| 미분류 대용량 폴더 | 사용자 프로필·ProgramData 아래 규칙에 안 걸린 상위 20개, 1GB(1,000,000,000바이트) 이상 | 5.2의 보호·부분 집계·비중복 정책으로 공유 스캔 결과에서 산출 | CannotVerify(NoRule). 관측 크기·확장자 분포·관측 파일의 최근 수정 시각 표시. 수정 시각은 마지막 사용 시각이 아님. 조회 실패/보호 제외를 미분류로 오인하지 않음. 2차 LLM 슬롯 |

supplement.ini 1차 항목: Adobe Media Cache Files, npm cache, pip cache, NuGet 전역 패키지 위치(기본 `%UserProfile%\.nuget\packages`), Steam 셰이더 캐시, NVIDIA DXCache/GLCache, Squirrel 계열 앱 버전 폴더. Squirrel의 `app-*`는 버전 폴더 존재만 Info로 표시하며, 이름·날짜만으로 '미사용 구버전'이나 삭제 가능으로 판정하지 않는다. NuGet 패키지도 캐시라는 이유만으로 재다운로드 가능·미사용을 보장하지 않는다.

### 5.1 규칙과 설정 경로 계약

- winapp2 전체를 완벽하게 해석한다고 가정하지 않는다. 1차 지원은 섹션, `Detect`/`DetectFile` 번호 변형(대안 OR), `FileKeyN`의 경로·세미콜론 패턴·`RECURSE`/`REMOVESELF`, `ExcludeKeyN`의 FILE/PATH 제외이다. 삭제 의미의 플래그는 조회 범위에만 변환한다. `RegKey`는 크기 계산/실행 대상이 아니며 효과를 지원하지 않는다고 기록한다.
- 알 수 없는 탐지·제외·운영체제 제약 문법, 해석 실패, 해결되지 않은 환경 변수는 해당 규칙을 Unsupported로 건너뛴다. 특히 제외 조건을 무시한 넓은 탐색은 금지한다. 지원한 규칙/건너뛴 규칙 수와 원본 버전을 리포트에 기록한다. 안전한 탐지 근거가 없는 항목은 모든 사용자에게 적용하지 않는다.
- Core는 파싱/계획만 하고 레지스트리·파일 탐지는 Probes가 수행한다. 경로는 Windows에서 정규화한 후 대소문자 무시·디렉터리 경계 단위로 비교한다. 볼륨 루트 전체·UNC·장치 경로로 확장되는 규칙은 1차 Unsupported다.
- 보호 정책 > 규칙의 제외 경로 > 검토된 supplement/앱 설정 경로 > 기본 커뮤니티 경로 순으로 평가한다. 겹치는 파일은 합계에서 한 번만 세고 출처 규칙 목록을 보존한다. 정확한 일치 파일만 분류하며 일부가 매칭됐다고 상위 폴더 전체를 캐시로 분류하지 않는다.
- 앱 설정 리더는 읽을 파일/키·지원 앱 버전·우선순위를 fixture와 함께 명시한다. Adobe의 검증되지 않은 바이너리 환경설정은 추측하지 않는다. npm/pip/NuGet의 환경 변수·사용자/전역 설정 중 검증한 것만 읽고, 프로젝트별 설정·런타임 인수는 관측 범위 밖으로 표시한다. NuGet은 `NUGET_PACKAGES`/설정의 globalPackagesFolder도 고려한다. 설정 파일은 필요한 키만 읽고 인증 토큰 등을 로그/리포트에 남기지 않는다.
- 임의 설치 앱·사용자 스크립트를 실행해 설정을 알아내지 않는다. 설정된 경로가 UNC/보호/오프라인 위치이면 상태를 표시하고 순회하지 않는다. Steam 라이브러리 경로 전체가 아니라 그 아래 확인된 `steamapps/shadercache`만 캐시 후보로 다룬다.
- 앱은 포함된 고정 규칙 스냅샷을 사용하며 1차 자동 규칙 다운로드는 없다. 원본 라이선스/저작자 표시·변경 사항·커밋/해시를 함께 보관한다. 같은 INI 형식을 쓴다는 사실만으로 라이선스 의무를 추론하지 않는다.

### 5.2 파일 스캔·보호·집계 계약

- `protect.json` 기본 경로는 Known Folders의 문서·사진·바탕화면(리디렉션 포함), 감지된 클라우드 동기화 루트, Program Files, System32, Installer, WinSxS다. 보호 경로의 내용 순회는 1차에서도 하지 않고 제외 사유만 표시한다. 정책 파일이 잘못되면 파일 순회를 중단하고 CannotVerify를 반환한다. 다른 하드웨어 검사는 계속한다.
- 사용자 프로필과 ProgramData를 기본 루트로 하고 TEMP·Windows 임시 위치·검증된 앱 설정 경로만 추가한다. 시스템 위치는 지정한 임시 하위 경로만 조회한다. 다른 사용자 프로필을 자동 탐색하지 않는다. 정션·심볼릭 링크 등 reparse point는 따라가지 않으며 클라우드 placeholder를 읽어 다운로드시키지 않는다. 파일 본문은 읽지 않는다(정해진 작은 앱 설정 파일은 예외).
- 파일 크기·확장자·수정 시각과 가능한 파일 ID/할당 크기만 수집한다. 중첩 루트·규칙·하드링크는 가능한 경우 볼륨 ID+파일 ID로 중복 제거한다. 파일 ID를 못 얻으면 논리 크기 추정/중복 가능이라고 표시하며 정확한 물리 점유량이라고 표현하지 않는다. 압축·희소 파일은 논리 크기와 관측 가능한 할당 크기를 구분한다. 합계는 '확보 가능량'으로 이름 붙이지 않는다.
- 접근 거부·삭제 중 파일·타임아웃은 누락 건수와 부분 집계를 남긴다. 읽지 못한 위치를 0바이트/빈 폴더/정상으로 취급하지 않는다. 경로 보호 확인은 루트 등록 시와 각 하위 항목 처리 시 적용한다.
- 미분류 순위에는 완전하게 관측된 폴더의 미분류 파일 논리 크기만 사용한다. 알려진 파일은 빼고, 임계값 이상인 자식 후보가 있으면 상위 후보를 먼저 버리는 bottom-up 방식으로 부모/자식 중복을 없앤 뒤 크기 내림차순·경로 오름차순으로 상위 20개를 선택한다. 탈락한 상위 폴더의 잔여 파일이 목록에서 생략될 수 있음을 명시한다. 부분 스캔 폴더는 별도 CannotVerify(PartialData)로 표시하며 순위 밖에 둔다. 보호 경로는 순위에 포함하지 않는다.

## 6. 권한 모델

- 앱은 일반 권한으로 시작한다
- `RequiresElevation` 프로브는 권한이 없으면 CannotVerify(ElevationRequired)
- 상단 "관리자 권한으로 다시 검사" 버튼은 앱을 `runas`로 재실행한다. 이때도 조회만 한다. UAC 취소 시 기존 결과/창을 유지한다. 성공 시 별도 검사 ID로 수집하고 기존 일반 권한 결과와 섞지 않는다. 1차에서 앱 전체 승격은 진단용 예외이며 2차 조치는 별도 실행기로 분리한다
- 표준 사용자가 다른 관리자 계정을 입력할 수 있으므로 원래 SID와 프로필 컨텍스트를 넘기고 승격 후 비교한다. SID가 바뀌면 관리자 창은 시스템 범위만 검사하고 사용자별 레지스트리·앱 설정·캐시·시작 항목은 원래 일반 권한 창에서 확인하도록 안내한다. 관리자 계정의 HKCU/프로필을 원래 사용자 결과로 표시하지 않는다. 전달 인자는 고정된 재검사 옵션과 컨텍스트 식별자만 허용한다
- 1차 배포는 사용자 쓰기 가능한 규칙 파일을 관리자 권한으로 신뢰하지 않는다. 관리자 검사에서는 무결성이 확인된 포함 규칙/보호 정책만 사용하고 사용자 추가 경로는 승격된 스캔에 적용하지 않는다
- 2차 실행기는 별도 exe. 작업 계획 JSON을 받아 실행하고 결과를 반환하며, 앱은 해당 프로브를 재실행해 상태 변화를 검증한다. 격리 용량(복원 가능)과 실제 확보 용량은 구분해 표시한다

## 7. 화면

창 하나. 왼쪽 분류 목록과 건수, 가운데 카드 목록, 상단 검사 시작/취소 버튼과 요약(정상 n, 후보 n, 확인 불가 n, 정보 n). 검사 중/부분 완료/취소/완료를 구분하고, 건수는 Finding 기준임을 명시한다. 후보 0건이 전체 PC 정상 인증이라는 인상을 주지 않는다. 온라인 검사 여부와 마지막 측정 시각을 표시한다.

카드: 한 문장 제목 → 측정값 → 근거 → 판정 배지 → 권장(조건 포함) → 영향 → 가능한 버튼만 표시한다. [유지]는 현재 검사에서 권고를 접는 UI 동작이며 설정 변경·영구 검사 제외가 아니다. [설정 열기]는 실제 지원 URI가 있을 때만 제공하고 없으면 수동 경로 안내로 대체한다. `적용`은 비활성이며 '자동 조치는 다음 버전 예정' 설명을 제공한다.

`OpenLink`는 vendor-links의 HTTPS 공식 호스트·경로 허용 목록으로 검증하고 NVIDIA 응답/리디렉션에도 적용한다. `OpenSettings`는 코드에 정의한 설정 URI만 허용한다. 규칙이나 응답이 지정한 임의 실행 파일·명령·file URI는 실행하지 않는다.

문구 톤 예시:
- "모니터 3에서 더 높은 주사율 후보를 찾았어요 / 현재 120Hz · 같은 해상도로 보고된 후보 130Hz / 설정에서 HDR·색상 조건을 확인하세요"
- "그래픽 드라이버 업데이트 후보가 있어요 / 설치 버전과 선택한 계열의 최신 후보를 비교했어요 / 공식 배포 설명으로 이동"

예시 숫자는 테스트용이며 현재 PC 상태로 고정하지 않는다.

한국어 문자열은 리소스 파일로 분리한다.

## 8. 오류 처리와 로깅

- 프로브 실패·타임아웃·네트워크 실패는 모두 CannotVerify로 흡수. 검사는 중단되지 않는다
- 로그: `%LocalAppData%\PcOptimizer\logs\`, 일 단위 롤링. 프로젝트 공용 로거 래퍼 하나로 통일하고 `Console.WriteLine` 직접 사용 금지
- 리포트 JSON에 프로브별 성공/실패/소요 시간 기록
- 기본 로그 보관은 7일, 최대 20MiB이며 앱 로그만 순환한다. 원시 WMI 덤프·파일 목록·환경 변수·설정 파일 내용을 통째로 기록하지 않는다
- JSON 기본 내보내기는 사용자명·프로필/개인 경로·PC명·SID·장치 일련번호를 제거하거나 검사 단위 익명 ID로 바꾼다. 폴더 상세 이름/전체 경로를 포함하는 진단용 내보내기는 별도 선택으로 제공하고 저장 전 안내한다. 경로 해시만으로 익명화됐다고 간주하지 않는다. 자동 업로드는 없다
- 개인 정보가 들어갈 수 있는 예외 메시지는 요약 코드로 치환하고 원문은 기본 로그에 남기지 않는다

## 9. 테스트

- 규칙: 가짜 측정값 → 기대 Finding. 정상/후보/확인 불가 경계, null/0/false, 부분 결과와 미실행, 메모리는 설정 속도 < 보고 속도에서만 조건부 Candidate가 나오고, 일치는 Info, 누락·0은 CannotVerify이며, 어떤 경우에도 '정격 미달'·'XMP 꺼짐' 단정이나 무조건적 XMP 활성화 권고 문구가 없는지 검증
- 디스플레이: 59.94/60, 해상도 변경 필요 모드, 인터레이스, DRR, 복제, 원격 세션, 다중 모니터·핫플러그를 fixture로 검증
- 드라이버: 두 계열에 같은 버전, 목록에 없는 설치 버전, 정확한 장치 매핑 실패, HTML 오류 응답·스키마 변경, 네트워크 끊김, 수치 버전 비교, 허용되지 않은 URL 검증
- 파일/파서: 알 수 없는 제외 문법, 보호 경로 아래 사용자 설정 경로, 중첩 루트·하드링크·정션, 접근 거부, 취소, 설정 파일 내 비밀 값, Steam 게임 본체 제외, Squirrel '미사용' 오판 금지를 테스트. 합계와 상위 20개 부모/자식 중복 제거도 검증
- 엔진/UI: 프로브 한 개 실패 후 다른 결과 유지, 타임아웃 뒤 늦은 결과 무시, 취소 후 재검사 중복 방지, UAC 취소·다른 SID 컨텍스트 처리, 기본 내보내기 개인정보 제거
- 프로브: 실제 PC 스모크 테스트를 `Category=Smoke` 트레이트로 분리하고 기본 단위 테스트에서는 실행하지 않는다. 일반 권한 검사와 사용자가 실행한 관리자 검사를 구분한다. 온라인 테스트는 별도 명시적 옵션으로만 실행한다
- 이 PC의 새 측정 결과를 Windows 설정/장치 관리자 등 독립된 화면과 비교한다. 제공된 RTX 4080 SUPER·DDR5 6000·모니터 3대와 버전/Hz 값은 검증 후보이며 기대값으로 하드코딩하지 않는다. 원본 프로브·원시 출력이 없는 과거 수치를 재현 증거로 사용하지 않는다
- 12개 분류 모두 성공 경로와 실패/지원 불가 경로를 구현한다. 접근이 막힌 항목도 사유가 보여야 하며, 모든 항목을 Unsupported로만 처리한 구현은 완료로 인정하지 않는다. 부팅 영향 등 본 문서에서 명시적으로 제외한 세부 기능은 예외다

## 10. 코딩 규칙

- 프로젝트 규칙으로 C# 파일 헤더는 `/** @file / @author: rudals252 / @brief */` 형식, 상수 UPPER_SNAKE_CASE, 클래스·메서드 PascalCase를 사용한다. XAML은 XML 주석 헤더를 사용한다. 생성된 파일은 제외한다. 외부 CLAUDE.md를 읽어야만 알 수 있는 규칙으로 남기지 않는다
- 매직 넘버 금지. 환경별로 달라지는 값(타임아웃, 로그 경로 등)만 설정 파일로 분리
- 모든 공개 클래스·메서드에 한글 XML 문서 주석

## 11. 조사 근거 요약

- 전달받은 2026-09-26 탐색 결과: 메모리 6000/6000, 모니터 3은 120Hz·열거 후보 130Hz, NVIDIA 설치 616.56 vs 조회 617.14, HAGS·게임 모드·TRIM 켜짐, VBS 실행 중, 시작 항목 28개. 저장소에 원본 프로브와 출력이 없어 이번 검토에서는 사실 검증하지 않았으며 이후 새 검사로 확인한다
- 특정 프로브에서 WMI 모드 누락/PowerShell 인수 변환 문제가 보고되었으나 플랫폼 전체의 제약으로 일반화하지 않는다. C#에서도 P/Invoke 시그니처·구조체 크기·유니코드·오류 코드를 검증한다
- winapp2 규칙 수·포함 범위는 스냅샷에 따라 바뀐다. 검토 시 원본에 NuGet LocalAppData 캐시 항목이 있는 것을 확인했다. 다른 앱의 '지원 없음'은 단순 키워드 검색만으로 확정하지 않고 고정한 스냅샷의 경로·실제 규칙 의미로 확인한다
- 참고 프로젝트: FPS Doctor(검사 항목, Apache-2.0), c-cleaner(3단계 판정·격리 설계, MIT), TinyNvidiaUpdateChecker(NVIDIA 조회 방식), PC-AI(로컬 LLM 슬롯 참고)

### 검토 근거

- [.NET 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy): .NET 8은 2026-11-10 종료, .NET 10 LTS는 2028-11-14까지 지원
- [메모리 WMI 속성](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-physicalmemory): SMBIOS 보고값이며 XMP 프로필 활성 플래그가 아님
- [EnumDisplaySettings](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsw), [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig): 모드 열거·활성 경로·가상 주사율 및 원격 세션 오류 처리
- [Windows 드라이버 선택 기준](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/overview-of-the-driver-selection-process): 섀시 종류만으로 드라이버 적합성을 확정할 수 없음
- [WUA 검색 예제](https://learn.microsoft.com/en-us/windows/win32/wua_sdk/searching--downloading--and-installing-updates): 본 앱에서는 검색만 사용, 다운로드·설치 단계는 사용하지 않음
- [Winapp2 원본](https://raw.githubusercontent.com/MoscaDotTo/Winapp2/master/Non-CCleaner/Winapp2.ini), [라이선스](https://github.com/MoscaDotTo/Winapp2/blob/master/License.md): 재배포할 버전 기준 출처·조건을 함께 보관

## 12. 검토 결과 및 착수 조건

4개 프로젝트, Verdict 4종, 검사 12분류, supplement 7종, 미분류 상위 20개/1GB 이상, 조회 전용 범위는 유지했다. 수정의 목적은 진단 결과를 과장하지 않고, 읽지 못한 상태·부분 집계·권한 경계를 구현자가 임의로 해석하지 않게 하는 것이다.

2026-09-26 현재 작업 환경의 `dotnet --info`는 x64 런타임 6/8/9만 보고하며 SDK는 없다. 구현 전 .NET 10 SDK 준비와 WPF-UI/CommunityToolkit/xUnit의 복원·최소 빌드를 확인해야 한다. 본 검토에서는 SDK 설치나 제품 코드 구현을 하지 않았다. 구현 순서는 [구현 계획](../plans/2026-09-26-pc-optimizer-implementation-plan.md)으로 관리한다.

## 13. 부록: 진단 항목별 연결 도구와 조치 후 재확인 (초안, REV-004 대응)

상태: 초안. 사용자가 1차 출시 범위를 결정하기 위한 자료다. "연결"은 앱이 실행·설치·삭제를 대신하는 것이 아니라, 초보자가 그 도구에서 무엇을 하면 되는지 안내하고 다시 검사해 결과를 확인하는 것을 뜻한다. 1차는 조회 전용이므로 열 수 있는 것은 설정 URI와 공식 링크뿐이고, 표의 "2차" 열은 관리자 실행기 도입 후 가능한 자동 조치다.

| 진단 항목 | 1차 연결(안내·열기) | 2차 자동 조치(실행기) | 조치 후 재확인 |
|---|---|---|---|
| 디스플레이 주사율 후보 | `ms-settings:display` → 고급 디스플레이에서 선택 안내 | `ChangeDisplaySettingsEx`로 같은 해상도 Hz 변경 + 15초 되돌리기 확인 | 디스플레이 프로브 재실행: 현재 Hz == 선택 Hz |
| 메모리 속도 설정 확인 | BIOS/UEFI 메모리 프로필 확인 안내(메인보드 제조사 링크 표) | 없음(펌웨어 설정은 자동화하지 않음) | 재부팅 후 메모리 프로브: 설정 속도 == 보고 속도 |
| GPU 드라이버 업데이트 후보 | NVIDIA 공식 다운로드·상세 페이지 링크(P6), AMD·Intel 공식 페이지·자동 감지 도구 링크 | 없음(드라이버 설치는 자동화하지 않음) | 설치 정보 프로브: 버전·날짜 갱신, 같은 계열 최신과 재비교 |
| Windows 업데이트 대기 드라이버 | `ms-settings:windowsupdate-optionalupdates` | 없음(설치는 Windows Update에 맡김) | WUA 검색 프로브: 후보 0건 |
| 전원 계획 | `ms-settings:powersleep` | `powercfg /setactive` (이전 계획 저장·복원) | 전원 프로브: 활성 계획 GUID |
| 그래픽 설정(HAGS·게임 모드) | `ms-settings:display-advancedgraphics`, `ms-settings:gaming-gamemode` | 레지스트리 값 변경(이전 값 저장) — HAGS는 재부팅 필요 | 설정 프로브: 값 확인, 재부팅 필요 표시 |
| 보안 상태(메모리 무결성) | 정보만. `windowsdefender://coreisolation` 수동 경로 안내 | 없음(보안 기능은 변경하지 않음) | 해당 없음 |
| 저장소 여유·디스크 상태 | `ms-settings:storagesense`, 디스크 상태는 제조사 도구 안내 | 저장소 센스 실행 요청 없음(표준 임시 위치는 아래 항목으로) | 볼륨 프로브: 여유 공간 |
| 표준 임시 위치 | `ms-settings:storagesense`, Windows 디스크 정리(`cleanmgr`) 안내 | 격리 이동 후 N일 뒤 정리(휴지통 아님), Windows 구성 요소는 DISM 경로만 | 파일 스캔 프로브: 위치별 관측 크기 감소, 격리 용량과 실제 확보 용량 구분 |
| 앱 캐시(검토 규칙 7종) | 앱 내 캐시 관리 화면 안내(Premiere 미디어 캐시 환경설정, `npm cache verify`, `pip cache purge`, Steam 다운로드 설정) | 검토 규칙만 격리 이동, 실행 중 앱은 제외 | 앱 캐시 프로브: 규칙별 관측 크기 |
| 앱 캐시(커뮤니티 규칙) | 관측·설명만. 해당 앱의 정리 기능 또는 BleachBit·PC Manager 같은 기존 도구 안내 | 없음(1·2차 모두 자동 삭제 대상 아님) | 해당 없음 |
| 시작 프로그램 | `ms-settings:startupapps` | 없음(사용자가 설정 앱에서 끔) | 시작 프로그램 프로브: 활성 상태 |
| 미분류 대용량 폴더 | 폴더 열기(탐색기)와 소유 앱 추정 설명(2차 LLM 슬롯) | 없음 | 파일 스캔 프로브 |

결정이 필요한 것: (1) 1차 출시에 이 표의 "1차 연결" 열까지 포함할지, (2) 2차 자동 조치의 대상을 검토 규칙 7종·전원 계획·주사율로 한정할지, (3) 커뮤니티 규칙 관측 결과를 기존 도구(BleachBit 등) 안내와 함께 보여줄지 아니면 숨길지.
