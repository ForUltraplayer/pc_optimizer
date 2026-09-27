# SP4 최종 독립 재검증 — 2026-09-27

## 대상과 판정

- 기준 HEAD `0fa2bf5`, 마지막 제품 코드 `ec3547e`, 브랜치 `feature/p0-skeleton`. 시작 시 작업 트리 깨끗함.
- 공유 원장, HANDOFF, SP4 최종 리뷰 사본, 이전 리뷰 이후 `237708f..0fa2bf5` 변경을 확인했다. 중점은 Task 10·13의 REV-010/016~018, 관리자 도구 실행, 최종 후속의 링크 실행·동기 프로브·초기 타일, 패키징이다.
- **검토 범위에서 새 병합 차단 코드 결함을 발견하지 않았다.** 이전 실패 재현 4건을 원문 그대로 최신 고정 복사본에 넣어 모두 통과시켰다. 구현 완료·자동 검증 수치는 보고와 일치한다.
- 이는 SP4 코드와 자동 검증 범위의 판정이다. 실제 다른 계정 UAC, 일반 권한 브라우저, .NET 미설치 PC 등 미실시 수동 검증까지 완료됐다는 뜻은 아니다. 병합·게시·사용자 캐시 정리는 하지 않았다.

## 기존 발견 재검증

| 항목 | 확인 근거 | 판정 범위 |
|---|---|---|
| REV-010 | `CacheProcessGuard`가 중첩 AggregateException을 펼쳐 예상 종료 예외를 기록하고 대기·핸들 보존·늦은 종료 후 복구. 원본 `AggregateKillFailureMustNotEscapeGuard` 통과, 정식 종료 실패·로그 회귀 통과 | 해당 잔여 예외 경로 검증 완료. 실제 종료 불가 프로세스를 만들지는 않음 |
| REV-014 | 드라이버 요약 타일 제거. 체크박스·요청만으로 완료 시각을 만들지 않고 두 온라인 공급자 성공과 온라인 비교 규칙을 확인. 로컬 Driver CannotVerify와 구분하는 회귀 통과 | SP4 온라인 상태 표시 검증 완료. 실제 온라인 서버 조회는 이번에 미실행 |
| REV-015 | `DoNowCount`/`DoManuallyCount`, 0건 바로 실행 타일 숨김, 보호 위치 도구가 있을 때 정리 창 노출, `SummaryTiles` 검사 전 숨김을 소스·WPF 레이아웃 회귀로 확인 | SP4에서 채택한 1·2항만 검증 완료. 제품 제안 3·4항 및 실제 초보자 사용성은 후속 |
| REV-016 | MainViewModel → MainWindow → CacheToolsWindow → SystemCacheToolBackend에 SystemOnly 전달. Locate·Inspect·Clear 모두 관측 전 거절, Started=false. 3종 도구 거절·Full 허용 회귀와 원본 재현 통과 | UI·backend 계약 검증 완료. 표준 계정의 실제 UAC 전환은 미실시 |
| REV-017 | `Task.Run`으로 동기 프로브를 UI 밖에서 시작. 타임아웃 후 실제 Task 추적·재호출 차단·늦은 완료 복구. 양방향 검사/사양 UI 차단과 동기 BlockingProbe 회귀, 원본 반복 Capture 재현 통과 | 현재 UI 호출 경로 검증 완료. 서비스 직접 동시 호출의 공통 직렬화는 기존 이월 유지 |
| REV-018 | IsLoading/IsDraining을 정리 진입 조건에 포함하고 변경 알림 연결. 원본 로딩 중 정리 진입 재현 및 시작·완료/종료 대기 전이 회귀 통과 | 현재 모달 정리 창의 UI 진입 경계 검증 완료 |

REV-010의 원래 구현 일부는 Codex가 작성했지만 이번 잔여 수정은 Claude의 `3d286be`이며, 이번에는 그 수정과 기존 독립 재현 조건을 다시 검증했다. 원장의 이전 자기 검증 이력을 지우지 않는다.

## 독립 실행 결과

`git archive 0fa2bf5`를 `artifacts/review-sp4-final-0fa2bf5/snapshot`에 풀어 실행했다. 원본 제품 소스·정식 테스트는 수정하지 않았다.

| 검증 | 결과 | 근거 |
|---|---|---|
| Release build | 경고 0, 오류 0 | `dotnet build PcOptimizer.sln -c Release --no-restore -p:CopyRetryCount=0` |
| 기존 기본 테스트 | **1279/1279** | `sp4-final-baseline.trx` |
| Smoke | **22/22** | `sp4-final-smoke.trx`, 약 38초 |
| 이전 독립 재현 원문 | **4/4** | `sp4-original-repro-green.trx` |
| 실제 pip ToolSmoke | **1/1** | `sp4-final-toolsmoke.trx`, 소유 임시 HTTP/wheel 캐시 삭제·설치 패키지 fixture 보존 |
| Online | 미실행 | 네트워크 공급자 코드 변경이 이 검토의 중심은 아님 |

