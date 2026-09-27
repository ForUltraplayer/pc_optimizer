# SP1 Task 1 구현 및 자체 검증

코드 커밋 `11959e0`, 기준 `57440ac`, 브랜치 `codex/sp1-actions`. 구현자 Codex의 자체 검증이다. 독립 리뷰 승인은 대기이며 실제 조치를 사용하는 SP1 전체 완료가 아니다.

## 변경과 근거

| 범위 | 변경 파일 | 대응 |
|---|---|---|
| 링크 위임 | `DesktopShellConnection`, `UnelevatedShellLauncher`, `LinkPolicy`, `FindingCardViewModel` | 기존 데스크톱 창/PID/세션/비승격 토큰 확인 후 COM 위임. 셸 없음·승격·조회 실패·COM 실패는 주소 복사 안내. Explorer 새 프로세스 폴백 제거, 쉼표 URL 거절, 실패 주소는 AbsoluteUri |
| 도구별 실행 가능 표시 | `CacheToolActionAvailability`, `IActionAvailability`, `SystemCacheToolBackend`, `App.xaml.cs`, `MainViewModel` | npm/pip/NuGet 별로 설치 위치를 확인·보관, 생성/검사 시작 때 갱신. dotnet만 있어도 npm을 바로 실행 가능으로 세던 문제 수정. SystemOnly에서는 사용자 도구 조회도 생략 |
| UI/개인정보 | `MainViewModel.Overview`, `Strings.resx`, `CoreStrings.resx`, `PcSpecSnapshot`, `PcSpecService.Sections`, `PcSpecTextFormatter` | 연결이 끊긴 예전 명령 제거, 종료 대기/범위 문구 수정. 사용자 지정 볼륨 이름은 식별 정보 포함 선택 때만 화면/사양 내보내기에 표시, 용량/파일 시스템 유지 |
| 빌드/배포 | `Directory.Build.props`, App csproj/profile, Tests lock, `tools/package.ps1`, 두 검증 스크립트 | 게시 속성 분리, Debug PDB 보존, 공통 RID 그래프 고정. 실제 ZIP 원문/경로/해시 검증, 런타임·WPF-UI 원문 라이선스 추가, 미서명/SmartScreen 안내 |
| 플랫폼 계약·이월 | [기술 근거 노트](sp1-platform-contracts.md) | 시작 항목·Update/DO·주사율의 문서상 보장과 평가 조건 분리. T2/T3/T6/T9/T10/T12 책임 명시 |

## 검증

실행 도구는 `C:\Program Files\dotnet\dotnet.exe`, SDK 10.0.401. 의존성 버전은 변경하지 않았다.

| 명령/조건 | 결과 |
|---|---|
| `restore PcOptimizer.sln --locked-mode`, Core/Probes/App/Tests csproj 각각 `restore --locked-mode` | 모두 통과 |
| `build PcOptimizer.sln -c Debug --no-restore` / `-c Release` | 각각 경고 0, 오류 0 |
| Debug `src/PcOptimizer.App/bin/Debug/net10.0-windows/PcOptimizer.pdb` | 파일 존재, 145904 bytes |
| `test tests/PcOptimizer.Tests -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke'` | 최종 **1288/1288** |
| 같은 프로젝트 `--no-build --no-restore --filter 'Category=Smoke'` | **23/23** (69초) |
| `tools/package.ps1 -OutputDirectory dist/sp1-evaluation` | 최종 publish 성공, ZIP 최상위 4개, 파일 13개, EXE 65054556 bytes |
| `tools/verify-package.ps1 -ZipPath dist/sp1-evaluation/PcOptimizer-v0.2.0-win-x64.zip` | 전체 스트림 읽기, PE 서명, 필수 파일, 규칙/설명/라이선스 원문 SHA-256, ZIP SHA-256 통과 |
| `tools/test-package-verifier.ps1 -ZipPath ...` | 정상 ZIP 통과 및 해시 불일치·고지 누락·규칙 내용 변조·중복 엔트리 **4종 거절** |
| `git diff --check` | 통과 |

최종 ZIP SHA-256: `4EE550C46D099BB5FE8C8764F852992183951EE7EC3558D37F9836FE91A3F13D`. 평가 산출물이며 버전은 기존 v0.2.0을 유지했다. 공개 릴리스나 원격 업로드는 하지 않았다.

TRX는 git-ignored `tests/PcOptimizer.Tests/TestResults/sp1-t1-final-unit.trx`, `sp1-t1-final-smoke.trx`. Smoke 이후 추가한 테스트는 실패 안내의 대문자 호스트/한글 경로 정규화 입력 1건이며 네이티브 Smoke 코드는 변경하지 않았다. Online/ToolSmoke는 미실행. 사용자 캐시 정리·설정 변경·브라우저 열기는 하지 않았다.

## 발견·수정 과정과 한계

- 첫 단위 실행은 도구 위치 조회 횟수(기존 1회 가정 → 생성/검사 때 각 3도구 총 6회) 단언이 실패해 계약에 맞게 수정했다. 첫 전체 실행은 바뀐 캐시 안내 문구의 기존 문자열 단언 1건이 실패해 수정했다. 이 실패는 제품 결함 RED로 주장하지 않는다.
- 네이티브 셸 Smoke의 최초 `NotNull` 단언은 실패했다. 별도 토큰 조회 결과 현재 Explorer가 TokenElevation=1이었다. 테스트를 환경별로 연결/거절 검증하도록 수정했고 최종 TRX에는 `NoNormalDesktop: refusal verified; normal-desktop COM success remains untested on this host.`를 남겼다. 안전 가드를 해제하지 않았다.
- 링크 단위 회귀는 정상 연결·해제, 셸 없음/승격/조회 오류/COM 종료, 쉼표와 `%2C`, 허용되지 않은 주소의 연결 전 거절, 정규화한 실패 안내를 확인한다. **실제 비승격 셸 COM 연결 성공·브라우저 토큰은 T12에서 평가 필요**.
- 볼륨 이름 회귀는 관측 결과→스냅샷→화면과 내보내기가 공통으로 사용하는 VisibleItems 경로를 확인한다. 실제 클립보드 입력/키보드 자동화는 하지 않았다.
- 기존 REV-010·014~018 완료 범위는 유지했다. 서비스 직접 호출 상호 배제(REV-017 잔여)는 T2다. REV-002/003/006/007의 이전 독립 검증 대기를 이번 기본 테스트 결과만으로 닫지 않는다.
- 구현과 테스트가 같은 세션의 결과다. 독립 검토자는 `57440ac..11959e0`과 이 기록, 공유 원장을 읽고 대응 결과를 별도로 남겨야 한다.
