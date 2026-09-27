# SP1 통합 구현·자체 검증 — preview.7

기준 `f403a97`, 브랜치 `codex/sp1-actions`. 사용자의 구현 → UI/UX → 검증 순서와 독립 리뷰·서브에이전트 생략 지시를 따랐다. 중간 기능별 ZIP 없이 마지막 통합 ZIP 하나를 생성했다. 이전 결과/원장 이력을 덮어쓰지 않는다.

## 이번 결과

- T6: HKCU/HKLM Run에 이어 기본 사용자·공용 시작 폴더의 지원되는 `.lnk` 보관/복원을 연결했다. 임의 프로그램·바로가기 실행 없음. `StartupFolder` 사용자 범위와 `CommonStartupFolder` 시스템 범위를 별도로 등록한다.
- T11: 메인 진입과 조치 목록을 공간 확보 / 로그인 자동 실행 줄이기 / 전력·발열 조정으로 묶었다. 필터 변경에도 확인 계획·최근 결과를 유지하고, 확인·결과는 목록 위에 둔다. 수동 캐시 위치 선택은 펼침 영역에 모으고 빈 목록 사유를 표시한다. 좁은 창 그룹 설명의 줄바꿈도 보완했다.
- 이전 배치의 HKLM Run, 사용자 지정 Adobe·영상 캐시, 업데이트 상태/서비스 복구/Download 정리, Steam 선택 정리·그래픽 공식 연결을 이번 ZIP에 처음 함께 포함한다.
- 기능별 대상·연령·보호 제한은 `tools/README-in-zip.txt`와 앞선 구현 보고서에 명시한다. 관측 용량을 실제 확보 용량이나 성능 향상으로 표현하지 않는다.

## 시작 폴더 실행 계약

