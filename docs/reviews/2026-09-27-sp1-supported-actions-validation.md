# SP1 지원 조치 통합 — 구현·자체 점검 및 평가 배포

사용자 지시대로 독립 리뷰 없이 T5 이후 가능한 구현과 검증을 이어갔다. 아래는 구현자의 자체 검증이며, SP1 전체 자동 조치/실환경 출시 승인이 아니다. 실행 중인 에이전트나 백그라운드 제품 작업은 없다.

## 실제 등록과 제한

| 기능 | 등록 코드 | 범위 / 되돌리기 | 현재 상태 |
|---|---|---|---|
| A1 Temp·Explorer 캐시 | FileCleanupAdapter / UserTempTargets | Full / 영구 삭제 | 7일 경과·고정 파일 집합·핸들 재검증·부분 결과 |
| A2 Windows Temp | FileCleanupAdapter.ForSystemTemp / SystemTempTargets | System, Full·SystemOnly / 영구 삭제 | Windows Known Folder 고정·모든 등록 사용자 프로필 제외·같은 파일 엔진 |
| C 전원 계획 | PowerActionAdapter / PowerSettingsPlatform | Full / 저장 기록과 현재 값 일치 시 복원 | 설치된 기본 GUID만 적용. 새 계획 생성/복제 없음. 고성능 AC 조건. 실제 쓰기 평가는 미실시 |
| F1 npm·pip·NuGet HTTP | OfficialCacheActionAdapter / SystemCacheToolBackend | Full / 되돌리기 없음 | 보호 위치 도구만, 고정 인자, 실제 Process.Start 직전 검증 및 성공 후 Started 기록 |
| B 시작 앱 | 자동 어댑터 미등록 | Windows 시작 앱 연결 | Windows 11 빌드별 StartupApproved 전체 bytes/로그인 동작 근거 없음 |
| D 주사율 | 자동 어댑터 미등록 | Windows 디스플레이 연결 | 실제 시험·장치 분리·15초 원복/크래시 경계 검증 없음 |
| A2 Update/DO | 자동 어댑터 미등록 | Windows 저장소 연결 | 업데이트 활동 배타성·서비스 복구·DO 대상 계약 실환경 검증 없음 |
| F2 Adobe·Steam·NVIDIA | 자동 어댑터 미등록 | 앱 자체 캐시 관리 안내 | 앱별 옮긴 경로와 idle 판정 계약을 확정하지 못함. 대형 폴더/커뮤니티 규칙을 삭제 허가로 승격하지 않음 |

자동 등록은 `App.xaml.cs`의 직접 어댑터 3개(UserFiles, SystemFiles, OfficialCache)와 복원 어댑터 1개(Power)다. `ActionDefinition`에서 삭제는 Irreversible, 설정은 Caution, BatchEligible=false다. 일괄 자동 최적화는 없다. B/D/Update/DO/F2는 완료 체크하지 않고 이유를 UI·계획·인계서에 남겼다.

## 주요 변경 파일과 점검 결과

