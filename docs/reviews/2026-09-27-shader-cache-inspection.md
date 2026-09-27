# Steam·NVIDIA/Direct3D 위치별 캐시 검사 / preview.6

구현·테스트·배포 기록 커밋 `40ae676` (23파일). 후속 문서 커밋은 인계 참조 기록이다.

기준 `9bafe82`, 브랜치 `codex/sp1-actions`. 사용자 기능 우선·계속 진행 지시에 따라 T10의 라이브러리/위치별 관측을 보강했다. 독립 리뷰 없이 구현자 자체 검증을 수행했다. 자동 삭제·T10 전체 완료와 구분한다.

## 변경

- `ShaderCacheInspector.cs`: 현재 사용자 Steam 설치 경로 값 한 개와 `steamapps/libraryfolders.vdf`만 읽어 최대 65개 라이브러리의 `steamapps/shadercache`를 각각 관측한다. 설정 파일 최대 1 MiB, 중복 정규화 경로 제거, 위치당 3초와 전체 프로브 예산/취소를 적용한다. 게임 본체·워크숍·다운로드 폴더·계정 정보·앱 매니페스트 내용은 읽지 않는다.
- `SteamLibraryReader`: 본문 읽기 전 보호 콜백을 추가했다. 읽기 실패를 미설치/HKLM 경로로 대체하지 않고 잘못된 VDF 경로 하나를 조용히 버리지 않는다. 이 두 실패는 Unreadable/Invalid로 보고하며 존재하는 기본 캐시로 대체하지 않는다. 파일이 실제로 없으면 설치 루트의 shadercache 한 곳만 후보로 남는다.
- 승격 중에도 새 전용 inspector의 작은 Steam 설정 읽기는 허용한다. 일반 보충 규칙의 승격 설정 미적용은 그대로다. **전용 Steam 읽기에만** `%ProgramFiles%`/`%ProgramFiles(x86)%` 출처·실제 해석 경로가 모두 일치하는 보호 항목을 예외로 둔다. 기존 공통 scanner/삭제 실행기/커뮤니티 규칙의 보호는 변경하지 않는다. 같은 경로의 Documents/CloudSync/다른 SystemPath 출처가 겹치면 계속 차단한다.
- 그래픽 캐시는 현재 LocalAppData 아래 `NVIDIA/DXCache`, `NVIDIA/GLCache`, `D3DSCache` 세 위치를 분리한다. NVIDIA 프로그램/드라이버 다운로드·전체 폴더는 대상이 아니다. 다른 드라이버 버전 경로/사용자 지정 위치까지 포괄한 결과는 아니다.
- 현재 프로필/SID 목록을 확인할 수 없으면 사용자 검사를 생략한다. 다른 사용자, UNC, 미해석 8.3, 보호 위치를 거절한다. 루트·상위 폴더는 위에서 아래 순서로 확인해 정션/placeholder/접근 실패 이후 하위 속성도 조회하지 않는다. 하위 링크·온라인 전용 파일 제외, 시간 초과의 부분 합계 보존, 숫자 없는 미관측을 구분한다.
- `ShaderCacheRule`: Steam/그래픽 각각 한 카드에 위치별 크기·부재·실패 사유를 표시한다. 관측 합계는 하드링크 중복 가능성이 있는 논리 크기이며 확보 가능량/개선 후보가 아니다. 미관측 위치가 있으면 일부 위치 합계로 명시한다. 전체 미관측은 확인 불가다. 새 관측이 있을 때만 같은 보충 앱 카드·Steam 설정 카드 중복을 제거한다. 다른 커뮤니티 정보는 보존한다.
- `ScanService`/`AppCacheProbe`에 등록하고 `FindingCardViewModel`에 **캐시별 용량·주의사항 보기**를 연결했다. 첫 화면 **앱 캐시 위치·용량** 버튼은 앱 캐시 분류로 이동하며 실행·삭제·재검사를 하지 않는다. Resolve/CapCut/Adobe 결과도 같은 분류에서 볼 수 있다.
- 위치 측정값은 기존 `appCache.*` 내보내기 토큰화 범위에 넣었다. 원문 라이브러리 경로를 문장에 복사하지 않고 별도 측정값으로 표시한다. 레지스트리 키 전체/자동 로그인 계정 값은 요청하지 않는다.

## 근거와 제한

