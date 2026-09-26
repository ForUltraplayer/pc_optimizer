# PC Optimizer — Windows 11 x64 평가 빌드

기존 Windows·제조사·패키지 도구를 한 화면에서 연결하는 진단 앱입니다. LLM은 필요하지 않습니다. 진단은 조회 전용이며, 정리는 별도 화면에서 대상 확인과 사용자 확인 후에만 실행합니다.

## 실행

`artifacts/win-x64/PcOptimizer.App.exe`를 실행합니다. 배포 폴더 전체가 필요하며 .NET 런타임이 포함됩니다.

1. **검사 시작**: 하드웨어·설정·저장소·앱 캐시를 조회합니다. 확인 불가 이유와 관측 범위를 함께 표시합니다.
2. **온라인 업데이트 확인**: 선택한 검사에서만 NVIDIA·Windows Update를 조회합니다. 설치는 하지 않습니다.
3. **도구로 캐시 정리**: npm·pip·NuGet HTTP 중 하나를 선택하고 **정리 대상 확인 → 확인 후 정리**를 누릅니다. Windows 임시 파일은 저장소 설정으로 연결합니다.
4. 공식 도구 실행 후 같은 위치를 다시 관측하고, 정리 창을 닫으면 전체 진단을 다시 실행합니다.

자동 정리는 일반 권한에서만 지원합니다. 현재 사용자 프로필 밖으로 옮긴 캐시, 보호/동기화 경로, 링크/placeholder, 불완전하게 읽힌 대상은 자동 정리하지 않습니다. 해당 도구가 설치돼 있어야 하며 자동으로 설치하지 않습니다. pip은 PATH에서 찾은 Python의 격리 모드에서 pip 모듈을 쓸 수 있어야 합니다. 캐시 정리는 되돌릴 수 없으며 이후 설치 때 재다운로드가 필요합니다. 패키지 설치·빌드 중에는 실행하지 마세요.

Squirrel 버전 폴더, NuGet 전역 패키지, 드라이버 설치, 보안 설정 변경은 자동 조치 대상이 아닙니다.

## 개발·검증

.NET SDK 10.0.401을 사용합니다.

```powershell
dotnet build -c Release
dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke"
dotnet publish src/PcOptimizer.App/PcOptimizer.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64
```

- `Category=Smoke`: 실제 PC 조회 및 소유한 임시 캐시/링크 fixture 검사. WMI·링크 생성 권한과 npm/dotnet 설치가 필요합니다.
- `Category=Online`: 실제 NVIDIA·WUA 검색, 설치 없음.
- `Category=ToolSmoke`: `PCOPTIMIZER_TEST_PYTHON`에 Python 실행 파일을 명시한 pip fixture 정리 검증. 제품의 기본 검사에는 사용하지 않습니다.
- `PCOPTIMIZER_UI_ARTIFACTS`: 선택적으로 렌더링 테스트 PNG 저장 폴더를 지정합니다.

[공유 리뷰 원장](docs/reviews/REVIEW_LEDGER.md), [현재 검증·제한](docs/reviews/2026-09-27-validation.md), [자동 조치 계약과 공식 명령 근거](docs/superpowers/specs/2026-09-27-first-release-actions.md)를 읽고 이어서 작업하세요.

아직 평가 빌드입니다. 실제 일반 권한 UI/UAC 흐름, 모니터 DPI 전환, 별도 .NET 미설치 PC와 모든 도구 버전 조합의 검증은 남아 있습니다. rules의 읽을 수 있는 사본은 출처 확인용이며 앱은 어셈블리에 포함된 정책만 사용합니다. Winapp2의 저작자 표시·CC BY-SA 조건은 배포물 `rules/LICENSE-winapp2.md`를 따릅니다.
