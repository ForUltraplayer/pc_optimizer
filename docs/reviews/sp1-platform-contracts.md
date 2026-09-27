# SP1 플랫폼 계약과 평가 조건

작성 2026-09-27, Task 1 구현자 조사. 문서로 확인한 API 계약과 제품의 활성화 조건을 구분한다. 이 문서는 독립 승인 또는 실기기 변경 검증 결과가 아니다. 실제 PC의 시작 항목·서비스·전원·디스플레이를 변경하지 않았다.

## B — 시작 프로그램

- [Microsoft 시작 앱 관리 안내](https://support.microsoft.com/en-gb/windows/experience/startup-boot/configure-startup-applications-in-windows?nochrome=true)는 설정/작업 관리자에서 켜고 끄는 흐름을 설명한다. 이번 공식 자료 조사에서 `StartupApproved` REG_BINARY를 타 프로그램이 쓸 때의 전체 형식·버전 호환 계약은 찾지 못했다. 공식 계약이 없다고 증명한 것은 아니다.
- 기존 분류기의 0x02/0x03 읽기와 실제 쓰기는 별개다. 단순히 첫 바이트를 바꾸는 구현을 지원 완료로 표시하지 않는다. 다른 사용자의 SID, HKCU/HKLM, 32/64비트 보기, Run/Run32/StartupFolder를 구분한다.
- T6 평가 fixture: 테스트용 항목만 등록한 VM에서 OS 빌드·레지스트리 보기·전체 원문 bytes/타입/부재를 보존하고 작업 관리자 전환 전후 및 다음 로그인 동작을 확인한다. 미지원 형식/없음/패키지·예약 작업 항목은 쓰지 않고 설정 안내로 남긴다. T3 복구 저장소와 현재 값 일치 조건이 선행한다.
- 현재 활성화 근거: **없음**. T6에서 검증된 빌드·형식 목록이 생기기 전에는 자동 쓰기를 등록하지 않는다.

## A2 — Windows Update 캐시와 서비스 복구

- [IUpdateInstaller.IsBusy](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdateinstaller-get_isbusy)는 조회 시점의 설치/제거 진행 상태다. 문서도 이후 작업 기회를 예약하지 않는다고 명시한다. false 한 번을 다운로드·CBS 등 전체 작업의 배타적 잠금으로 해석하지 않는다.
- [Stopping a Service](https://learn.microsoft.com/en-us/windows/win32/services/stopping-a-service): 정지 요청과 실제 Stopped 관측은 다르며 종속 서비스 때문에 요청이 거절될 수 있다. 예제의 종속 서비스 전체 정지를 제품에 그대로 도입하지 않는다.
- 제품 조건(T9): 작업 중/재부팅 필요/판정 불가면 거절. 허용된 서비스만 원래 상태와 앱이 바꾼 사실을 변경 전에 내구성 있게 기록한다. 정지 대기·취소·시간 초과·삭제 예외 후에도 복구를 별도 단계로 수행하고 재조회한다. 원래 꺼져 있던 서비스를 켜거나 시작 유형을 바꾸지 않는다.
- VM fixture: 활동 상태의 재등장, STOP_PENDING 지연, 종속 서비스, 두 번째 정지 실패, 삭제 중 실패, 재시작 실패, 프로세스 종료 후 Pending 복구를 각각 검증한다. 허용 삭제 대상은 T5 파일 단위 재검증을 따른다.
- 현재 활성화 근거: **없음**. 업데이트 활동 감지/재진입 차단의 실효성이 평가되지 않으면 직접 정리를 활성화하지 않고 Windows 정리 도구 연결을 유지한다.

## A2 — 배달 최적화

- [Delete-DeliveryOptimizationCache](https://learn.microsoft.com/en-us/powershell/module/deliveryoptimization/delete-deliveryoptimizationcache?view=windowsserver2025-ps) 문서에는 FileId/IncludePinnedFiles가 있지만 설명 일부가 미완성이다. 이름만으로 기본 삭제 범위나 고정 파일의 처리 의미를 확정하지 않는다.
- [Microsoft 지원의 배달 최적화 안내](https://support.microsoft.com/en-us/windows/deployment/updates-lifecycle/delivery-optimization-in-windows)는 자동 캐시 관리와 디스크 정리 UI를 안내한다. 시스템 내부 캐시 경로를 직접 지우는 근거로 사용하지 않는다.
- T9 평가 fixture: 지원 OS의 모듈 존재·서명·명령 메타데이터와 실제 도움말, 활성 전송/고정 콘텐츠, 호출 전후 관측을 확인한다. PowerShell 실행 시 기존 고정 인자·환경·프로세스 수명 경계를 적용한다. 자동 우회나 서비스 강제 종료 없음.
- 현재 활성화 근거: **없음**. 평가 전에는 Windows 공식 정리 UI 연결만 제공한다.

## D — 주사율 시험과 복구

- preview.3 후속: 사용자 카드 실행 접근 지적을 반영해 일반 실행 Full 사용자에게 직접 시험 준비를 제공한다. 별도 인자 조건은 제거했으며 실제 변경은 확인 뒤 시작한다. [카드 연결 보고서](2026-09-27-display-card-action-integration.md). 실기 전환 검증 미완료와 단일 프로세스 복구 한계는 그대로다.
- 2026-09-27 후속: [Task 8 실행기·평가 화면 구현](2026-09-27-sp1-display-trial-implementation.md). 대역 41건 및 관리자 조회/CDS_TEST=0 검증. 실제 전환/원복/재부팅 평가는 남아 `--display-evaluation`에서만 제공한다. 강제 종료·네이티브 호출 정지 중 복구 보장을 추가하지 않았다.
- [ChangeDisplaySettingsExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw)는 CDS_TEST, 임시 변경, 사용자 프로필 저장 플래그와 DISP_CHANGE 오류를 구분한다. API 성공은 화면의 실제 가독성 확인이 아니다.
- 제품 조건(T8): 같은 디스플레이·현재 해상도/색 형식 유지, 모드 재열거, 시험 성공, 원래 모드 저장 후 임시 변경. 15초 내 유지 확인이 없으면 원래 모드 적용·재조회한다. 디스플레이 분리/외부 모드 변경을 무시하고 다른 장치에 원래 모드를 쓰지 않는다.
- 복구 한계: 단일 프로세스는 강제 종료·전원 차단 중 타이머를 실행할 수 없다. 영구 저장은 유지 확인 뒤에만 한다. 프로세스 종료까지 보장하는 별도 watchdog이 없다면 그 한계를 명시한다. 재연결·재부팅의 OS 동작도 VM/실기기 평가 없이 보장하지 않는다.
- fixture: 시험 거절, 임시 적용 실패, 유지 시간 초과, 사용자 취소, 원복 실패, 장치 분리, 외부 모드 변경, 앱 종료·프로세스 강제 종료. 현재 실제 변경 검증은 **미실시**.

## T1 링크 연결 실기기 관측

- 기존 데스크톱의 COM 연결은 [Microsoft의 Explorer 위임 예제](https://devblogs.microsoft.com/oldnewthing/20131118-00/?p=2643)와 [FindWindowSW](https://learn.microsoft.com/en-us/windows/win32/api/exdisp/nf-exdisp-ishellwindows-findwindowsw)를 참고했다. 새 Explorer 프로세스 실행으로 대체하지 않는다.
- 현재 호스트에서 데스크톱 Explorer는 동일 세션이지만 TokenElevation=1이었다. 따라서 자동 링크 열기를 거절하는 경로만 검증했다. URL 호출 없이 COM 연결만 검사하는 Smoke도 환경에 따라 성공 연결/거절을 명시적으로 구분한다. 비승격 Explorer에서 COM 성공·실제 브라우저 토큰 검증은 T12에 남는다.

## F2 후속 — Adobe 기본 오디오·파형 캐시 (preview.4)

기본 Common 하위 Media Cache Files의 `.cfa`/`.pek`, Peak Files의 `.pek`에 대해 90일 경과·Full 현재 프로필·보호/링크·파일 집합·알려진 앱 프로세스 관문을 구현했다. DB/프로젝트/렌더 및 사용자 지정 위치는 포함하지 않는다. [공식 근거·실행 계약·검증·경합 한계](2026-09-27-sp1-adobe-cache-implementation.md)를 읽고 이어갈 것. 프로세스 부재 관측을 OS의 작성자 배타성 보장으로 표현하지 않는다. Steam/NVIDIA·사용자 지정 앱 위치는 아직 이월이다.

## 영상 편집 캐시 진단 추가 / preview.5

사용자 요청으로 DaVinci Resolve·CapCut 읽기 검사를 추가했다. [계약과 검증](2026-09-27-video-editor-cache-inspection.md). Resolve의 작은 config.dat에서 CacheClip 위치만 해석하는 승격 읽기 예외와, Videos 바로 아래 CacheClip만 메타데이터 관측하는 보호 예외는 이 전용 inspector에 한정한다. 겹친 클라우드/문서/시스템 보호는 유지하며 공통 scanner·실행기 보호를 바꾸지 않는다. CapCut은 기본 Cache만 확인한다. 두 앱의 프로젝트 DB/사용자 지정 위치 전체 및 자동 삭제 계약은 아직 없다.

## 이월 책임표

| 항목 | 담당 | 완료 조건 |
|---|---|---|
| 서비스 계층 검사·사양·조치 상호 배제 | T2 | UI 없이 동시 직접 호출·실제 종료 뒤 해제 회귀 |
| 보호된 복구 저장소 | T3 | 원자 기록·SID·변조/링크·중단 복구 fixture |
| 사용자 지정 앱 캐시 경로 | T10 | 현재 사용자 설정의 안전한 조회와 실행 전 재검증 |
| StartupApproved 형식 | T6/T12 | 지원 빌드·형식 실측, 미검증 자동 쓰기 비활성 |
| Windows Update/DO 안전한 정리 | T9/T12 | 활동 감지·복구·지원 명령 평가, 미검증 직접 삭제 비활성 |
| UAC/다른 관리자/SystemOnly/Entra SID | T12 | 일반 셸 및 다른 자격 증명 평가 |
| 일반 권한 Explorer/브라우저, 셸 없음 | T12 | 성공·거절 양쪽 실기기 증거 |
| DPI/키보드/.NET 없는 PC/SmartScreen | T12 | 배포 EXE 실제 평가, 게시자 미서명 상태 명시 |
| 코드 서명·설치형 배포 | 별도 범위 | 포터블 ZIP 검증과 구분, SP1에서 완료 주장 없음 |