- `Probes/Actions/Power/`: `PowerEnumerate`로 설치 GUID만 조회하고 `PowerSetActiveScheme`으로 현재 사용자 계획만 변경한다. native DLL 검색은 System32. 미리보기의 원래 GUID/AC/세션을 고정하고 실행 시 다시 확인한다. T3 Pending 영속 저장 뒤 쓰고 실제 활성 GUID 재조회 뒤 Applied. 복구는 현재 적용 값 일치 조건과 기록의 전체 GUID bytes 검증. API 실패/계획 소실/배터리 전환/외부 변경/후속 읽기 실패는 각각 코드/기록으로 남긴다. 외부 프로그램과 원자적 CAS는 보장하지 않는다.
- `Files/SystemTempTargets`는 Windows 폴더와 SystemDirectory 부모 일치를 확인한다. 임의 사용자 입력 경로, Update/Installer/WinSxS로 확대하지 않는다. 프로필 목록 및 포함 보호 정책을 확인하고 전체 루트가 겹치면 거절한다. 서비스 시작/정지는 구현하지 않았다.
- `OfficialCacheActionAdapter`는 기존 CacheCleanupService 안으로 중첩 진입하지 않는다. 공통 조율기만 계획/관문을 소유하고 backend/고정 명령을 재사용한다. 경로/지문/보호 정책을 재검사하며 `IActionExecution.TryStartProcess`가 실제 시작 전에 만료/세션/취소를 검증하고 Start=true 직후 시작을 기록한다. Start 실패는 Started=false, 살아 있는 도구는 실제 종료까지 관문 유지. 종료 후 관측 실패는 성공으로 숨기지 않는다.
- `ActionEffect`의 공식 도구 전후 값은 논리 크기다. 실제 여유 공간으로 바꾸어 표시하지 않는다. 파일 엔진은 실측 볼륨 여유 변화와 삭제/건너뜀/실패 개수를 따로 표시한다.
- `ActionCenterViewModel/Window`, `ActionPresentation`, `MainWindow`, `App.xaml.cs`: 효과 설명→대상 확인→별도 실행 확인→결과/되돌리기. 기존 공식 캐시 버튼도 공통 창을 연다. 선택/결과 시 위로 스크롤하여 긴 목록 아래에 묻히지 않는다. 미지원 자동 조치는 수동 설정 연결과 사유를 보여준다. 설정 열기 자체를 Applied 결과로 기록하지 않는다.
- 도구 목록은 앱 시작 시 보호 위치 존재 스냅샷이다. 설치 변경 후 목록 반영에는 앱 재시작이 필요하다(이월). 세션이 SystemOnly/Unknown이면 공식 도구 후보 탐색 자체를 생략한다. UI가 숨겨져도 backend/조율기가 독립적으로 사용자 범위를 거절한다.
- 익명 내보내기 모델에 ActionPreview의 실제 경로, 복구 기록 bytes/SID나 명령 본문을 추가하지 않았다. 기존 개인정보 정리 회귀를 유지했다.

## 검증 명령과 근거

`C:\Program Files\dotnet\dotnet.exe`, Release. TRX는 `tests/PcOptimizer.Tests/TestResults/`(git-ignored).

1. `build PcOptimizer.sln -c Release --no-restore`: **경고 0·오류 0**.
2. `test tests/PcOptimizer.Tests -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke|FullyQualifiedName~PowerActionSmokeTests' --logger 'trx;LogFileName=sp1-final-basic-power-green.trx'`: **1409/1409 = 기본 1408 + 읽기 전용 전원 Smoke 1**.
3. `test ... --filter 'Category=Smoke' --logger 'trx;LogFileName=sp1-final-smoke.trx'`: 관리자 **41/41**. 이후 Power DLL 검색 속성·UI 문구/안전 배지·패키지 버전 정규식만 수정했다. 위 2번에서 전원 조회와 전체 기본을 다시 확인했다.
4. `test ... --no-build --filter 'Category=ToolSmoke' --logger 'trx;LogFileName=sp1-t10-tools-admin.trx'`: 관리자 **4/4**. 기존 pip fixture와 신규 npm/pip/NuGet fixture 3건. `PCOPTIMIZER_TEST_NODE=C:\Program Files\nodejs\node.exe`, `PCOPTIMIZER_TEST_DOTNET=C:\Program Files\dotnet\dotnet.exe`, `PCOPTIMIZER_TEST_PYTHON=C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`. 모두 생성한 테스트 캐시만 명시적으로 고정하고 설치 패키지 fixture 보존 확인. 제품의 도구 보호 위치 검사는 대역/기존 회귀로 별도 검증한다.
5. 전원 대역 계약 **17/17**: Pending 선행·적용/복구·이미 적용·미설치·AC 불명/배터리·조회 실패·SystemOnly·미리보기 후 변경·API 실패·재조회 불일치·외부 변경·복구 실패/계획 소실. 실제 Windows 계획을 바꾸지는 않았다.
6. 신규 공식 조치 대역 **7/7**: 실제 시작 성공/실패, 시작 직전 세션 변경, 보호/지문 변경, 후속 관측 실패, 늦은 종료 동안 공유 관문 유지.
7. `PCOPTIMIZER_UI_ARTIFACTS=artifacts/sp1-final-ui`로 440×720 논리 크기 100/150/200% 오프스크린 렌더와 실제 WPF 디스패처 테스트. `action-choices.png`, `action-selected.png`를 육안 확인. 예시 데이터는 fixture이며 실 PC 진단 결과가 아니다. 실제 키보드 입력이나 사용자 화면 창을 열지 않았다.
8. `git diff --check` 통과. Online은 네트워크 수집 변경이 없어 재실행하지 않았다.

