# SP1 Task 3 구현·자체 검증 (2026-09-27)

기준 `20077db`, 브랜치 `codex/sp1-actions`. 사용자가 “독립리뷰없이 진행한다”고 명시했다. 독립 리뷰를 파견하거나 승인 대기로 멈추지 않으며, 아래 결과는 구현자 자체 검증이다.

## 구현

- `Core/Actions/RollbackRecord.cs`: 버전 1, SID, ID/조치/범위, 코드 대상 키, 원래/적용 값의 존재 여부·형식·전체 bytes, 시각, 리비전, Pending/Applied/Restoring/Restored/Unchanged. 사용자 undo와 서비스/임시 표시 복구 책임을 구분한다.
- `Probes/Actions/RollbackCodec.cs`: 128 KiB 파일·32 KiB 값, 정확한 필드 집합, 누락/중복/미지 필드, 구형 버전/다른 SID/알 수 없는 조치, 불법 전이/원문 교체를 거절한다. JSON에는 실행 명령이나 임의 레지스트리 쓰기 기능이 없다.
- `RollbackStore.cs`: 토큰 SID·세션·관리자 그룹과 HKLM 등록 프로필을 대조한다. `%LocalAppData%\PcOptimizer\rollback`만 사용하고, 프로필 밖으로 이동된 LocalAppData는 이번 구현에서 지원하지 않아 거절한다. 기존 느슨한 저장소 권한을 임의 수선하지 않는다.
- 루트와 파일은 **생성 시점부터 Administrators 소유, 상속 차단, Administrators/SYSTEM 두 ACE만 FullControl**이다. 조상/루트를 재분석점 자체로 열어 확인하고 쓰기·삭제 공유 없는 핸들로 고정한다. 레코드/잠금 파일도 같은 ACL과 단일 hardlink를 확인한다. 저장소 세션의 독점 파일 잠금은 다른 앱 인스턴스에도 적용된다.
- 쓰기: 새 `.pending` 파일 → `Flush(true)` → 동일 폴더 `MoveFileEx(REPLACE_EXISTING | WRITE_THROUGH)` → 교체 파일 재개방/flush. 실패를 성공으로 숨기지 않는다. 실패한 임시 파일은 이슈로 남긴다. 정상 완료 기록은 30일 후 정리하지만 Pending/Restoring/미완료 내부 변경은 기간만으로 삭제하지 않는다.
- `RestoreCoordinator.cs`: T2 `ActionCoordinator`를 재사용한다. 적용 전에 코드 어댑터가 실제 대상과 원문을 재수집/검증하고 Pending 저장 후에만 MarkStarted/변경을 호출한다. 복구는 기록 ID로 준비하고 현재 값이 앱의 적용 값과 같을 때만 원래 값으로 쓴다. 원래 값이면 변경 없이 Unchanged, 제3의 값이면 CurrentValueChanged로 남긴다. 어댑터의 변경 직전 비교 계약도 별도로 요구한다.
- 기록 저장 후 프로세스 중단, 변경 후 최종 상태 저장 실패, 복구 후 상태 저장 실패는 Pending/Restoring과 원문 재관측으로 수습한다. 네이티브 작업이 취소를 무시하거나 종료 관측이 실패하면 실제 종료가 확인될 때까지 공통 관문/저장소 잠금을 유지한다.
- `InspectAsync`는 재시작 후 기록·손상 이슈·NeedsRecovery를 제공한다. **화면 표시/앱 시작 연결은 Task 4**, 실제 OS 조치와 내부 자동 복구 정책은 해당 후속 Task에서 등록한다. 지금은 새 실제 OS 변경 어댑터도 자동 복구 정책도 활성화하지 않았다.

## 검증

| 실행 | 결과 | 근거 |
|---|---|---|
| `dotnet build PcOptimizer.sln -c Release --no-restore` | 경고 0, 오류 0 | 최종 소스 빌드 |
| `dotnet test PcOptimizer.sln -c Release --no-restore --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke" --logger "trx;LogFileName=sp1-t3-final-unit.trx"` | **1340/1340** | T2 1314 + 새 계약 26 |
| 관리자 토큰 `dotnet test tests/PcOptimizer.Tests/PcOptimizer.Tests.csproj -c Release --no-build --filter FullyQualifiedName~RollbackStoreSmokeTests --logger "trx;LogFileName=sp1-t3-storage-final.trx"` | **13/13** | 최종 네이티브 저장소/프로필 검증 |
| `git diff --check` | 통과 | 공백/패치 형식 |