TRX 폴더는 복사본의 `tests/PcOptimizer.Tests/TestResults/`다. 기본 실행 후에 `docs/reviews/repro/Sp4Task9ReviewTests.cs`를 복사본의 `tests/PcOptimizer.Tests/Unit/App/`에 **내용 변경 없이** 복사했다. 따라서 기본 1279에는 추가 복사본 4개가 포함되지 않는다.

```powershell
dotnet test PcOptimizer.sln -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp4-final-baseline.trx'
dotnet test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category=Smoke' --logger 'trx;LogFileName=sp4-final-smoke.trx'
dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter 'FullyQualifiedName~Sp4Task9ReviewTests' --logger 'trx;LogFileName=sp4-original-repro-green.trx'
dotnet test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter 'Category=ToolSmoke' --logger 'trx;LogFileName=sp4-final-toolsmoke.trx'
```

ToolSmoke에는 이미 설치된 테스트용 Codex 런타임 Python을 `PCOPTIMIZER_TEST_PYTHON`으로 지정했다. 이 테스트는 `CacheToolProcess`의 실제 `-I` 인자·정리 결과를 검증하며, 제품 backend의 Program Files 도구 탐색까지 실환경 검증한 것은 아니다. 사용자 캐시는 사용하지 않았다. 도구 실행 코드·인자가 이번에 변경됐으므로, ‘수집 코드 변경 없음’만으로 ToolSmoke까지 생략하는 근거는 충분하지 않아 추가했다.

첫 솔루션 `restore --locked-mode`는 Core/Probes의 win-x64 잠금 그래프 차이로 NU1004였다. App 프로젝트를 `-r win-x64 --locked-mode --ignore-failed-sources`로 복원해 같은 패키지 버전으로 검증했다. 기존 이월 lock 그래프 불일치가 여전히 재현되며, 원본 lock 파일은 수정하지 않았다. 복원·실환경 Smoke/ToolSmoke는 샌드박스 밖에서 실행했다.

## 배포 ZIP 확인

- 파일: `dist/PcOptimizer-v0.2.0-win-x64.zip`, **59,588,505 bytes (약 56.8 MiB)**.
- SHA-256: `35B24D9BC1EA4C1907EE00FD6D2A6EF2FF5DFD0758603692B926C588FC58BF09`.
- 최상위 `PcOptimizer.exe`, `실행방법.txt`, `LICENSES/`, `rules/`의 4종. 내부 파일 8개. Python ZipFile.testzip()의 손상 항목 없음.
- `rules/sources.json`, `winapp2.ini`, `supplement.ini`, `rule-metadata.json`은 현재 저장소 파일과 바이트 일치.
- 실행 파일은 ZIP 안에 하나 있으며 크기 65,051,369 bytes. 실제 .NET 미설치 환경에서 실행한 것은 아니다. ZIP의 exe와 소스의 재현 빌드 일치까지 주장하지 않는다.
- 기존 최종 리뷰가 지적한 .NET/WPF-UI 제3자 고지 원문 추가 동봉은 아직 미반영이다. ZIP에는 프로젝트의 THIRD-PARTY-NOTICES.md와 winapp2 라이선스만 있다. 이월 항목을 해결된 것으로 표시하지 않는다.

## 남은 확인과 SP1 인계

1. 링크 실행은 URL 직접 ShellExecute에서 고정 Windows explorer.exe로 변경됐고 시작 정보/실패 안내 회귀는 통과한다. **실제 비승격 브라우저로 열렸다는 검증은 아직 없다.** 셸 미실행·다른 계정·쉼표 URL 처리는 기존 이월로 남긴다. ‘Critical 해결’이라는 문구를 실제 토큰 확인 완료로 읽으면 안 된다.
2. 일반 셸 UAC, 다른 관리자 자격 증명의 SystemOnly, .NET 없는 PC, 실제 DPI·키보드·아이콘·SmartScreen은 수동 확인 대기다. 동기 BlockingProbe 검증은 실제 WMI 손상 PC 검증을 대신하지 않는다.
3. 서비스 계층 공통 직렬화, 도구별 실행 가능 판정, lock 그래프 정리와 기타 기존 minor는 SP1 첫 작업의 명시적 입력으로 유지한다. 현재 UI 보호가 서비스 직접 호출까지 보장하지 않는다.
4. 항상 관리자 전환 후 진단의 사용자 설정 캐시 경로 읽기가 제한되는 기존 동작도 남는다. 사용자가 옮긴 캐시를 찾는 제품 목표와 연결되므로, SP1에서는 지원 범위와 UI 안내를 명확히 할 것. 이번 검증이 해당 기능 확장을 구현한 것은 아니다.

개발 브랜치 병합 판단을 위한 코드 검토는 통과 범위로 볼 수 있다. 일반 사용자 배포 완료 판정은 위 실환경 확인과 별도로 한다. 이 검토에서는 병합·게시하지 않았다.
