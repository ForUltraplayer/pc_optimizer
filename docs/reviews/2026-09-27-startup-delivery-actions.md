# 시작 등록·배달 최적화 실행 통합

기준 `e7413ea`, 브랜치 `codex/sp1-actions`. 사용자 최신 지시: 기능 하나마다 프리뷰를 만들고 멈추지 말고 남은 기능을 묶어서 구현한다. 이번에는 버전 변경·ZIP 생성 없이 소스와 실행 흐름을 통합한다. 독립 리뷰/에이전트는 사용하지 않았다.

## 구현 범위와 파일

- Core `Actions/StartupRegistration.cs`, `StartupSelection.cs`: 현재 사용자 64비트 Run의 등록 이름/형식 검증, 스냅샷에서 사용자 선택 목록 구성. 명령을 해석하거나 실행하지 않는다.
- Probes `Actions/Startup/`: REG_SZ/REG_EXPAND_SZ 원문 보존, 미리보기 이후 비교, 링크를 따라가지 않는 각 단계 키 열기, 같은 핸들에서 원문 재비교 후 값 한 개 제거/복원. 공통 SID·세션·Full 범위와 내구 journal을 사용한다.
- `RollbackRecord.CanExpire`: 해제된 시작 등록의 원본을 담은 Applied 기록은 복원 전 30일 자동 정리에서 제외. Restored/Unchanged는 기존 완료 기록 정책 적용. 적용 중인 원본의 유일한 사본을 시간만으로 지우지 않는다.
- `RestoreCoordinator`: 시작 항목 이름과 실제 복원 효과를 확인 화면에 표시한다. StartupApproved 값은 쓰지 않으며 원래 Run 등록만 복원한다.
- Probes `Actions/SystemCleanup/`: Windows 배달 최적화 제공자를 통해 미리보기 당시의 비고정 Caching 파일 ID만 정리한다. 서비스 중지, 시스템 캐시 경로 직접 삭제, 셸 실행은 없다.
- App 조립/선택 모델/카드/XAML: 시작 등록을 검사 결과에서 선택하거나 개별 카드에서 바로 준비한다. 다시 검사하면 목록을 교체하고 SystemOnly는 사용자 항목을 제외한다. DO는 별도 시스템 조치로 선택한다. 미리보기와 실행은 분리하고 끝나면 기존 공통 재검사·결과·복구 흐름을 사용한다.

## T6 계약 변경과 한계

기존 T6의 StartupApproved 비공개 이진 형식 쓰기는 계속 미구현이다. 이를 임의로 만들지 않고 [Microsoft가 문서화한 Run 등록](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys) 자체의 해제/복원으로 현재 사용자 범위를 구현했다. 화면에도 **자동 실행 등록 해제**로 명시한다.

- 대상: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, Registry64, 값 이름 1~100자(공백만/제어·서식·surrogate 문자 제외). 일반 문자열/확장 문자열의 유효한 UTF-16 원문 <=32 KiB만.
- HKLM, RunOnce, StartupFolder, 예약 작업, 서비스, 패키지 startup은 제외. 동일 이름의 다른 출처를 대신 변경하지 않는다.
- 전체 원문/형식은 보호된 복구 기록에만 저장하며 새 내보내기/로그에 명령을 싣지 않는다. 앱을 종료하거나 삭제하지 않는다. 재로그인 시 등록이 없어진 효과이며 앱이 자체 재등록할 수 있다.
- 원본 복원은 값이 여전히 없을 때만 한다. 현재 값이 이미 원본이면 기존 AlreadyOriginal 경로. 앱이 새 값을 등록했으면 관측 시 거절한다. Task Manager의 사용/사용 안 함 상태는 그대로이므로 등록 복원=활성화라고 약속하지 않는다.
- Windows 기본 값 쓰기는 CAS가 아니다. 고정 핸들의 마지막 비교와 쓰기 사이에 외부 앱이 바꾸는 경합을 원자적으로 막는다고 주장하지 않는다. 전후 비교와 내구 원본 보존이 경계다. 실제 다음 로그인·외부 앱 동시 재등록·다른 계정 UAC는 미검증.
- 처음에는 [RegOpenKeyTransacted](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regopenkeytransactedw)를 시도했으나 이 호스트에서 전용 fixture의 pinned/null 및 root/full-path 모두 오류 6801. 해당 구현/진단 코드는 최종 코드에서 제거했다. OS 자원 관리자를 초기화하거나 PC 설정을 고치지 않았다.

## T9 DO 계약

