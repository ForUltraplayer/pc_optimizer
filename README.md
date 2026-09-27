# PC Optimizer — Windows 11 x64 평가 빌드

기존 Windows·제조사·패키지 도구를 한 화면에서 연결하는 진단 앱입니다. LLM은 필요하지 않습니다. 진단은 조회 전용이며, 정리는 별도 화면에서 대상 확인과 사용자 확인 후에만 실행합니다.

preview.13에서는 제목 표시줄·왼쪽 메뉴를 짙은 남색으로 구분하고 본문 글자·카드 경계·스크롤바 대비를 높였습니다. 내부 목록 위에서도 휠로 스크롤할 수 있고, 목록 끝에서는 바깥 본문으로 이어집니다. 탭마다 보던 위치를 앱 실행 중 기억하며, 새 조치 확인·결과가 생길 때만 상단으로 이동합니다.

## 실행

GitHub Release의 `PcOptimizer-v<버전>-win-x64.zip`을 받아 압축을 풀고 **`PcOptimizer.exe`** 하나만 실행합니다(설치 없음, .NET 런타임 포함 단일 실행 파일). 관리자 권한 확인창에서 "예"를 누릅니다. 삭제는 폴더를 지우면 되고, 설정·로그는 `%LocalAppData%\PcOptimizer`에 있습니다. zip 최상위에는 `PcOptimizer.exe`, `실행방법.txt`, `LICENSES/`(라이선스 고지), `rules/`(규칙 원문 `winapp2.ini`·`supplement.ini`·`rule-metadata.json`과 출처 `sources.json`, 실행 시 읽지 않음)만 있습니다.

1. **검사 시작**: 하드웨어·설정·저장소·앱 캐시를 조회합니다. 확인 불가 이유와 관측 범위를 함께 표시합니다.
2. **온라인 업데이트 확인**: 선택한 검사에서만 NVIDIA·Windows Update를 조회합니다. 설치는 하지 않습니다.
3. **개선할 작업 선택 · 결과 · 되돌리기**: 사용자·Windows 임시 파일, 탐색기 캐시, Adobe 기본 미디어·파형 캐시, 설치된 npm·pip·NuGet HTTP 도구, 전원 계획 중 하나를 선택합니다. **대상 확인 → 별도 실행 확인** 후에만 조치합니다. 조치 결과와 재검사 상태를 같은 창에서 확인할 수 있습니다.
4. **주사율 개선 후보의 NHz 시험 적용**: 대상과 주사율을 새로 확인한 뒤 시험합니다. 15초 안에 유지하지 않으면 원복을 시도하며, 앱·드라이버가 멈추면 직접 복구가 필요할 수 있습니다.

preview.4의 **Adobe 기본 미디어 캐시 정리**는 현재 사용자 기본 `Roaming\Adobe\Common\Media Cache Files`의 `.cfa`·`.pek`, **기본 파형 캐시 정리**는 `Peak Files`의 `.pek` 중 생성·수정 후 모두 90일이 지난 파일만 대상으로 합니다. Adobe 결과 카드에서도 기본 미디어 캐시 확인을 열 수 있습니다. 앱 실행 중/조회 실패는 거절하고, 미리보기 후 생긴 파일·사용 중인 파일·보호 대상은 지우지 않습니다. 캐시 데이터베이스, 원본 미디어·프로젝트·렌더 결과물, 옮긴 캐시 위치는 포함하지 않습니다. 이후 오디오 변환·파형을 다시 만드는 시간이 필요할 수 있습니다. 90일 기준은 미사용을 입증하는 기준이 아니며 정리 중에는 관련 앱을 열지 마세요.

첫 화면의 **추천 조치**에서 기대 효과·현재 상태·할 일·주의사항을 확인합니다. 정상·참고·확인 불가와 커뮤니티 관측은 **전체 결과**에서, 원시 측정값·출처는 카드의 **자세히 보기**에서 확인합니다. 내보내기는 창 하단의 **검사 옵션**에 있습니다. 앱은 항상 관리자 권한으로 실행됩니다(UAC 프롬프트). 표준 계정에서 다른 관리자 계정으로 승격해 실행하면 시스템 범위만 검사하고 이 계정의 항목(시작 프로그램·임시 파일·앱 캐시)은 검사·정리하지 않으며, 관리자 계정으로 로그인해서 실행하면 전부 검사할 수 있습니다. 캐시 관측 크기를 확보 용량이나 개선 후보 수에 합산하지 않습니다.

