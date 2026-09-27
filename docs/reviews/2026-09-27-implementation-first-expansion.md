# 구현 우선 배치 — 시스템 시작 등록·선택 캐시 위치

기준 `1aabef1`, 브랜치 `codex/sp1-actions`. 사용자 지시: 구현 → UI/UX → 검증. 독립 리뷰/서브에이전트 없음. 프리뷰/ZIP 생성 없음. 이 문서는 구현자의 변경 기록이며 동작 검증 보고서가 아니다.

## T6 — 모든 사용자 Run

- `Core/Actions/StartupRegistration.cs`, `StartupSelection.cs`: HKCU, HKLM64, HKLM32의 출처·보기·이름 식별. 원래 HKCU `hkcu-run-v1:` 복구 키 호환. 새 키는 `hklm64-run-v1:`/`hklm32-run-v1:`. 같은 이름을 출처별로 분리한다.
- `ActionModels.cs`: `MachineStartup`을 enum 끝에 추가해 기존 숫자 보존. 시스템 범위 실행기와 현재 사용자 실행기를 별도로 등록한다.
- `Probes/Actions/Startup/StartupRunPlatform.cs`, `StartupRunActionAdapter.cs`: 코드에 고정된 HKLM/보기만 열고 각 조상의 OPEN_LINK/REG_LINK 거절, 고정 핸들 재비교·쓰기·후속 관측을 사용한다. SID/세션·Full/SystemOnly 관문 유지. RunOnce·폴더·StartupApproved 값은 쓰지 않는다. 원자적 CAS 보장은 기존과 같이 없다.
- `RollbackCodec.cs`, `RollbackRecord.cs`, `RestoreCoordinator.cs`: 출처·시스템 범위를 기록하고 동일 원본 복원/외부 변경 거절. Applied 시작 등록 원본은 복원 전 자동 만료하지 않는다. 복구 기록은 작업을 실행한 관리자 SID의 저장소에 귀속되며 다른 관리자 계정으로 기록을 공유하지 않는다.
- `Applications/StartupItemsProbe.cs`, `App/Services/ScanService.cs`, `Core/Rules/StartupItemsRule.cs`: SystemOnly에는 별도의 시스템 범위 프로브 구성. HKCU 레지스트리와 사용자 시작 폴더는 열거하지 않고 HKLM/공용 폴더만 검사한다. 요약 범위도 표시한다.
- App의 조치 목록/개별 카드/복구 목록/실행 확인에 32/64비트와 모든 사용자 영향을 표시한다.

