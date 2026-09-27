# SP1 Task 8 주사율 시험 실행기·평가 화면

사용자 최신 지시: **UI/UX·문구 개선은 추후, 기능 추가 구현 우선. 독립 리뷰 없이 진행.** 이번 작업은 주사율 시험/유지/원복 기능이며 독립 리뷰 승인이 아니다. 기존 리뷰 상태·발견 이력은 변경하지 않았다.

구현·검증 코드 커밋 `a885d22` (기준 `aec6c27`), 22개 변경 파일. 배포 ZIP은 같은 소스의 커밋 전 작업 트리에서 생성했다. 후속 인계 문서는 바이너리를 변경하지 않는다.

## 구현 범위

- `Probes/Actions/Display/DisplaySettingsPlatform.cs`: 실제 출력 경로(모니터·어댑터·GDI·LUID·source/target·출력 기술) 재식별. Full 로컬 대화형 사용자, 같은 해상도/방향/32bpp/프로그레시브의 열거 모드만 허용한다. 복제·원격·간접/가상·이름 실패는 거절한다. 현재/프로필 DEVMODE 220bytes 원문과 필드 일치를 확인하고 쓰기에서는 새로 읽은 현재 DEVMODE의 주사율 필드만 지정한다.
- `DisplayTrialCoordinator.cs`, `DisplayTrialModels.cs`: 공유 작업 관문, 5분 단조 만료·일회성 계획, CDS_TEST 재검사, 원본 Pending 선행 저장, 임시 적용, 15초 단조 기한, 유지 때만 현재 사용자 프로필 저장, 실제 모드 재조회. 쓰기 직전 관문을 통과한 경우에만 Started=true. 취소는 원복을 취소하지 않는다.
- Pending은 시험 중 계속 유지하고 유지/검증/저장 성공 후 Applied(UserUndo)로 전이한다. Restoring/Restored·Unchanged와 실제 원복/기록 실패를 구분한다. 미해결 표시 기록이 있으면 다음 시험을 막는다. 재실행 시 같은 장치·현재 값만 복구하며 외부 변경을 덮어쓰지 않는다. 네이티브 API의 compare-exchange 원자성은 보장하지 않는다.
- `App/ViewModels/DisplayTrialViewModel.cs`, `Views/DisplayTrialWindow.*`: 후보 선택 → 사전 검사/미리보기 → 별도 실행 → 유지/원래대로 → 결과/재검사. 중단 기록 및 유지 후 되돌리기는 별도 확인 후 실행한다. UI 타이머는 표시용이고 원복은 워커 소유. 시험 중 창/메인 창 닫기는 중단 요청 후 실제 작업 종료까지 대기한다.
- `App.xaml.cs`, `MainWindow.*`: `--display-evaluation` 인자와 Full 범위일 때만 평가 버튼/실행기를 조립한다. 일반 실행의 자동 조치 목록에는 추가하지 않았다. 후보·복구 목록을 시작 때 읽되 실제 시험을 자동 시작하지 않는다.
- `PcOptimizer.App.csproj`, `tools/README-in-zip.txt`: 0.3.0-preview.2 및 평가 실행 방법/범위 명시. UI 디자인 개선이나 문구 전면 개편은 하지 않았다.

## 자체 검증

실제 사용자 화면 전환, 프로필 쓰기, 개인 캐시 삭제, 키보드 입력은 하지 않았다. 아래 단위 테스트의 설정 변경은 대역이고 WPF 창은 화면에 표시하지 않은 오프스크린 검사다.

