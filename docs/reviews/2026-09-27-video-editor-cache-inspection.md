# DaVinci Resolve·CapCut 캐시 검사 / preview.5

구현·검증·배포 기록 커밋: `820b53d` (19파일). 후속 커밋은 인계 참조 기록이다.

기준 `806bff9`, 브랜치 `codex/sp1-actions`. 사용자가 무료 영상 편집 앱 캐시도 확인하도록 요청했다. 기존 자동 조치 목록은 유지하고 **두 앱의 읽기 전용 캐시 후보 검사**를 추가했다. 구현자 자체 검증이며 사용자 지시에 따라 독립 리뷰·서브에이전트는 생략했다.

## 구현 및 접근

- `DaVinciCacheConfigReader.cs`: 현재 프로필의 `AppData/Roaming/Blackmagic Design/DaVinci Resolve/Preferences/config.dat`를 최대 64 KiB로 읽는다. 확인한 텍스트 형식의 `RenderCaching.CacheDir`, `Site.Count`, 첫 저장소의 `Type`/`Root`만 남긴다. `CacheClip` 상대 이름은 단일 사이트의 첫 IOFileSys 저장소와 결합하고, 절대 경로도 마지막 이름이 CacheClip일 때만 후보로 반환한다. 중복 키·변수·상위 경로 이동·알 수 없는 형식은 거절한다. 프로젝트 라이브러리/DB는 읽지 않는다.
- 설정이 없을 때는 Known Folder Videos/CacheClip만 확인한다. 설정이 있는데 읽거나 해석하지 못하면 기본 폴더로 대체하지 않는다. 전역 설정과 프로젝트별 경로 재정의는 구분한다. 프로젝트별 재정의, 다른 캐시 이름, 모든 Resolve 버전의 설정 형식은 미지원이다.
- `VideoEditorCacheInspector.cs`: CapCut은 현재 프로필 기본 `AppData/Local/CapCut/User Data/Cache`만 메타데이터로 센다. Projects/Draft/Apps/내보내기 폴더나 User Data 전체를 캐시로 간주하지 않는다. 옮긴 LocalAppData·사용자 지정 CapCut 경로·버전별 다른 위치는 미확인이다. 이 PC에 CapCut 기본 설치 경로가 없어 해당 탐지는 가짜 트리로 검증했다.
- `AppCacheProbe`에서 순차 실행하고 `ScanService`에 `VideoEditorCacheRule`을 등록했다. 앱/경로가 없으면 새 카드를 만들지 않는다. **전체 결과**에서 각 앱 정보 카드의 **캐시 위치·정리 방법 보기**로 위치·관측 범위·앱 자체 정리 안내를 펼친다. 별도 실행 인자는 필요 없다. 카드 크기는 논리 크기이며 삭제량/개선 후보/바로 실행 수로 집계하지 않는다. 소량도 0 GiB로 반올림하지 않도록 bytes/KiB/MiB/GiB를 쓴다.

## 읽기 경계

- Full 사용자 검사에서만 실행하며 기존 프로브의 User 범위/SystemOnly 제외를 유지한다. ProfileList와 현재 SID를 확인할 수 없으면 새 사용자 설정 검사도 생략한다.
- 승격 중 사용자 설정 미적용이라는 기존 정책에 **Resolve 전용 제한적 읽기 예외**를 추가했다. 임의 명령 실행·플러그인 로드·삭제 대상 등록은 없다. npm/pip/NuGet/Steam의 승격 시 설정 미적용은 바꾸지 않았다.
- `ResolvedProtection.SourceRoots`에 중복 경로의 보호 출처를 모두 보존한다. 기존 Roots/IsProtected/삭제 정책은 그대로다. 새 inspector만 Videos 바로 아래 CacheClip 읽기를 허용한다. 동일 경로에 Documents/클라우드/시스템 보호가 겹치면 예외를 적용하지 않는다. 동영상 전체나 CacheClip이라는 임의 폴더 전체에 통용되는 예외가 아니다.
- 기존 긴 경로 정규화, 다른 사용자 경계, 루트·상위 정션/온라인 전용/접근 실패 차단, 하위 링크/placeholder 건너뜀을 적용한다. 설정 파일 본문도 링크·보호 확인 후 요청한다. UNC는 지원하지 않는다. 스캔은 메타데이터 관측이며 읽는 동안 파일이 바뀔 수 있고 링크 교체에 대한 삭제 실행기 수준의 핸들 고정 보장은 주장하지 않는다.
- 대상별 5초와 AppCacheProbe 전체 예산·취소를 공유한다. 이미 센 크기는 부분 결과로 보존하고 미관측은 숫자 없음으로 구분한다. native I/O 자체를 강제 중단하는 hard timeout은 아니다. 하드링크 물리 중복을 제거한 크기나 확보 가능량이 아니다.
- `appCache.videoCache.*.paths`를 기존 내보내기 경로 토큰화에 연결했다. 외부 드라이브 이름·인증 값이 기본 내보내기에 남지 않음을 검증했다.

