# T10-B Steam 선택 정리·그래픽 공식 기능 연결

기준 `9687ff4`. 사용자 순서 **구현 → UI/UX → 검증**을 유지한다. 독립 리뷰·에이전트·새 프리뷰 없음. 아래는 구현 보고이며 실행 검증/출시 승인이 아니다.

## 구현

- `SteamShaderCache` 현재 사용자 조치, 세션 한정 `SteamCacheLocationCatalog`, `FileCleanupAdapter.ForSteamCache` 추가. 조치 창에서 라이브러리별 `steamapps\shadercache`를 명시적으로 선택하면 공통 워커에서 Steam 레지스트리 단일 값·최대 1 MiB `libraryfolders.vdf`와 재대조한다. 실행 시에도 동일 경로와 현재 설정을 재확인한다. 임의 폴더 이름만으로 정리를 허용하지 않는다. UI 등록은 문자열 처리이며 디스크 접근은 준비 워커로 넘긴다.
- 기존 사용자 확인/등록 프로필/다른 사용자/보호·정규화/링크·hardlink/원본 파일 ID/메타데이터/완전 열거/독점 파일 핸들 엔진을 재사용한다. 고정 로컬 드라이브만 허용하며 Program Files의 삭제 보호는 풀지 않는다. 설정 파일 읽기에서만 기존 검사와 같은 출처별 Program Files 예외를 적용하고 겹친 KnownFolder/CloudSync 보호는 유지한다.
- 정리 범위: 숫자 AppID 하위, 생성·수정 시각이 모두 30일 이전인 일반 파일. 루트 파일·이름을 해석 못한 폴더·최근 파일 제외. 폴더 자체·게임 설치·워크숍·Steam 다운로드 캐시는 제외. 현재 설정에 없는 라이브러리는 거절. 최대 16개 명시 선택, 앱 종료 시 선택 해제.
- `SteamProcessGuard`: 로컬 WMI에서 이름·PID·세션·실행 파일 경로만 읽고 명령줄/창 제목/계정은 읽지 않는다. Steam/SteamCMD/서비스/오버레이/셰이더 작성자 이름 또는 등록 라이브러리 하위의 실행 파일이면 거절. 대화형 세션의 경로 미확인·비정상 형식·5초/16,384개 초과·조회 실패도 거절한다. 경로는 판정 후 버리며 로그·리포트에 넣지 않는다. 프로세스 종료나 설정 변경은 하지 않는다.
- 준비 앞뒤/실행 앞/각 파일 삭제 직전에 프로세스 상태를 다시 확인한다. 새 활동이 보이면 추가 삭제 중단, 기존 엔진이 처리 수를 보존한다. 부분 결과·원복 불가·공유 라이브러리의 다른 사용자 영향·재다운로드/컴파일 비용을 표시한다. 기존 조율기가 실제 워커 반환까지 관문을 유지한다.
- NVIDIA 검사에 현재 사용자 `NVIDIA Corporation\NV_Cache`를 추가(기존 DXCache/GLCache/D3DSCache 인덱스 유지). 별도 경로 관측·누락/부분 결과/익명화 계약을 그대로 사용한다.
- `CacheSupportLinks`: Steam 다운로드 캐시와 NVIDIA 셰이더 정리의 정확한 공식 URL 2개만 코드 허용. 기존 `LinkPolicy` 엄격한 HTTPS 파싱과 비승격 셸 위임 사용. 임의 호스트/쿼리/하위 경로를 허용 목록에 추가하지 않는다. 조치 창과 결과 카드에 연결하고 웹 페이지를 연 것을 정리 완료로 기록하지 않는다. Direct3D는 기존 Windows 저장소 설정으로 연결한다.

## 그래픽 자동 삭제를 활성화하지 않은 이유

