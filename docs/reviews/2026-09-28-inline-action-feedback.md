# 조치 카드 내부 확인·결과 표시 (2026-09-28)

기준 HEAD `172b413`, 사용자 요청에 따른 구현자 자체 검증. 독립 리뷰·승인이 아니다.

## 요청과 변경

- 조치 실행 / 되돌리기의 진행·확인·거절 사유·결과를 누른 카드 바로 아래에 표시한다. 화면 위의 공통 영역을 찾아야 했던 동작을 제거했다. 항목별 결과를 보존하며 한 번에 하나의 실행 확인만 활성화한다.
- 같은 페이지의 카드 선택·결과 도착으로 내부/외부 스크롤을 맨 위로 이동하지 않는다. 다른 페이지에서 조치 화면으로 진입하는 기존 흐름에는 별도 확인 영역을 유지한다.
- 복구 가능한 설정은 완료 카드에서 원복을 준비할 수 있다. 원복도 별도 실행 확인을 거친다. 파일·캐시 삭제에는 원복 버튼을 만들지 않는다.
- 거절·링크 실패·폴더 선택 취소도 해당 카드에 남긴다. 지연된 재검사 결과가 새로 누른 카드로 잘못 전달되지 않게 작업 당시의 카드에 연결한다.
- 커뮤니티 규칙 결과를 기본 표시하고, 사용자 문구를 '추가 앱 파일 탐색 결과'로 변경했다. 추천 판정·삭제 안전성·실행 가능 대상을 확대하지 않는다.

## 오래된 임시파일 확인

`App.xaml.cs`의 '오래된 임시 파일 정리'는 `ActionId.UserFiles / UserTempTargets.Temp`이며, 기존에도 `PrepareChoiceCommand → PrepareAsync → Preview → ExecuteCommand → ActionWorkflow.ExecuteAsync`의 별도 실행 확인이 있었다. 준비만으로 삭제하지 않는다. 상단에 있던 확인 패널이 보이지 않으면 반응이 없는 것처럼 보일 수 있었다. 확인 절차·7일 기준·삭제 실행기는 변경하지 않았다.

## 주요 파일

- `ActionFeedback.cs`: 항목별 상태, 실행 확인, 결과, 원복 연결. 기존 조율기의 명령과 수명 관문을 사용한다.
- `ActionFeedbackView.xaml(.cs)`: 카드 내부 표시. 내용이 있는 카드만 상세 UI를 생성하며 중첩 스크롤을 추가하지 않는다.
- `ActionCenterViewModel.cs`, `ActionCenterView.xaml(.cs)`, `MainWindow.xaml.cs`: 카드별 연결 및 내부 조치 스크롤 유지.
- `MainViewModel.cs`, `Strings.resx`: 추가 앱 파일 결과 기본 표시와 설명.
- `InlineActionFeedbackTests`, `ActionCenterLayoutTests`, `MainWindowLayoutTests`, `MainViewModelTests`: 실행 전 확인, 상태 위치, 원복, 지연 결과, 스크롤 및 기본 표시 회귀.

## 검증

```powershell
dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=inline-full.trx' --logger 'console;verbosity=minimal'
```

- 기본 **1767/1767** 통과(기존1761 + 신규6). 빌드 중 오류·경고 없음.
- 준비 진행 중/준비 완료까지 삭제 호출0회, 별도 실행 후1회, 거절 시0회. 설정 원복 준비 중 쓰기 횟수 불변, 실행 후 복원. 실제 파일·GPU·레지스트리 변경 없이 대역으로 검증했다.
- 같은 카드 반복 실패, 다른 카드 실패, 카드 간 결과 보존, 이전 확인 해제, 늦은 재검사의 원래 카드 귀속을 검증했다.
- 실제 WPF 레이아웃에서 카드 내 확인 존재, 상단 공통 확인 없음, 내/외부 스크롤 위치 유지, 외부 진입 확인 영역 이동을 검증했다. 440px 폭 100/150/200% 확인·결과 렌더와 `artifacts/inline-actions/action-selected.png`를 확인했다.
- 중간 실패: `Assert.Single(Where(...))` 분석기 오류는 predicate 오버로드로 수정. 기존 '확인 시 상단 이동' 테스트는 변경된 요구사항에 맞춰 위치 유지로 수정하고, 외부 진입의 기존 동작은 별도 검증했다. 최종 전체 테스트에서 모두 통과했다.

Smoke/Online/ToolSmoke, 실제 사용자 파일 삭제, 실제 설정 원복, 실기 마우스 조작은 이번에 실행하지 않았다. 기존 공개 `v0.0.2-beta.1` ZIP/태그는 이 변경을 포함하지 않으며 원격 배포는 변경하지 않는다. 로컬 Release 실행 파일은 `src/PcOptimizer.App/bin/Release/net10.0-windows/PcOptimizer.exe`이고 .NET Desktop Runtime이 필요하다.
