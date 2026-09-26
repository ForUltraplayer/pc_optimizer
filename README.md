# PC Optimizer — Windows 11 x64 평가 빌드

기존 Windows·제조사·패키지 도구를 한 화면에서 연결하는 진단 앱입니다. LLM은 필요하지 않습니다. 진단은 조회 전용이며, 정리는 별도 화면에서 대상 확인과 사용자 확인 후에만 실행합니다.

## 실행

최신 UI 평가판은 `artifacts/win-x64-ui/PcOptimizer.App.exe`를 실행합니다. 기존 `artifacts/win-x64`는 사용 중인 이전 UI 빌드입니다. 배포 폴더 전체가 필요하며 .NET 런타임이 포함됩니다.

1. **검사 시작**: 하드웨어·설정·저장소·앱 캐시를 조회합니다. 확인 불가 이유와 관측 범위를 함께 표시합니다.
2. **온라인 업데이트 확인**: 선택한 검사에서만 NVIDIA·Windows Update를 조회합니다. 설치는 하지 않습니다.
3. **공간 확보 → 정리할 캐시 확인**: npm·pip·NuGet HTTP 중 하나를 선택하고 **정리 대상 확인 → 확인 후 정리**를 누릅니다. Windows 임시 파일은 저장소 설정으로 연결합니다.
4. 공식 도구 실행 후 같은 위치를 다시 관측하고, 정리 창을 닫으면 전체 진단을 다시 실행합니다.

첫 화면의 **추천 조치**에서 기대 효과·현재 상태·할 일·주의사항을 확인합니다. 정상·참고·확인 불가와 커뮤니티 관측은 **전체 결과**에서, 원시 측정값·출처는 카드의 **자세히 보기**에서 확인합니다. 내보내기는 창 하단의 **검사 옵션**에 있습니다. 앱은 항상 관리자 권한으로 실행됩니다. 표준 계정에서 다른 관리자 계정으로 승격해 실행하면 시스템 범위만 검사하고 이 계정의 항목(시작 프로그램·임시 파일·앱 캐시)은 검사·정리하지 않으며, 관리자 계정으로 로그인해서 실행하면 전부 검사할 수 있습니다. 캐시 관측 크기를 확보 용량이나 개선 후보 수에 합산하지 않습니다.

**설정 후보 보기 / 드라이버 결과 보기**는 해당 결과로 바로 이동합니다. 온라인 조회를 생략한 드라이버는 **온라인 비교 전**으로 표시합니다. 캐시 정리 후에는 실행 직전·직후의 논리 크기와 결과를 비교하며, 최근 정리 결과는 창을 닫고 재검사한 뒤에도 메인 화면에 남습니다. 실제 디스크 확보 용량을 측정한 값은 아닙니다.

자동 정리는 Program Files(`%ProgramFiles%`·`%ProgramFiles(x86)%`·`%ProgramW6432%`) 같은 보호 위치에 설치된 도구만 실행합니다. 사용자 폴더(nvm·사용자별 Python 등)에 설치된 도구는 관리자 권한 앱이 실행하지 않고, 터미널에서 직접 실행할 공식 명령을 안내합니다. 다른 관리자 계정으로 승격해 실행한 경우에는 캐시 정리를 열지 않습니다. 현재 사용자 프로필 밖으로 옮긴 캐시, 보호/동기화 경로, 링크/placeholder, 불완전하게 읽힌 대상은 자동 정리하지 않습니다. 해당 도구가 설치돼 있어야 하며 자동으로 설치하지 않습니다. pip은 PATH에서 찾은 보호 위치 Python의 격리 모드에서 pip 모듈을 쓸 수 있어야 합니다. 캐시 정리는 되돌릴 수 없으며 이후 설치 때 재다운로드가 필요합니다. 패키지 설치·빌드 중에는 실행하지 마세요.

Squirrel 버전 폴더, NuGet 전역 패키지, 드라이버 설치, 보안 설정 변경은 자동 조치 대상이 아닙니다.

## 개발·검증

.NET SDK 10.0.401을 사용합니다.

```powershell
dotnet build -c Release
dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"
dotnet publish src/PcOptimizer.App/PcOptimizer.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64-ui
```

- `Category=Smoke`: 실제 PC 조회 및 소유한 임시 캐시/링크 fixture 검사. WMI·링크 생성 권한과 npm/dotnet 설치가 필요합니다.
- `Category=Online`: 실제 NVIDIA·WUA 검색, 설치 없음.
- `Category=ToolSmoke`: `PCOPTIMIZER_TEST_PYTHON`에 Python 실행 파일을 명시한 pip fixture 정리 검증. 제품의 기본 검사에는 사용하지 않습니다.
- `PCOPTIMIZER_UI_ARTIFACTS`: 선택적으로 렌더링 테스트 PNG 저장 폴더를 지정합니다.

[공유 리뷰 원장](docs/reviews/REVIEW_LEDGER.md), [현재 검증·제한](docs/reviews/2026-09-27-validation.md), [자동 조치 계약과 공식 명령 근거](docs/superpowers/specs/2026-09-27-first-release-actions.md)를 읽고 이어서 작업하세요.

아직 평가 빌드입니다. 실제 일반 권한 UI/UAC 흐름, 모니터 DPI 전환, 별도 .NET 미설치 PC와 모든 도구 버전 조합의 검증은 남아 있습니다. rules의 읽을 수 있는 사본은 출처 확인용이며 앱은 어셈블리에 포함된 정책만 사용합니다. Winapp2의 저작자 표시·CC BY-SA 조건은 배포물 `rules/LICENSE-winapp2.md`를 따릅니다.
