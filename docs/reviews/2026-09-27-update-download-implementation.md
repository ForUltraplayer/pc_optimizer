# T9-B Windows Update Download 실행 연결

기준 `3456a75`. 사용자 지시 **구현 → UI/UX → 검증**, 독립 리뷰/서브에이전트/개별 프리뷰 생성 없음. 이 기록은 구현 보고이며 동작 검증 완료가 아니다.

## 실행 계약

- `WindowsUpdateCache` 시스템 조치와 고정 키 `windows-update-download`를 등록한다. Windows Known Folder에서 구성한 `SoftwareDistribution\Download`만 선택하며 UI 경로 입력·DataStore·Installer·WinSxS·설치된 업데이트 삭제는 없다.
- 생성·수정 시각이 모두 7일 이전인 일반 파일만 기존 `FileCleanupAdapter`가 미리보기에서 고정한다. 기존 정규화/8.3·모든 등록 사용자 프로필·보호 정책·링크/placeholder·동일 볼륨·hardlink·조상 핸들·파일 ID/메타데이터 검사를 유지한다. 보호 예외를 추가하지 않았다. 폴더 자체는 지우지 않고 실행 중 생긴 새 파일은 계획에 넣지 않는다.
- 준비/실행 전 wuauserv·BITS가 **모두 Running이며 STOP 수락** 상태여야 한다. 이미 정지된 서비스를 조회 목적으로 명시적으로 시작하지 않는다. WUA 설치/재부팅, 모든 사용자 BITS 작업 개수, DO 다운로드/일시 중지, CBS·파일 교체 재부팅 대기, 알려진 업데이트/서비스 구성 작업 프로세스를 확인한다. 확인 불가나 활동 중이면 거절한다. BITS의 이름·URL·대상 파일은 수집하지 않으며 작업이 하나라도 있으면(정지/오류 포함) 자동 취소하지 않고 거절한다.
- 예상 논리 크기가 0이면 서비스 중지 전에 종료한다. 실제 실행은 기존 내구 저장소를 잠근 채 서비스별 Pending 저장 → 중지/관측 → 고정 파일 집합 전체 재대조 → 개별 파일 독점 핸들 삭제 → 원래 실행 서비스 복구 순서다. 사용자 취소/오류도 finally에서 독립 복구 기한을 사용한다. 복구 실패는 기록을 보존하고 결과를 실패로 남긴다.
- 중지 후에는 WUA/BITS COM을 다시 호출하지 않는다. 파일 처리 경계마다 서비스 정지 상태·프로세스/재부팅 조건·DO를 확인하며 DO 조회 후에도 서비스 상태를 다시 읽는다. 새 활동·서비스 재시작 시 추가 삭제를 중단하고 기존 처리 개수를 남긴다. 실제 네이티브 작업과 복구가 끝날 때까지 공통 실행 관문을 유지한다.
- 서비스 변경 자체도 Started다. 결과의 `ServiceRecoveryCompleted`는 기록한 서비스들의 원상태 재관측 결과를 파일 처리 수와 별도로 전달한다. UI에서 삭제 파일 복구 불가와 서비스 복구 가능을 구분한다. 작업 후 기존 공통 재검사 흐름을 사용한다.

## 변경 파일

- 신규 `Probes/Actions/SystemCleanup`: `BitsJobReader`, `UpdateCleanupGuard`, `WindowsUpdateDownloadTargets`, `WindowsUpdateCleanupAdapter`.
- `UpdateServiceMaintenance`: 실제 호출 연결, 서비스 복구 결과 전달. `Core/Actions/ActionModels`, `ActionCoordinator`: 시스템 조치 등록/범위와 결과 계약.
- `App.xaml.cs`, `ActionPresentation`, `ActionCenterViewModel`, `WindowsUpdateStateRule`: 조치 선택·사유·복구 결과와 기존 미지원 문구 정정. UI/UX 전면 개편은 T11에 남는다.

## 공식 근거와 제한

- [Microsoft 업데이트 문제 해결](https://support.microsoft.com/en-US/Windows/Deployment/updates-lifecycle/troubleshoot-problems-updating-windows), [업데이트 구성 요소 수동 초기화](https://learn.microsoft.com/en-us/troubleshoot/windows-client/installing-updates-features-roles/additional-resources-for-windows-update): 서비스 중지 후 캐시를 다루는 참고 근거다. 광범위한 초기화 절차의 CryptSvc·DataStore·등록 초기화·ACL 변경은 구현하지 않는다.
- [BITS EnumJobs](https://learn.microsoft.com/en-us/windows/win32/api/bits/nf-bits-ibackgroundcopymanager-enumjobs), [IEnumBackgroundCopyJobs](https://learn.microsoft.com/en-us/windows/win32/api/bits/nn-bits-ienumbackgroundcopyjobs), [공식 SDK Bits.h](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/Bits.h): ALL_USERS=1 및 COM GUID/메서드 순서 참고. ABI 실제 호출 시험은 아직 하지 않았다.
- 상태 조회와 서비스 제어는 OS의 전역 원자적 업데이트 잠금이 아니다. 조회 직후 새 작업/서비스 재시작 가능성, 알 수 없는 작성자, BITS 외 전송, WMI/COM 지연이 남는다. 오래된 파일·고정 집합·직전 재조회·독점 핸들은 위험을 줄이는 조건이며 업데이트 전체의 배타적 유휴를 증명하지 않는다. 다른 SID의 앱 인스턴스/Windows 자체 작업까지 현재 앱 관문으로 직렬화했다고 주장하지 않는다.
- 두 서비스 모두 Running 조건과 모든 BITS 작업 거절은 의도적으로 보수적이다. 보통 BITS가 정지된 PC에서는 사용할 수 없고 Windows 저장소 정리 안내를 유지한다. COM이 조회 중 서비스 활성화에 영향을 줄 가능성까지 무부작용으로 보장하지 않는다.

## 컴파일 / 후속 검증

- `dotnet build src/PcOptimizer.App/PcOptimizer.App.csproj -c Release --no-restore`: 최종 **경고 0/오류 0**.
- 테스트 작성/실행, Smoke/ToolSmoke, UI 실기, 실제 COM/BITS 조회·서비스 제어·캐시 삭제는 실행하지 않았다. 과거 1620/48 수치는 새 코드에 적용하지 않는다. 배포 ZIP/버전 변경 없음.
- T12: BITS ABI/권한/등록 없음/작업 상태, DO 불명/지연, 서비스 전환·중간 재시작, 첫/둘째 중지 실패, 파일 실행 전 변경/부분 삭제/취소, 복구 한쪽 실패·프로세스 종료 뒤 복구, 원래 정지/다른 계정/다중 인스턴스, 경로/링크/별칭·보호, 0바이트와 신규 파일, 준비/실행 토큰 분리, Started·복구 결과 문구를 회귀 및 VM에서 검증한다. 사용자 PC의 업데이트 캐시를 개발 시험으로 삭제하지 않는다.
- T9 구현 연결까지 진행했으며 검증 완료로 닫지 않는다. 다음 구현은 T10-B Steam/그래픽별 지원 계약과 실행 경계이며, 검증 불가한 공급자는 공식 기능 연결을 유지한다.