[NVIDIA 공식 문서](https://nvidia.custhelp.com/app/answers/detail/a_id/5735/)는 캐시 비활성화, 재부팅, 정리, 원래 설정 복원을 포함한다. 현재 앱은 해당 설정/재부팅/그래픽 작성자 유휴를 검증하지 못하므로 DX/GL/NV_Cache를 일반 파일 삭제에 등록하지 않았다. [NVIDIA 캐시 설명](https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-gb/mergedProjects/nv3dENG/Manage_3D_Settings_%28reference%29.htm)도 재사용으로 컴파일을 줄이는 목적을 설명한다. 공간이 크다는 이유만으로 성능 개선 후보로 올리지 않는다. 기존 계획의 ‘idle 확인 불가 공급자는 공식 기능 연결’ 원칙을 적용하며 전체 T10 완료로 표시하지 않는다.

[Steam 공식 다운로드 캐시 안내](https://help.steampowered.com/en/faqs/view/6AD7-820D-8BE5-E51F)는 이번 셰이더 캐시와 다른 대상이다. 링크 이름/설명에서 구분하고 재로그인 가능성을 안내한다. Steam 직접 파일 정리를 Valve가 제공하는 공식 자동화 API라고 표현하지 않는다.

## 한계와 후속 검증

- [Win32_Process](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process)의 ExecutablePath 조회는 권한 조건이 있다. WMI EnablePrivileges를 사용하지만 실제 Windows 버전/보호 프로세스에서 경로 미제공으로 보수적 거절이 빈번할 수 있다. 실제 PC 조회는 아직 하지 않았다.
- 프로세스 조회는 전역 작성자 잠금이 아니다. 시스템 세션의 경로 미제공 프로세스는 알려진 작성자 이름만 확인한다. 등록 라이브러리 밖에서 실행하는 사용자 지정 게임/작성자, 경로 별칭, 조회 직후 새 실행의 한계가 있다. 고정 파일 집합과 삭제 시 독점 핸들이 이를 줄이는 조건이며 모든 앱의 유휴를 증명하지 않는다. 강제 종료/권한 완화로 우회하지 않는다.
- 공통 준비 15초/20,000항목 및 실행 제한 때문에 큰 캐시는 거절/부분 처리될 수 있다. 매 파일 WMI 재조회 비용/시간 초과는 T12 측정 대상이다. 사용자 선택형 정리이며 미사용 판정은 아니다.
- 첫 컴파일: `SteamCacheLocationCatalog`의 부모 경로 null 가능성 CS8602 1건. `string.Equals`로 수정 후 최종 `dotnet build src/PcOptimizer.App/PcOptimizer.App.csproj -c Release --no-restore` **경고 0/오류 0**.
- 테스트 작성/실행·Smoke·실기 조회/삭제·Steam/게임 시작/종료·브라우저 실행은 하지 않았다. 버전/ZIP 변경 없음. 기존 preview.6에는 이번 코드가 없다.
- T12 회귀: Full/SystemOnly/SID 변경, 등록 목록 변경·손상/설정 파일 링크·다른 사용자/중복 보호·Program Files 정리 거절, AppID/루트 숫자 파일/새 파일/최근 파일, 프로세스 WMI 불완전/시간 초과/서비스/다른 세션 게임/중간 실행·취소, 부분 삭제 수, 링크 정확 일치·실패·비승격, 4번째 그래픽 행/기존 3행 없는 fixtures, UI 선택/거절/재검사. VM/전용 fixture 외 사용자 캐시를 개발 시험으로 지우지 않는다.

신규 주요 파일은 `SteamCacheLocationCatalog.cs`, `SteamProcessGuard.cs`, `CacheSupportLinks.cs`; 기존 App/ActionCenter/ActionPresentation/XAML/LinkPolicy/ActionCoordinator/ActionModels/FileCleanupAdapter/ShaderCacheInspector/ShaderCacheRule에 연결했다. 다음 구현은 T6 시작 폴더 원본 보존/복원 계약과 앱별 설정 자동 탐지 확장. T11 전면 UX와 T12 종합 검증은 후속이다.