TRX는 `tests/PcOptimizer.Tests/TestResults/`에 있다. 기본 테스트 후 추가 변경은 네이티브 저장 후 flush/프로필 조회 가시성과 그 Smoke 2건이며 최종 Release 빌드·13건으로 검증했다.

계약 테스트: Pending/Restoring 저장 실패 시 변경 미호출, 변경 직후 저장 실패, 재시작 대역, 원래 값 없음 복원, 중복 복구, 현재 값 변경 및 쓰기 직전 변경, SID/범위/코드 대상 거절, 타임아웃 후 실제 종료 전 잠금 유지, 30일 이후 미완료 보존.

네이티브 테스트: 실제 관리자 ACL/왕복/독점 열기, 교체 공유 위반으로 기존 Pending 보존, 부분/타 SID/구형/과대 JSON, ACL 변조, 미리 만든 사용자 쓰기 가능 루트 거절, 루트·조상 symlink/레코드 hardlink 거절, 프로필 등록 대조. 모두 `pcoptimizer-rollback-test-<GUID>` 소유 임시 폴더에서 수행 후 삭제했다. 실제 사용자 복구 폴더/설정/캐시는 쓰지 않았다.

초기 실패: 새 테스트의 `System.IO` using 누락을 수정했다. 샌드박스 토큰의 최초 저장소 Smoke는 관리자 확인 8건과 symlink 권한 1건에서 거절됐다(9 실패/2 통과). 보호 조건을 낮추지 않고 관리자 토큰으로 동일 테스트를 실행해 11/11, 추가 경계 포함 최종 13/13을 확인했다.

## 보장 범위와 후속 책임

- 일반 사용자/medium token이 소유하거나 쓸 수 있는 파일은 복구 지시로 신뢰하지 않는다. 이 호스트에서 ACL 구조/변조 거절은 검증했으나 별도 표준 사용자 프로세스 공격 실험은 하지 않았다. 이미 관리자인 공격자나 오프라인 디스크 변조까지 방어한다고 주장하지 않는다.
- 파일 flush/교체 요청 성공과 실제 전원 차단 내구성 실험은 다르다. 전원 차단·강제 종료 중 즉시 원복은 불가능하며 다음 실행에서 관측한다. 드라이브/하드웨어 실패·기록 삭제·ACL 훼손은 확인 불가로 남는다.
- 복구 설정 API의 외부 프로그램과의 원자 비교/쓰기는 각 플랫폼 API가 제공하는 범위에 달려 있다. T6~9 어댑터는 재식별·허용 값·변경 직전 비교 및 실제 종료를 구현해야 한다. JSON 원문을 registry path나 셸 명령으로 실행하지 않는다.
- 프로세스 간 잠금은 이 저장소를 사용하는 변경/복구에 적용된다. 모든 검사/기존 캐시 도구/다른 프로그램을 전역 잠그는 기능으로 표현하지 않는다.
- Task 4에서 목록/확인/결과/늦은 완료 UI와 익명 내보내기 경계를 연결한다. T6/T9 플랫폼 확인과 T12 일반 셸/UAC/다른 계정/.NET 없는 PC/DPI는 여전히 남아 있다.
- 전체 기존 조회 Smoke·Online·ToolSmoke·GUI·ZIP은 이번에 재실행하지 않았다. Task 1 평가 ZIP은 최신 코드 배포본이 아니다.

구현 근거: [CreateFile 공유/재분석점/디렉터리 핸들](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea), [파일 소유권과 접근 권한](https://learn.microsoft.com/en-us/windows/win32/fileio/file-security-and-access-rights), [DACL/ACE](https://learn.microsoft.com/en-us/windows/win32/secauthz/dacls-and-aces), [MoveFileEx 쓰기 완료/교체](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexa). 코드/실행 근거와 문서상 API 계약을 구분했다.
