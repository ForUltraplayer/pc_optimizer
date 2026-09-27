# T9-A — 업데이트 상태·서비스 복구 기반 구현

기준 `0baa42c`, `codex/sp1-actions`. 구현→UI/UX→검증 우선순위. 독립 리뷰/에이전트/새 패키지 없음. 아래는 구현 기록이며 동작 검증 완료가 아니다.

구현 커밋 `2451878` (`0baa42c..2451878`, 18파일).

## 구현

- `UpdateActivityReader`: 로컬 WUA `IsBusy`/`RebootRequired`와 wuauserv/BITS 상태를 각각 수집한다. 일부 오류는 null로 남기고 다른 조회를 계속한다. false는 설치/제거 진행 보고가 없다는 뜻이며 다운로드·CBS·전체 작업 유휴/배타성을 보장하지 않는다.
- `WindowsUpdateStateProbe`, `WindowsUpdateStateRule`, ScanService: 네트워크 검색 없는 시스템 범위 프로브와 상태 카드. 설치 진행, 재시작, 전환 중, 일부 확인 불가를 구분. 이 카드의 정상 관측을 정리 가능 후보로 승격하지 않는다. 온라인 드라이버 프로브와 별개다.
- `UpdateServicePlatform`: 코드 enum의 wuauserv/BITS만 SCM에서 열고 Query/Stop/Start 최소 권한을 요청. 현재 상태 비교→제어 요청→실제 상태 관측. Stop 요청 성공만으로 정지 완료라 하지 않는다. 종속 서비스 정지/시작 유형 변경/프로세스 kill 없음. 30초 관측 기한이며 네이티브 호출 자체를 중단할 수 있다고 주장하지 않는다.
- `UpdateServiceMaintenance`: 기존 공통 조치 워커 안에서 사용할 내부 기반. 저장소 독점 잠금, 기존 미완료/손상 기록 거절, 안정 상태 스냅샷, Running 서비스별 Pending 기록 후 중지. 원래 Stopped는 기록/중지/복구하지 않는다. 파일 작업 전 두 서비스 Stopped 재확인.
- 취소/예외 후 finally에서 각 서비스별 45초 독립 복구 토큰을 쓴다. 첫 서비스 실패 뒤에도 다른 서비스 복구를 시도하고 실제 Running 관측 뒤에만 완료 기록. 실패 기록을 지우지 않는다. 서비스 변경과 후속 파일 변경은 한 Started 전달자로 연결한다.
- `UpdateServiceRecoveryAdapter`: 복구 전용, 적용 Prepare는 항상 null. 검증된 서비스 키·원래 Running/적용 Stopped·같은 SID·시스템 범위만 허용한다. 현재 Running이면 기존 조율기가 쓰지 않고 완료 처리하고, Stopped이면 원래 Running 복구만 허용한다. 다음 실행에서 기록 목록을 통해 복구할 수 있도록 App에 등록했다.
- ActionId의 끝에 UpdateServices 추가(기존 enum 숫자 보존), RollbackCodec에 ServiceRecovery 용도로만 허용. RestoreCoordinator와 화면 문구에서 서비스 복구/파일 복원 불가를 구분한다.

## 아직 연결하지 않은 부분

**UpdateServiceMaintenance.RunAsync의 제품 호출자는 없다.** 서비스 중지와 Download 파일 정리 버튼을 노출하지 않았으며 이 작업에서 호스트 서비스를 변경하지 않았다. 다음 T9-B 구현자가 아래 선행 조건을 채워 연결한다.

1. WUA false 이외의 다운로드/BITS·CBS·재부팅 및 서비스 재진입 판정. 알 수 없는 상태는 거절. 중지 후 서비스 재활성화를 유발할 수 있는 COM 호출을 반복하지 않도록 시점 분리.
2. 고정 Windows SoftwareDistribution/Download 범위와 보호 출처 예외를 좁게 정의. 기존 Program Files 읽기/임시 파일 예외를 복사하지 않는다.
3. 미리보기 파일 집합 고정과 정지 후 동일성 대조. 매 삭제 직전 서비스 재시작/대상 변화를 검사하고 새 파일은 추가하지 않는다.
4. 파일 작업은 반환 전에 자식 I/O까지 종료해야 한다. 취소 시 먼저 파일 작업을 끝내고 서비스 복구. 서비스 정지도 Started이며, 미삭제 결과를 PC 변경 없음으로 표현하지 않도록 연결.
5. 외부 도구의 동시 서비스 변경에 대한 원자성 보장은 없다. 이미 원래 상태면 쓰지 않고 다른 서비스·시작 유형으로 범위를 늘리지 않는다. 복구 원본은 실행 관리자 SID에 귀속돼 다른 관리자에게 자동 공유하지 않는다.

## 컴파일과 후속 검증

- `dotnet build src/PcOptimizer.App/PcOptimizer.App.csproj -c Release --no-restore`: 경고 0/오류 0.
- 테스트 작성/실행 및 실제 서비스 중지/시작/캐시 삭제는 하지 않았다. 로컬 상태 프로브도 이번에는 실제 실행하지 않았다. 새 종합 검증 수치를 주장하지 않는다.
- T12: WUA 일부/전체 실패·취소/정지된 COM, 상태 조합, 같은 SID/다른 계정, 첫 중지 성공 후 둘째 실패, Pending 저장 실패, 적용 기록 저장 실패, StopPending 시간 초과, 파일 작업 예외/취소, 원래 Stopped 보존, 외부 재시작, 복구 하나 실패 뒤 다른 복구, 재실행 미완료 기록/UI를 대역과 VM으로 검증한다.
- REV-013(서비스 변경 시작/사전 거절), REV-016(시스템 범위·SID), REV-017/018(네이티브 종료/저장소/공통 관문)이 관련되며 기존 상태를 검증 완료로 갱신하지 않는다.

## 근거

- [IUpdateInstaller.IsBusy](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-iupdateinstaller-get_isbusy): 순간 설치/제거 상태이며 작업 기회를 예약하지 않음.
- [ISystemInformation.RebootRequired](https://learn.microsoft.com/en-us/windows/win32/api/wuapi/nf-wuapi-isysteminformation-get_rebootrequired): 업데이트 설치/제거 완료를 위한 재시작 필요 보고.
- [Stopping a Service](https://learn.microsoft.com/en-us/windows/win32/services/stopping-a-service), [StartServiceW](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-startservicew): 요청과 상태 전환 완료를 구분. Microsoft 예제의 종속 서비스 자동 정지는 채택하지 않음.