- Steam 라이브러리 파일 파서/디스크 경로는 기존 프로젝트 구현과 포함 규칙의 좁은 경로 계약을 재사용했다. [Valve KeyValues 문서](https://developer.valvesoftware.com/wiki/KeyValues)는 형식 참고이며 파일 경로나 자동 삭제 계약의 보증으로 쓰지 않는다.
- [Steam 공식 다운로드 캐시 정리](https://help.steampowered.com/en/faqs/view/6AD7-820D-8BE5-E51F)는 다운로드 캐시를 다룬다. 이 버튼이 `shadercache` 전체를 지운다고 안내하지 않는다. [Steam 클라이언트 셰이더 캐싱 기록](https://store.steampowered.com/news/posts/?enddate=1521655199&feed=steam_client)은 사전 컴파일 캐시 기능의 참고다.
- [NVIDIA 제어판 Shader Cache Size](https://www.nvidia.com/content/Control-Panel-Help/vLatest/en-gb/mergedProjects/nv3dENG/Manage_3D_Settings_%28reference%29.htm)는 디스크 저장 한도와 재컴파일 비용을 설명한다. 저장 한도 변경을 캐시 삭제 버튼으로 표시하지 않는다. Windows 디스크 기본 위치는 기존 프로젝트의 검토된 보충 규칙에서 가져왔다.
- Steam/그래픽 앱의 idle 판정·잠금·정리 후 게임 재개 평가가 아직 없어 자동 삭제 실행기는 등록하지 않는다. 단순히 파일이 오래됐거나 잠기지 않았다고 삭제 허용으로 간주하지 않는다. Steam의 다운로드 캐시 정리 기능을 셰이더 정리로 대체하지 않는다.
- 메타데이터 검사 중 경로/파일이 바뀔 수 있으며 삭제 실행기 수준의 핸들 고정/원자 스냅샷은 아니다. native I/O가 멈추는 동안 강제 중단하는 hard timeout은 아니다. 사용자 지정 폴더를 임의로 추가하는 기능은 없다.

## 검증

- `dotnet build PcOptimizer.sln -c Release --no-restore`: 최종 경고 0/오류 0.
- `dotnet test tests/PcOptimizer.Tests/PcOptimizer.Tests.csproj -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=shader-basic-final.trx' --results-directory TestResults/shader`: **1564/1564**, 신규 30건(검사/규칙/카드 모델 29 + 실제 XAML 연결 1).
- 중간 전체 기본 1534건에서 기존 보충 카드 ID를 기대하던 테스트 1건이 실패했다. 제품이 새 위치별 카드로 대체하므로 테스트를 새 카드 2개 및 기존 중복 부재까지 확인하도록 바꿨다. 보호/미관측 기대를 완화하지 않았다. 관련 43/43, 이후 전체 1560/1560, 레지스트리 실패/중복 시스템 보호 추가 후 1564/1564를 통과했다.
- 새 경계: 서로 다른 드라이브, 기본 Program Files 예외와 중복 보호 출처, 경로 중복, 잘못된 VDF/레지스트리 실패, 외부 사용자/UNC/8.3/클라우드, 상위 링크 미진입, 위치별 크기/부재, 부분 합계/예산, 프로필 확인, 계정 미수집, 기본 내보내기 경로 익명화, 승격/일반 프로브 연결, XAML 진입·상세 토글.
- `dotnet test tests/PcOptimizer.Tests/PcOptimizer.Tests.csproj -c Release --no-build --filter Category=Smoke --logger 'trx;LogFileName=shader-smoke.trx' --results-directory TestResults/shader`: **44/44**. 사용자 캐시·설정 변경은 없고 기존 쓰기 Smoke는 GUID 소유 fixture에 한정한다. Online/ToolSmoke는 관련 실행/온라인 경로 변경이 없어 이번에 재실행하지 않았다.

## 완료 기록

실제 승격 AppCacheProbe: Success, 27,256 ms. 사용자 경로·게임 이름·계정 값은 아래 기록에 포함하지 않는다.

| 대상 | 상태 | 논리 bytes | 파일 수 |
|---|---|---:|---:|
| Steam 라이브러리 1 | Observed | 12,327,435 | 9 |
| Steam 라이브러리 2 | Absent | 미관측(폴더 없음) | — |
| Steam 라이브러리 3 | Absent | 미관측(폴더 없음) | — |
| Steam 라이브러리 4 | Observed | 191,006 | 4 |
| NVIDIA DXCache | Observed | 11,894,505,472 | 105 |
| NVIDIA GLCache | Observed | 1,081,026 | 24 |
| Windows Direct3D | Observed | 3,207,168 | 73 |

Steam 합계 12,518,441 bytes(화면 12.5 MB), 그래픽 합계 11,898,793,666 bytes(화면 11.9 GB). 이 값은 확보 가능량이나 삭제 권한이 아니다. 캐시 파일을 변경하지 않았다.

`tools/package.ps1 -OutputDirectory dist/sp1-evaluation`의 locked restore/publish 및 ZIP 고지·규칙·원본 해시 검증 완료:

- `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.6-win-x64.zip`: 내부 13파일, 최상위 4개, EXE 65,161,192 bytes.
- SHA-256 `5E7D89E11944A09E8D8C9F29F8F67D381D596E6ABBEA92B3884214BA68A748FC`.
- 기존 ZIP과 실행 중인 앱을 보존했다. 새 EXE로 실행해야 캐시 진입 버튼과 새 검사 결과가 표시된다. master 병합·원격 게시 없음.

T6 시작 앱·T9 Update/DO·T10 자동 정리 및 T8/T12 실기 평가, UI/UX 전면 개편은 미완료다.