- `dotnet build PcOptimizer.sln -c Release --no-restore`: 최종 **경고 0 / 오류 0**.
- `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp1-display-basic-final.trx'`: **1449/1449**. 기존 1408에서 신규 41건. 지원 거절/출력 재식별/주사율 bytes/최종 쓰기 경계, 유지/15초/클릭 경합/느린 적용/취소, 드라이버 실패·재시작 코드, 원복·기록 실패, 미해결 복구/새 조율기 복구, 손상·타 SID, 실제 Dispatcher/닫기 관문 등을 검증했다.
- `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter 'Category=Smoke' --logger 'trx;LogFileName=sp1-display-smoke-final.trx'` (관리자): **42/42**. 기존 41건 + 조회/CDS_TEST 1건. 이후 마지막 수정은 테스트용 대화형 환경 공급자 추가(기본값은 기존 Environment.UserInteractive 유지)이며 최종 전체 기본/해당 네이티브 Smoke를 다시 수행했다.
- 최종 `--filter FullyQualifiedName~DisplayTrialSmokeTests --logger 'trx;LogFileName=sp1-display-readonly-final.trx'` (관리자): **1/1**. 후보 15개, 현재 240Hz / 첫 후보 48Hz의 **CDS_TEST=0**. 전후 활성/프로필 모드가 같고 **Apply/SaveProfileOnly 미호출**. 실제 48Hz 전환을 했다는 의미가 아니다.
- `--filter FullyQualifiedName~DisplayTrialWindowTests --logger 'trx;LogFileName=sp1-display-window-final.trx'`: **1/1**. 실제 WPF Dispatcher의 UI 귀속, 실행 버튼 바인딩/비기본 버튼, 창 닫기 차단·원복 후 닫기. `artifacts/sp1-display-ui/display-trial-preview.png` 시각 확인. 화면 배율 전반이나 실사용성 평가를 대신하지 않는다.
- Online/ToolSmoke는 해당 수집·공식 도구 실행 코드를 변경하지 않아 이번 재실행하지 않았다. 직전 완료 이력과 혼동하지 않는다.

### 초기 실패와 처리

- 신규 테스트 첫 빌드에서 `System.IO` 누락과 지역 변수 이름 충돌 각 1건: 소스 수정 후 전체 빌드 통과.
- 샌드박스 CDS_TEST는 -1을 반환했다(`sp1-display-readonly.trx`). 동일 바이너리·동일 후보를 관리자 환경에서 재실행하면 0(`sp1-display-readonly-admin.trx`), 최종 코드도 0. 화면 접근 실행 환경 차이에 민감하므로 실패를 강제로 성공 처리하거나 관문을 완화하지 않았다.
- 첫 오프스크린 이미지는 Window 배경 밖 Content의 투명 영역이 검게 보였다. 루트 배경을 명시한 뒤 재렌더·검사했다.

## 배포·실기 제한

- `tools/package.ps1 -OutputDirectory dist/sp1-evaluation`: locked restore/publish 성공. `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.2-win-x64.zip`, 최상위 4개/내부 13파일/실행 파일 65,146,717 bytes. 라이선스·규칙 원문·해시 검증 통과. SHA-256 `9E8AAA4E100B05283E8A824053C624DC989E1C0A54A993FCC61951BD03E93134`. preview.1 ZIP 보존. 앱 실실행/화면 전환/.NET 없는 PC에서 실행은 하지 않았다.
- 평가용으로만 사용한다. 압축 폴더 PowerShell: `Start-Process -FilePath .\PcOptimizer.exe -ArgumentList '--display-evaluation' -Verb RunAs`.
- 실제 화면 전환/가독성/유지/시간 초과 복원·분리·재연결·프로필 유지·재부팅 복구는 별도 평가 PC에서 미실시. Task 8 전체/정식 기본 활성화 완료로 표시하지 않는다.
- LUID·출력 경로가 달라진 재부팅/재연결은 보수적으로 거절할 수 있다. 그 경우 Windows 디스플레이 설정으로 복구한다. OS/드라이버가 네이티브 호출에서 멈추거나 단일 프로세스 강제 종료/PC 전원 차단 중에는 15초 원복을 보장하지 않는다. 별도 watchdog을 추가하지 않았다.
- 일반 실행에서는 표시 기록이 공통 기록 목록에 미지원으로 보일 수 있다. 원복 기능은 같은 평가 인자로 다시 실행하여 평가 창에서 사용한다.
- 기본 자동 기능은 사용자/Windows Temp·Explorer 캐시·공식 npm/pip/NuGet HTTP·전원 계획으로 유지된다. T6 시작 앱, T9 Update/DO, T10 F2 및 실제 계정/기기 평가는 여전히 남았다. 다음 구현에서도 UI 다듬기보다 기능과 플랫폼 계약 검증을 우선한다.

API 근거: [Microsoft ChangeDisplaySettingsExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw). CDS_TEST는 적용하지 않는 시험, UPDATEREGISTRY는 사용자 프로필 저장, NORESET은 프로필만 쓰는 동작이다. 성공 반환과 사용자가 화면을 볼 수 있는지는 별개다.
