# SP1 Task 5 — 사용자 임시 파일 정리 (구현자 자체 검증)

- 기본 LocalAppData Temp와 Explorer 썸네일/아이콘 패턴만 코드 등록했다. 최소 생성/수정 경과 7일, 기본 TEMP/TMP 일치, 현재 등록 프로필·보호 정책·Known Folder·동기화 경로·다른 SID 경로를 확인한다. 옮겨진 Temp는 지원하지 않는다. 조회용 가드와 달리 현재 프로필 안에 중첩된 다른 SID도 제외한다.
- `Probes/Actions/Files/`의 4파일: 15초/20,000개 예산 내 완전 열거만 계획화. 파일 ID/볼륨/크기/생성·수정·변경 시각/속성/링크 수/부모 ID 고정. 실행 전 전체 재검사 및 삭제 직전 독점 핸들 검사. 부모 핸들을 유지하며 FileDispositionInfo로 파일만 삭제한다. 디렉터리 삭제·소유권 변경·프로세스 종료 없음.
- 새 파일은 대상에 추가하지 않는다. 바뀐 파일, 잠긴 파일, readonly/hardlink/reparse/offline/recall 항목 제외. 파일별 삭제/건너뜀/실패와 동일 볼륨의 실측 여유 변화(null/음수 포함)를 구별한다. 여러 볼륨에 걸친 대상은 계획을 발급하지 않는다.
- App 코드 등록 → 조치 선택 → 조회 미리보기 → 별도 실행 확인 → 결과/재검사 연결. `ActionCenterViewModel`, `ActionCenterWindow`, `ActionPresentation`, `ActionEffect`에 선택과 파일별 결과를 반영했다. Full 범위만 제공한다.

## 검증

`dotnet`은 `C:\Program Files\dotnet\dotnet.exe`, Release 구성이다.

- `build PcOptimizer.sln -c Release --no-restore`: 경고 0, 오류 0.
- `test tests/PcOptimizer.Tests -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp1-t5-basic-final.trx'`: **1381/1381**.
- `test ... --no-build --filter 'Category=Smoke' --logger 'trx;LogFileName=sp1-t5-smoke.trx'`: **39/39**. 이후 추가한 alias/hardlink 테스트를 포함한 `FullyQualifiedName~FileCleanupSmokeTests`, `sp1-t5-native-final.trx`: **4/4**. 전체 40건 일괄 재실행으로 표현하지 않는다.
- 신규 파일 계약 17건 + UI 선택/확인 1건. 실제 native 시험은 생성한 GUID fixture만 대상으로 승인된 관리자 실행. 파일 교체·잠금·심볼릭 링크 조상·short path 정규화·hardlink 제외를 검증했다. 호스트에서 short-name이 긴 이름과 같으면 8.3 별칭 생성 자체의 증거는 아니며 기존 CacheBoundarySmoke도 보존한다.
- 최초 취소 회귀는 조율기 최종 결과가 아직 Running인 시점에 읽어 실패했다. 실제 gate Idle을 기다리는 것으로 테스트를 바로잡았다. 전체 기본 실행의 기존 WUA 테스트 1건은 20ms 타이머가 SearchAsync보다 먼저 취소되는 경쟁으로 실패했다. 2개 취소 테스트를 작업 생성 후 명시적 취소로 바꾸고 전체 통과했다. 제품 WUA 코드는 변경하지 않았다.

## 제한과 후속

- 독립 리뷰는 사용자 지시로 생략. 실제 사용자 임시 파일·Windows 설정은 테스트에서 변경하지 않았다.
- Known Folder 이동/클라우드 공급자 실환경 전체 조합, 다른 계정 UAC와 실제 디렉터리 junction 교체, 전원 차단은 미실시다. 인식하지 못하는 위치를 새 대상으로 추정하거나 지원 완료로 표시하지 않는다.
- 원장 REV-008 경로 정규화, REV-013 실행 전 거절, REV-016 사용자 범위, REV-017/018 공유 관문을 유지한다. 기존 독립 검증 이력을 확대하지 않는다.
- API 근거: [SetFileInformationByHandle](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfileinformationbyhandle), [FILE_DISPOSITION_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_disposition_info), [FILE_ID_INFO](https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_id_info).
