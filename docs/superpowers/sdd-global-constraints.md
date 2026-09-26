# 공통 제약 (모든 작업에 적용) — 출처: docs/superpowers/specs/2026-09-26-pc-optimizer-design.md

## 스택·구조
- C# / .NET 10 LTS (SDK 10.0.401 설치됨, `global.json`으로 고정). WPF. Windows 11 x64.
- 프로젝트: `src/PcOptimizer.Core`(net10.0, Windows API·파일 시스템·네트워크 I/O 의존 없음), `src/PcOptimizer.Probes`(net10.0-windows), `src/PcOptimizer.App`(WPF, net10.0-windows), `tests/PcOptimizer.Tests`(xUnit, net10.0-windows).
- 의존 방향: App → Probes → Core, App → Core. Probes는 App을 모른다. Core는 어느 쪽도 모른다.
- 라이브러리: CommunityToolkit.Mvvm, WPF-UI(Fluent), System.Management, xUnit. 다른 UI 스택으로 바꾸지 않는다. 복원 lock 파일(packages.lock.json) 사용.
- App 매니페스트는 `asInvoker`. 앱은 일반 권한으로 시작한다.
- `IElevatedExecutor`, `IUnknownFolderAdvisor`는 Core에 타입 계약만 두고 1차에 구현·등록·호출하지 않는다. Elevated/Llm 프로젝트는 만들지 않는다.

## 조회 전용 원칙
- 1차는 파일 삭제·이동, 레지스트리 변경, 드라이버 다운로드·설치, 업데이트 설치를 하지 않는다. 앱 로그·설정·사용자가 내보낸 리포트 저장만 허용.
- 미구현 프로브를 성공한 것처럼 반환하는 스텁을 두지 않는다. 조회 실패를 빈 목록이나 Ok로 바꾸지 않는다.

## 데이터 모델 불변식
- Verdict = Ok | Candidate | CannotVerify | Info. "문제" 등급 없음.
- CannotVerify는 CannotVerifyReason 필수(ElevationRequired | NetworkFailed | NoRule | Unsupported | ProbeError | Timeout | Cancelled | AccessDenied | PartialData | Ambiguous | NotRequested). 그 외에는 null.
- Candidate는 Recommendation과 Evidence 필수.
- Measurement = { Name, Value(숫자/불리언/문자열/목록 구분), Unit?, Source, ObservedAtUtc, Quality(Observed|Reported|Estimated|Partial) }.
- ProbeResult = { ProbeId, Status(Success|Partial|Failed|Skipped|Cancelled), Measurements, Issues, StartedAtUtc, Duration, UserContext }. 프로브는 Finding/Verdict를 만들지 않는다. 규칙 엔진이 변환한다.
- IRule은 불변 스냅샷(측정값 + 프로브 상태/Issues)만 입력받아 Finding[]을 내는 순수 함수. I/O 없음.

## 코딩 규칙 (프로젝트 규칙)
- 모든 C# 파일 서두에 헤더:
  /**
   * @file    : <파일명>
   * @author  : rudals252
   * @brief   : <파일 기능 요약>
   */
  XAML 파일은 XML 주석 헤더(`<!-- @file / @author: rudals252 / @brief -->`). 생성 파일(obj/, *.g.cs 등)은 제외.
- 파일 구성 순서: 헤더 → using(기본 → 서드파티 → 프로젝트) → 상수/필드 → 메서드 → 클래스.
- 상수 UPPER_SNAKE_CASE, 클래스·메서드 PascalCase. 매직 넘버 금지. 환경별로 바뀌는 값(타임아웃, 로그 경로)만 설정으로 분리.
- 모든 공개 클래스·메서드에 한글 XML 문서 주석.
- 로깅은 프로젝트 공용 로거 래퍼 하나로만. `Console.WriteLine` 직접 사용 금지.
- 한국어 UI 문자열은 리소스 파일로 분리.
- Nullable 활성화, 분석기 경고를 무시하지 않는다.

## 검증
- 완료 보고는 실제 `dotnet build`/`dotnet test` 출력을 확인한 뒤에만. 실패는 출력 그대로 보고.
- 기본 테스트(`--filter "Category!=Smoke"`)는 OS 전체 스캔·네트워크·UAC를 띄우지 않는다.
- 커밋 메시지 끝에 `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` 추가. git user는 `-c user.name=rudals252 -c user.email=jwr300028@gmail.com`.

## 공유 리뷰 원장 (필수)
- `docs/reviews/REVIEW_LEDGER.md`는 독립 검토자(Codex)의 결함·범위 항목(REV-xxx)을 담은 공유 원장이다. 구현 시작·리뷰 시작·단계 완료 보고 전에 자기 작업과 관련된 항목을 반드시 읽는다.
- 항목을 고치면 그 항목의 `대응 기록`에 변경 파일/커밋, 실행한 검증 명령, 결과, 남은 제한을 적고 상태를 `수정됨·재검증 대기`로 바꾼다(`검증 완료`는 독립 검토자만 기록). 보류·비반영은 이유를 적는다.
- `docs/reviews/repro/`의 재현 테스트는 일반 빌드에 포함되지 않는다. 해당 항목을 고칠 때 이를 `tests/PcOptimizer.Tests/Unit/...`의 정식 회귀 테스트로 편입한다(파일 헤더·한글 XML 주석 규칙 적용).
- 리뷰어는 관련 REV 항목이 diff에서 실제로 해결됐는지 별도 확인한다. 다른 리뷰어의 승인만으로 REV 항목을 닫지 않는다.
