# 1차 공식 도구 조치 — 사용자 확정 사항

2026-09-27 사용자는 **자동 조치도 1차에 포함**하고 **npm·pip·NuGet HTTP 캐시 정리와 Windows 정리 도구 연결**을 선택했다. 기존 설계·공통 제약의 조회 전용/2차 이월 문구는 이 허용 목록에 한해 대체한다. 드라이버 설치·주사율 변경·Squirrel 버전 폴더·NuGet 전역 패키지 정리는 포함하지 않는다.

## 실행 계약

- 설치된 도구로 캐시 경로를 조회한다. npm은 node.exe와 같은 설치 위치의 npm-cli.js, pip은 python.exe의 `-I -m pip`, NuGet은 dotnet.exe를 사용한다. 도구 자동 설치·셸·임의 명령·외부 규칙 명령문 없음.
- 흐름: 도구 선택 → 대상 확인(실행 파일·위치·논리 크기·재다운로드·되돌리기 불가) → 사용자 확인 → 실행 → 재관측 → 창을 닫으면 전체 재검사.
- 계획은 5분 유효·일회용이다. 실행 직전 도구 지문·캐시 경로·보호 정책을 다시 확인한다. 변경되면 새 확인을 요구한다. 실행은 직렬화한다.
- (2026-09-27 SP4 Task 10 정정) 앱은 항상 관리자 권한으로 실행된다. 도구 실행 파일(npm 진입 파일, pip은 python.exe 포함)이 정규화 후 보호 위치(`%ProgramFiles%`·`%ProgramFiles(x86)%`·`%ProgramW6432%` 아래)에 있을 때만 실행하고, 사용자 쓰기 가능 위치의 도구(PATH 탐색 결과 포함)는 실행하지 않고 직접 실행을 안내한다. 표준 계정이 다른 관리자로 승격한 SystemOnly 인스턴스는 사용자별 캐시(npm·pip·NuGet 모두)를 UI와 실행기 양쪽에서 관측·실행 전에 거절한다. 도구의 작업 폴더도 사용자 쓰기 가능 위치가 아니어야 하므로 System32로 고정한다(dotnet global.json·npm 프로젝트 .npmrc·python 모듈 경로를 작업 폴더에서 찾지 않게 함). 정리 창 노출 판정은 실행 규칙과 같은 후보·보호 위치 판정을 쓴다.
- 자동 실행은 현재 사용자 프로필 아래 일반 로컬 캐시로 제한한다. 프로필 밖으로 옮긴 캐시는 진단을 유지하고 수동 도구 관리를 안내한다.
- 포함 보호 정책, 다른 사용자, 문서·사진·바탕화면·동기화 경로, 대상과 하위의 정션/링크/placeholder, 불완전한 열거를 차단한다. NuGet HTTP 위치에 `.dat` 이외의 파일이 있으면 실행하지 않는다.
- npm `cache clean --force --offline --cache <확인 경로>`, pip `--cache-dir <확인 경로> cache purge`, dotnet `nuget locals http-cache --clear`만 허용한다. NuGet은 자식 프로세스의 `NUGET_HTTP_CACHE_PATH`로 고정한다.
- 개발·자동 테스트에서 실제 사용자 캐시를 정리하지 않는다. 가짜 도구 또는 소유한 임시 fixture만 사용한다.
- 논리 크기는 확보 용량 보장이 아니다. 도구 정상 종료와 후속 관측 성공을 구분하며 실패·시간 초과에는 부분 정리 가능성을 표시한다. 제3자 프로그램의 동시 변경은 완전히 잠글 수 없으므로 패키지 설치·빌드 중에는 실행하지 않는다.

## 공식 근거

- [npm cache](https://docs.npmjs.com/cli/v8/commands/npm-cache/): 공간 확보 목적의 clean과 force 요구.
- [pip cache](https://pip.pypa.io/en/stable/cli/pip_cache/), [pip caching](https://pip.pypa.io/en/stable/topics/caching/): 위치 조회와 HTTP·wheel 캐시 purge.
- [NuGet 캐시 관리](https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders): HTTP 캐시와 전역 패키지 구분.

## 출시 검증

구현·검증 진행 중. P7에 확인창 취소, 계획 만료/재사용/대상 변경, 보호 거절, 도구 실패/후속 확인 실패, 임시 fixture 명령 검증을 추가한다. 일반 권한·DPI·외부 도구 버전별 실제 UI 및 별도 .NET 없는 PC 검증은 단위 테스트로 대체하지 않는다.