플랫폼 근거: [Microsoft Run/RunOnce](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), [WOW64 registry keys](https://learn.microsoft.com/en-us/windows/win32/winprog64/shared-registry-keys). 문서 확인은 구현 근거이며 실제 PC에서 쓰기/로그인 확인을 대신하지 않는다.

## T10 — 사용자 지정 Adobe 캐시

- 새 `Probes/Actions/Files/AdobeCacheLocationCatalog.cs`: 사용자가 고른 폴더를 최대 8곳, 앱 실행 동안만 보관한다. 키는 임의 GUID이며 경로가 실행 명령으로 바뀌지 않는다. 동일 경로·세션은 같은 키를 재사용하고 이미 발급한 키의 위치를 교체하지 않는다.
- `Media Cache Files` 또는 `Peak Files` 폴더 자체만 선택. 상대/UNC/장치/ADS/환경 변수/8.3 추정 경로 거절. 실행 준비에서 실제 정규 경로·고정 드라이브·등록 프로필·다른 사용자·보호 정책을 재확인한다.
- 기존 FileCleanupAdapter에 등록 카탈로그 공급. 기본 위치와 동일한 90일 이상 cfa/pek 제한·앱 실행 상태·파일별 ID/독점 핸들·신규 파일 제외를 재사용한다. DB/프로젝트/원본/렌더 파일 추가 없음.
- App의 폴더 선택은 메모리 등록만 한다. 미리보기는 기존 작업 관문에서 실행하며 별도 실행 확인 전에는 삭제하지 않는다. 선택은 재시작 후 사라진다. 취소·8곳 상한·잘못된 폴더를 별도 안내한다.

근거: [Adobe 미디어 캐시](https://helpx.adobe.com/id_en/premiere/desktop/troubleshooting/media-issues/manage-media-cache.html). 이 기능은 명시적 위치 선택이며 미검증 Premiere 환경설정을 자동 해석하지 않는다.

## T10 — Resolve·CapCut 수동 위치 검사

- 새 `Probes/Applications/VideoCacheLocations.cs`: 앱별 하나의 선택 경로를 스레드 안전한 메모리에 보관한다. Resolve는 CacheClip, CapCut은 Cache 폴더 자체만 선택한다. 재검사 시 스냅샷을 전달하고 초기화하면 자동 탐지/기본 위치로 돌아간다.
- `AppCacheProbe.cs`, `VideoEditorCacheInspector.cs`, `ScanService.cs`: 선택 경로 우선, 기존 조상 링크·보호·다른 사용자·예산·부분 집계 재사용. 선택 위치가 없거나 읽지 못하면 그 결과를 표시하고 다른 폴더로 대체하지 않는다.
- 사용자 지정이라는 출처를 표시하며 앱 설치/설정/캐시 정체를 자동 검증한 것으로 표현하지 않는다. 삭제 기능 추가 없음. 경로는 기존 측정값/익명화 경로를 사용하며 명령 실행·내용 읽기 없음.
- App 조치 창의 Resolve/CapCut 버튼 → 폴더 선택 → 재검사. SystemOnly/Unknown에서는 선택 버튼이 숨겨진다. 선택만으로 경로 보호를 해제하지 않는다.

## 컴파일 확인과 검증 대기

- 실행: `dotnet build src/PcOptimizer.App/PcOptimizer.App.csproj -c Release --no-restore` → 경고 0, 오류 0.
- 중간 1회 App이 internal ActionUnavailableException을 참조해 CS0122 발생. public API를 결과 튜플로 바꾸어 내부 예외/원문을 UI에 노출하지 않도록 수정 후 빌드 통과.
- **테스트 실행/새 회귀 작성/실기 변경 없음**. 이전 기본 1620, Smoke 48 결과는 이 배치 검증에 재사용하지 않는다. 종합 검증 시 아래 항목을 작성/실행한다.
  - HKLM32/64 동일 이름 분리, HKCU 옛 기록 호환, MachineStartup scope 위조, 잘못된 출처·보기, 외부 값 변경·복구, 원본 만료 방지, SystemOnly가 HKCU를 조회하지 않음.
  - Adobe 선택 키/세션/중복/8곳 상한, 폴더 명·상대/UNC/별칭/보호/타 사용자/정션, 앱 실행 중, 파일 변경·90일 경계·신규 파일 제외, 선택 취소·계획 실행 흐름.
  - 영상 수동 경로의 기본값 대체 금지, 보호/정션/예산/부분 집계, 재검사 실패/빠른 클릭/기본 위치 복귀/SystemOnly, 익명 내보내기.
  - 실제 HKLM 등록 해제/복원·다음 로그인, UAC 다른 계정, Adobe 정리 뒤 편집 재개, 실기 폴더 선택/Resolve·CapCut 다른 드라이브는 T12에서 평가.

관련 원장: REV-008(경로), REV-013(실행 전 거절), REV-016(범위), REV-017/018(작업 수명). 기존 상태는 변경하지 않는다. 별도 승인/검증 완료를 기록하지 않는다.

## 남은 구현

- T6: StartupFolder 파일 보존·복구, StartupApproved 전체 바이너리 계약. 현재 지원 Run 등록 해제와 구분.
- T9: Windows Update Download·wuauserv/bits 상태 journal·중지/원복/재시작 복구.
- T10: Steam/NVIDIA 실행 전 idle 계약과 자동 정리, 버전별 앱 설정 자동 경로 탐지.
- 위 기능 구현 뒤 T11 UI/UX 개선, T12 종합 검증·배포.