[Microsoft 배달 최적화 관리 문서](https://learn.microsoft.com/en-us/windows/deployment/do/waas-delivery-optimization-monitor)의 FileId별 삭제·pin 제외 계약과, 이 PC의 System32 `DeliveryOptimizationSettings.psm1`의 실제 구현을 확인했다. 공식 cmdlet은 로컬 `root/Microsoft/Windows/DeliveryOptimization:MSFT_DeliveryOptimizationFile.Delete(fileId, deletePinned)`를 호출한다. 앱도 같은 제공자 메서드를 형식 검증 후 호출한다. `-Force`나 임의 PowerShell 문자열은 실행하지 않는다.

- 조회는 FileId/String, FileSizeInCache/UInt64, Status/UInt8, IsPinned/Boolean만. 최대 1,024개; 중복 ID·불명 상태·조회 실패는 거절.
- 로컬 제공자의 열거형은 Downloading=0, Complete=1, Caching=2, Paused=3. 0/3이 하나라도 있으면 보류. 삭제 후보는 상태 2·비고정·0보다 큰 캐시만.
- 계획에서 ID 집합을 고정한다. 새로 생긴 ID는 추가하지 않는다. 매 항목과 제공자 메서드 호출 직전에 전체 활동/같은 ID의 크기·상태·pin을 다시 읽는다. 없어진 ID는 건너뛰고 바뀐 항목은 거절한다.
- `deletePinned=false` 고정. 매개변수/반환 형식을 확인하고 실제 호출 직전에만 Started를 기록한다. 메서드 성공과 후속 실제 부재/0바이트를 함께 확인한다. 관측 실패·중간 변화에도 확인한 개수를 보존한다.
- 네이티브 동기 호출이 UI 취소에 응답하지 않으면 실제 반환까지 공통 관문을 유지한다. 취소가 제공자 내부 삭제를 즉시 중단한다는 보장은 없다. 다운로드 시작 자체를 OS 차원에서 잠그는 기능도 아니다.
- 시스템 조치라 SystemOnly에서도 지원한다. 전체 사용자 영향·재다운로드·되돌릴 수 없음을 확인 화면에 표시한다. Windows Update Download/DataStore/Installer/WinSxS 및 서비스 복구 구현으로 확대하지 않는다.
- 실제 호스트 읽기: 상태 2/비고정인 캐시 2개, 791,688,705 + 51,711,324 bytes. **실제 삭제는 하지 않았다.** 삭제·실패·부분 결과는 제공자 대역 테스트이며 Windows 실기 정리 평가는 남는다.

## 검증과 초기 실패 기록

공통 명령 접두사: `& 'C:\Program Files\dotnet\dotnet.exe'`.

- `build -c Release --no-restore`: 경고 0/오류 0.
- `test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=actions-basic-complete.trx' --results-directory TestResults/actions-batch`: **1620/1620**(기준 1564 대비 신규 56).
- 신규 시작 조치/선택/실제 XAML/DO 대역: 선택만으로 미변경, 원문·형식 보존, journal 접근 실패, 복구 기록 변조, 재등록, 범위 변화, pin/active/불명/중복/새 ID, 실제 호출 경계, 부분 실패, 취소 후 관문 유지.
- `Category=Smoke` 첫 전체: 47/48. 실패 1개는 제품 링크 거절이 아니라 테스트 fixture 정리의 `RegDeleteKeyEx(path)`가 링크 대상을 따라가는 동작이었다. cleanup을 OPEN_LINK 핸들에 대한 NtDeleteKey로 고쳤고, 남은 **정확한 GUID 키**를 대상 문자열까지 확인해 정리했다. 제품은 링크 거절 후 값을 변경하지 않는다.
- 해당 native 재검사 `FullyQualifiedName~StartupRunSmokeTests|FullyQualifiedName~DeliveryCacheSmokeTests`: **4/4**. 실제 사용자 Run과 분리된 GUID 키만 쓰기 테스트. 배달 최적화는 조회/인자 생성만 확인하며 Delete를 호출하지 않는다.
- 최종 전체 Smoke 결과는 아래 완료 기록에 추가한다. Online/ToolSmoke는 관련 기존 코드 변경이 없어 이번 미실행.
- 초기 수정 이력: 레지스트리 fixture 생성은 샌드박스에서 접근 거부→fixture 한정 승격 실행; TxR 6801→위 계약 조정; EnumerationOptions 모호성→네임스페이스 명시; DO 두 번째 항목의 MarkStarted 중복→한 번만 기록하도록 수정. 실패 TRX는 `TestResults/startup`, `TestResults/actions-batch`에 보존한다.

## 잔여와 전달

사용자 지시대로 독립 리뷰는 생략하며 위 검증을 독립 승인으로 표기하지 않는다. REV-013 시작 경계/부분 결과, REV-016 사용자 범위, REV-017/018 실제 작업 수명, T3 원본 보존을 신규 경계에서 확인했다. 기존 원장의 발견·독립 재검증 상태를 변경하지 않는다.

T6의 다른 시작 출처/Task Manager 비공개 형식, T9 Windows Update 서비스/Download 직접 정리, T10 사용자 지정 Adobe/Steam/NVIDIA 자동 삭제, T8/T12 실기 평가는 남는다. 특히 새 읽기 전용 캐시 검사 결과를 자동 삭제 권한으로 승격하지 않는다. 이번에는 새 프리뷰/ZIP을 만들지 않으므로 기존 preview.6 ZIP은 이 변경을 포함하지 않는다.

## 완료 기록

최종 전체 Smoke **48/48**: `test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category=Smoke' --logger 'trx;LogFileName=actions-smoke-final.trx' --results-directory TestResults/actions-batch`. 단순 대역 통과와 달리 실제 레지스트리 링크 거절/fixture 정리 및 DO 조회 ABI를 포함한다. 실제 로그인·DO 캐시 삭제는 이 결과에 포함되지 않는다.