**설정 후보 보기 / 드라이버 결과 보기**는 해당 결과로 바로 이동합니다. 온라인 조회를 생략한 드라이버는 **온라인 비교 전**으로 표시합니다. 캐시 정리 후에는 실행 직전·직후의 논리 크기와 결과를 비교하며, 최근 정리 결과는 창을 닫고 재검사한 뒤에도 메인 화면에 남습니다. 실제 디스크 확보 용량을 측정한 값은 아닙니다.

**내 PC 사양**은 CPU·메인보드·GPU·RAM·SSD·HDD·모니터를 한 열로 나열해 보여줍니다. 기본은 PC 이름·사용자명·일련번호 등 식별 정보를 뺀 익명화 상태이며, 필요할 때만 식별 정보 포함 토글을 켭니다. 화면에서 텍스트 복사, TXT(UTF-8, BOM 없음) 저장, PNG(96 DPI) 저장을 지원합니다.

자동 정리는 Program Files(`%ProgramFiles%`·`%ProgramFiles(x86)%`·`%ProgramW6432%`) 같은 보호 위치에 설치된 도구만 실행합니다. 사용자 폴더(nvm·사용자별 Python 등)에 설치된 도구는 관리자 권한 앱이 실행하지 않고, 터미널에서 직접 실행할 공식 명령을 안내합니다. 다른 관리자 계정으로 승격해 실행한 경우에는 캐시 정리를 열지 않습니다. 현재 사용자 프로필 밖으로 옮긴 캐시, 보호/동기화 경로, 링크/placeholder, 불완전하게 읽힌 대상은 자동 정리하지 않습니다. 해당 도구가 설치돼 있어야 하며 자동으로 설치하지 않습니다. pip은 PATH에서 찾은 보호 위치 Python의 격리 모드에서 pip 모듈을 쓸 수 있어야 합니다. 캐시 정리는 되돌릴 수 없으며 이후 설치 때 재다운로드가 필요합니다. 패키지 설치·빌드 중에는 실행하지 마세요.

Squirrel 버전 폴더, NuGet 전역 패키지, 드라이버 설치, 보안 설정 변경은 자동 조치 대상이 아닙니다.

## 개발·검증

.NET SDK 10.0.401을 사용합니다.

```powershell
dotnet build -c Release
dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"
```

- `Category=Smoke`: 실제 PC 조회 및 소유한 임시 캐시/링크 fixture 검사. WMI·링크 생성 권한과 npm/dotnet 설치가 필요합니다.
- `Category=Online`: 실제 NVIDIA·WUA 검색, 설치 없음.
- `Category=ToolSmoke`: `PCOPTIMIZER_TEST_PYTHON`에 Python 실행 파일을 명시한 pip fixture 정리 검증. 제품의 기본 검사에는 사용하지 않습니다.
- `PCOPTIMIZER_UI_ARTIFACTS`: 선택적으로 렌더링 테스트 PNG 저장 폴더를 지정합니다.

## 배포

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/package.ps1
```

`dist/PcOptimizer-v<Version>-win-x64.zip`이 만들어지면 GitHub Release에 첨부합니다. 사용자는 zip을 풀고 `PcOptimizer.exe`를 실행합니다. 버전은 `src/PcOptimizer.App/PcOptimizer.App.csproj`의 `<Version>`에서 읽습니다. App 프로젝트는 단일 파일·self-contained(win-x64) publish 속성을 갖고 있으며, 중간 publish 폴더는 `dist/publish`입니다(`dist/`는 git에 넣지 않음).

아이콘은 `src/PcOptimizer.App/Assets/app.ico`입니다. 외부 자산 없이 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/make-icon.ps1 -Output src/PcOptimizer.App/Assets/app.ico`로 다시 만들 수 있고(같은 결과), 디자이너 아이콘으로 바꿀 때는 이 파일만 교체합니다. 서드파티 고지는 `THIRD-PARTY-NOTICES.md`입니다.

[공유 리뷰 원장](docs/reviews/REVIEW_LEDGER.md), [현재 검증·제한](docs/reviews/2026-09-27-validation.md), [자동 조치 계약과 공식 명령 근거](docs/superpowers/specs/2026-09-27-first-release-actions.md)를 읽고 이어서 작업하세요.

아직 평가 빌드입니다. 실제 일반 권한 UI/UAC 흐름, 모니터 DPI 전환, 별도 .NET 미설치 PC와 모든 도구 버전 조합의 검증은 남아 있습니다. rules의 읽을 수 있는 사본은 출처 확인용이며 앱은 어셈블리에 포함된 정책만 사용합니다. Winapp2의 저작자 표시·CC BY-SA 조건은 `rules/LICENSE-winapp2.md`(배포 zip의 `LICENSES/winapp2-CC-BY-SA-4.0.md`)를 따릅니다.