- KnownFolder의 기본 Startup 경로가 예상 위치와 같을 때만 허용한다. 리디렉션은 거절한다. Full/SID/세션과 공용·사용자 범위를 재검사한다.
- `.lnk` 이름만 받으며 구분자·ADS·8.3 추정 이름을 거절한다. 모든 조상/Startup 디렉터리를 고정하고 reparse를 거절한다.
- 원본 파일을 독점 핸들로 열고 단일 hardlink, 일반 속성, 기본 데이터 스트림 하나, 76~32768 bytes, Shell Link 헤더/CLSID를 확인한다. 파일을 실행하거나 링크 대상을 해석하지 않는다.
- 볼륨/128-bit ID, 생성·수정·크기·속성, 본문과 owner/group/DACL SHA-256을 116-byte 지문으로 기록한다. 파일 전체는 복사본으로 다시 만드는 대신 원래 파일을 보존한다.
- Pending 기록 뒤 `SetFileInformationByHandle(FileRenameInfo)`로 Startup의 부모 Programs 폴더에 `.PcOptimizer-disabled-{digest}.lnk.disabled`를 보관한다. 덮어쓰기를 허용하지 않는다. 원본 bytes·ID·ACL을 유지하고 이동 뒤 지문을 확인한다.
- 복원은 같은 보관 파일 지문이 맞고 원래 이름에 충돌이 없을 때만 한다. 보관 파일 변경/삭제·재등록은 거절한다. 적용된 시작 항목의 원본 기록은 자동 만료하지 않는다.
- 보관 파일과 복구 기록 둘 다 유지해야 복원 가능하다. 원본 보관 경로는 사용자에게 설명한다. StartupApproved 바이너리 쓰기/Task Manager 토글은 구현하지 않았다. 실제 다음 로그인 효과는 아직 평가하지 않았다.
- 공식 근거: [Windows 시작 앱 관리](https://support.microsoft.com/en-us/windows/experience/startup-boot/configure-startup-applications-in-windows), [FILE_RENAME_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info). API 문서는 앱 전체 복구 보증이 아니다.

## 자체 검증

| 항목 | 결과 | 근거 |
|---|---|---|
| Release 솔루션 build | 경고 0 / 오류 0 | `dotnet build PcOptimizer.sln -c Release --no-restore` |
| 전체 기본 | 1639 / 1639 | `artifacts/sp1-completion/sp1-final-basic.trx` |
| 전체 Smoke | 57 / 57 | `artifacts/sp1-completion/sp1-final-smoke.trx` |
| 공식 도구 ToolSmoke | 4 / 4 | `artifacts/sp1-completion/sp1-final-toolsmoke.trx` |
| WPF 화면 | 100/150/200% 오프스크린 회귀, 메인/좁은 조치 창 PNG 시각 확인 | `artifacts/sp1-completion/ui/` |
| 단일 파일 publish | locked restore / self-contained win-x64 성공 | `tools/package.ps1` |
| ZIP 검사 | 13 파일 / 최상위 4, PE·규칙·라이선스·원본 해시 통과 | `tools/verify-package.ps1` |
| ZIP 검증기 | 해시/고지 누락/규칙 변조/중복 4종 거절 | `tools/test-package-verifier.ps1` |

신규 기본 19건: 서비스 원래 정지/양쪽 중지·복구, 첫째/둘째 중지 실패, 취소 뒤 별도 복구, 복구 실패 시 처리량·미완 기록 보존/다음 작업 차단; HKLM32/64·사용자/공용 폴더의 기록 선행 저장/codec/원문 복원/범위; Adobe·Steam 경로 형식·세션; 영상 위치 스냅샷; NV_Cache 관측; 공식 링크 정확 일치; 효과 필터 중 계획/결과 유지. 기존 검사 구성 기대 목록에 로컬 업데이트 상태 프로브를 추가했다.

신규 Smoke 9건: 실제 사용자 Startup 밖의 `Temp/PcOptimizer.StartupFixture.{GUID}/Startup`만 사용해 이동/복원/취소/파일 ID·본문·ACL 지문, 충돌/변조, ADS/형식/읽기 전용, 세션/이름, 심볼릭/하드링크를 확인했다. 셸 링크 실행, 실제 시작 등록/서비스·전원·화면 변경, 사용자 캐시 삭제는 하지 않았다. ToolSmoke는 npm·pip·dotnet을 소유한 GUID 임시 캐시에 고정했다.

명령:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/PcOptimizer.Tests -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp1-final-basic.trx' --results-directory artifacts/sp1-completion
& 'C:\Program Files\dotnet\dotnet.exe' test tests/PcOptimizer.Tests -c Release --no-build --filter 'Category=Smoke' --logger 'trx;LogFileName=sp1-final-smoke.trx' --results-directory artifacts/sp1-completion
$env:PCOPTIMIZER_TEST_NODE = 'C:\Program Files\nodejs\node.exe'
$env:PCOPTIMIZER_TEST_PYTHON = 'C:\ProgramData\Anaconda3\python.exe'
$env:PCOPTIMIZER_TEST_DOTNET = 'C:\Program Files\dotnet\dotnet.exe'
& 'C:\Program Files\dotnet\dotnet.exe' test tests/PcOptimizer.Tests -c Release --no-build --filter 'Category=ToolSmoke' --logger 'trx;LogFileName=sp1-final-toolsmoke.trx' --results-directory artifacts/sp1-completion
& ./tools/package.ps1 -Configuration Release -OutputDirectory dist/sp1-integrated
& ./tools/test-package-verifier.ps1 -ZipPath dist/sp1-integrated/PcOptimizer-v0.3.0-preview.7-win-x64.zip
```

실패/제한 이력: 첫 기본 실행 1619/1620은 새 프로브의 기대 목록 누락이었다. 신규 테스트의 xUnit2031 단언 형식 오류 수정. native fixture는 샌드박스 조상 핸들 접근 거절 6건 후 제한 밖 실행에서 7/7, 링크 2건 추가 후 전체 57/57. ToolSmoke는 샌드박스 NuGet ToolFailed로 3/4 후 같은 소유 fixture의 제한 밖 재실행 4/4. 첫 publish는 NuGet.Config 접근 거절, 제한 밖 locked restore/publish 성공. 앱 경계 완화나 테스트 제외로 통과시키지 않았다. Online은 온라인 수집 코드 변경이 없어 재실행하지 않았다. 버전 메타데이터 변경 뒤 최종 Release 빌드/게시도 성공했다.

## 아직 완료로 닫을 수 없는 항목

- T6 StartupApproved/Task Manager 토글: 지원되는 전체 바이너리 계약이 없다. Run 등록 해제·기본 시작 폴더 이동은 별도 동작이며 동일 토글이라고 설명하지 않는다.
- T10 Adobe/CapCut 버전별 설정 파일·Resolve 프로젝트별 경로 자동 탐지 확대: 확인된 스키마/실제 버전 fixture가 없다. 명시적인 수동 위치 선택을 유지한다. 이 요청을 취소하거나 구현 완료로 표시하지 않는다.
- T10 NVIDIA 자동 삭제: 공식 절차의 설정·재부팅과 그래픽 작성자의 유휴를 검증하지 못했다. 검사/정확한 공식 안내/Windows 기능 연결만 제공한다. Resolve/CapCut은 처음 요청한 캐시 확인까지 지원하며 직접 삭제는 미지원이다.
- T12 평가 Windows 11/VM 미제공: 일반 셸 UAC/다른 계정/실제 비승격 브라우저, 실제 다음 로그인·전원·화면 전환/강제 종료/모니터 분리, 업데이트 서비스/재시작/캐시 삭제·복구, 게임/편집 재개, .NET 미설치·SmartScreen·물리 DPI/키보드 시험이 남는다. 현재 작업 PC에서 임의로 변경해 대신 시험하지 않았다.
- 업데이트 COM/BITS ABI·권한/중간 활동 및 Steam 외부 작성자 경합의 실제 실행 검증은 대역/공통 파일 엔진 검증으로 대체되지 않는다. 따라서 정식 출시 준비 완료나 SP1 전체 완료를 선언하지 않는다.

## 산출물

- `dist/sp1-integrated/PcOptimizer-v0.3.0-preview.7-win-x64.zip` 및 같은 이름 `.sha256`.
- 압축 해제본 `dist/sp1-integrated/PcOptimizer-v0.3.0-preview.7-win-x64/PcOptimizer.exe`.
- SHA-256 `DD7D63723F07BB2518A94574EE582178553EC0F38B1BECD98D134AFA3FA3C001`.
- 원격 게시/master 병합 없음. preview.6 원본은 비교용으로 보존했다.