## 초기 실패와 수정

- 제한된 환경 ToolSmoke 4건 중 NuGet이 ToolFailed(3/4), 동일 소유 fixture를 승인된 관리자 조건으로 실행해 4/4. 권한/보호 정책을 완화하지 않았다.
- 패키지 버전을 평가판 `0.3.0-preview.1`로 변경하자 기존 `숫자.숫자.숫자` 정규식 테스트가 실패했다. prerelease 식별자를 허용하도록 기존 테스트를 갱신했다.
- 공간 정리 Partial 문구가 설정 조치 회귀까지 덮는 것을 기존 테스트가 잡았다. 공간 조치에만 새 문구를 적용하고 전체 통과했다.
- package 최초 시도는 샌드박스가 기존 NuGet.Config 읽기를 거절했다. 승인된 기존 설정 읽기/패키징으로 locked restore·publish·ZIP 검증 통과했다. 새 의존성을 추가하지 않았다.

## 평가 배포

- `tools/package.ps1 -OutputDirectory dist/sp1-evaluation`.
- `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.1-win-x64.zip`: **59,872,145 bytes**, 최상위 4개/내부 파일 13개. 단일 EXE 65,121,558 bytes, 런타임 포함.
- SHA-256: `A87A7E1B78904F50A67D279628EB421862DBD9F38ED62727F53F0A6652793E1F`.
- verify-package가 ZIP 파일/필수 고지/규칙 원문·source hash/ZIP hash 검증 완료. 실행방법에 자동 지원 범위와 미지원·미검증 조건을 동봉했다. 구 Task 1 ZIP은 보존했으며 최신이라고 안내하지 않는다.

## 남은 출시 조건

- 실제 일반 셸 UAC, 표준 계정→다른 관리자(SystemOnly), 비승격 브라우저 성공, .NET 없는 PC 실행, 실제 모니터 DPI/키보드/SmartScreen.
- 별도 평가 PC의 실제 전원 적용·복원과 정책 거절. Windows Temp/사용자 Temp 실환경 다중 사용자/클라우드·Known Folder 이동 조합. 현재 코드 등록은 평가판이며 실제 쓰기 검증 완료로 표시하지 않는다.
- B/D/Update/DO/F2는 위 표의 근거 부족으로 자동 구현·활성화가 남았다. 전체 SP1 완료나 제품 범위 영구 축소를 승인받았다고 간주하지 않는다. 각 계약을 검증할 평가 환경과 근거를 확보한 뒤 이어간다.
- 사용자 캐시·실제 PC 설정 변경, master 병합, 원격 게시/릴리스 업로드는 하지 않았다.

공식 API 근거: [PowerEnumerate](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerenumerate), [PowerSetActiveScheme](https://learn.microsoft.com/en-us/windows/win32/api/powersetting/nf-powersetting-powersetactivescheme). B/D/Update/DO 조건은 [플랫폼 계약](sp1-platform-contracts.md)을 유지한다.