## 외부 근거 및 현장 관측의 구분

- [Blackmagic Resolve 20 초보자 가이드](https://documents.blackmagicdesign.com/UserManuals/DaVinci-Resolve-20-Beginners-Guide.pdf?_v=1757574012000): Playback의 Delete Render Cache로 렌더 캐시를 관리한다. 공식 앱 기능 안내의 근거이며 내부 config.dat 스키마가 모든 버전에서 유지된다는 보증으로 쓰지 않는다.
- [Blackmagic Resolve 11 참조 매뉴얼](https://documents.blackmagicdesign.com/UserManuals/DaVinci_Resolve_11_Reference_Manual.pdf): 첫 미디어 저장소/프로젝트별 캐시 위치를 설명하는 과거 자료다. 최신 버전 설정 형식의 증명으로 쓰지 않았다. 이번 config.dat의 작은 텍스트 형식과 CacheClip 키 조합은 이 PC에서 읽기 전용으로 직접 확인했다.
- [CapCut 공식 데스크톱 안내](https://www.capcut.com/help/editing-not-match-displayed): Settings → Draft → Clear Cache(캐시 크기 옆 휴지통). 공식 문서는 모든 Windows 버전의 디스크 경로를 보증하지 않는다. 기본 Cache 경로는 제한된 탐지 후보이며 실제 설치된 CapCut에서 검증하지 못했다.
- 두 앱의 자동 삭제 실행기는 추가하지 않았다. Cache 안의 다운로드 자원·미디어 사본이나 프로젝트 의존성을 관측만으로 판단하지 않으며, 정리는 앱 안에서 하고 다시 검사하도록 안내한다.

## 검증

- `dotnet build PcOptimizer.sln -c Release --no-restore`: 경고 0/오류 0.
- `dotnet test tests/PcOptimizer.Tests/PcOptimizer.Tests.csproj -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke'`: 최종 **1534/1534**. 신규 44건(검사/파서/규칙/카드 모델 43 + 실제 XAML 연결 1).
- `dotnet test ... -c Release --no-build --filter Category=Smoke --logger 'trx;LogFileName=video-editor-smoke.trx' --results-directory TestResults/video-editor`: **44/44**. 사용자 캐시·설정 변경 없이 실행하며 기존 삭제 테스트는 GUID 소유 fixture에 한정한다.
- 마지막 소량 용량 문구와 메타데이터 출력 추가 뒤 `--filter FullyQualifiedName~AppCacheSmokeTests`로 실제 호스트 검사 **2/2**를 재실행했다. `TestResults/video-editor/video-editor-host-final.trx`: 승격 검사 Success, Resolve Observed, **3,596,144 bytes (3.43 MiB), 14파일, 건너뜀 0**. 전체 Smoke의 수집/보호 코드와 이후 변경된 문구/출력 범위를 구분한다.
- 신규 검증: 이동된 Resolve CacheClip, 설정 실패 기본 대체 금지, 중복 보호 출처, 원본/프로젝트 제외, 미설치/부재/접근 거부 구분, 다른 프로필/미해석 8.3/UNC, 정션/placeholder/설정 링크, 부분 집계/시간 초과/취소, 승격/일반 컨텍스트 연결, 내보내기 익명화, 실제 XAML 두 카드의 정리 안내 토글.
- 최초 테스트 컴파일에서 params 인자의 target-typed new와 누락된 NullAppLogger namespace를 수정했다. 첫 실행 36/36 이후 추가 경계를 넣어 전체 기본 1534/1534까지 확인했다. 실패를 성공으로 감추지 않는다.
- Online/ToolSmoke는 네트워크/공식 도구 실행 경로 변경이 없어 미실행. 실제 CapCut 설치, Resolve 프로젝트별 경로/정리 후 편집 재개, UAC/다른 계정/.NET 없는 PC는 별도 평가다.

## 배포

`tools/package.ps1 -OutputDirectory dist/sp1-evaluation`의 locked restore/publish 및 ZIP 원본 해시·고지·규칙 검증을 통과했다.

- `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.5-win-x64.zip`: **59,907,142 bytes**, 내부 13파일/최상위 4개, EXE 65,157,072 bytes.
- SHA-256: `97FBACA75598B7D69CEC1D8B5C8C1782FE20B5CB0B0D410FB7DF96B4EC93E686`.
- 기존 ZIP과 실행 중인 앱은 보존했다. 새 ZIP으로 실행하고 전체 결과를 열어야 새 카드를 볼 수 있다.

T10 F2 전체, Steam/NVIDIA, T6 시작 앱, T9 Update/DO, T8/T12 실환경 평가는 여전히 남는다. master 병합·원격 게시 없음.
