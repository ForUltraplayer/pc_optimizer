# UI 다듬기 — 좌측 내비게이션 배치 (2026-09-27, Claude)

기준 `557b3f8`(SP5·SP3 뒤). 구현 커밋 **`ee05ec4`**. 사용자 지시: SP5·SP3가 끝난 뒤 UI를 가다듬는다. 확정 배치안(2026-09-27 목업: 좌측 내비게이션 7개, 상단 타일 2개, 3줄 설명 카드)을 메인 창에 적용했다. 독립 리뷰 없음, 구현자 자체 기록.

## 배치(스펙 §3.1 "구조 설명으로 기록")

| 영역 | 내용 |
|---|---|
| 좌측 내비게이션(228px, 흰 배경) | 앱 제목 → **추천 조치 · 전체 결과 · 문제 해결 · 내 PC 사양 · 고급 · 조치 실행/되돌리기 · 설정**. 선택 항목은 연한 파랑 배경·진한 파랑 굵은 글자. 아래쪽에 마지막 측정 시각·상태 |
| 머리글 | 페이지 제목(굵게) · 오른쪽에 [취소](검사 중) [검사 시작/다시 검사] |
| 추천 조치 | 개요 배너(후보 수·범위·종료 대기 안내) → "바로 할 수 있는 것 n건 / 직접 해야 하는 것 n건" 타일(검사 뒤에만) → "내 PC에서 바꿀 수 있는 것" 효과별 버튼(조치 페이지로 이동·필터) → 정리 결과·범위 배너·상태 → 3줄 설명 카드 목록 |
| 전체 결과 | 같은 카드 목록에 분류 콤보·커뮤니티 규칙 체크 |
| 문제 해결 | 위쪽 증상 칩(선택 강조) → 증상 제목·요약 → 실행 출력 패널(진행·취소·결과·재부팅) → 순서별 도구 카드. 맨 위에 "드라이버 안내 · 어디서 무엇을 받을지" Expander(SP3 뷰) |
| 내 PC 사양 | 기존 `PcSpecView`(fastfetch식 한 열, 캡처·공유) |
| 고급 | SP2 전 자리: "준비 중" 설명과 게임 모드·고급 그래픽 Windows 설정 바로가기. "없다고 문제 없음이 아님" 명시 |
| 조치 실행 · 되돌리기 | 기존 조치 화면(`ActionCenterView`): 효과 필터·확인·실행·결과·앱 캐시 위치 선택·수동 조치·변경 기록/되돌리기 |
| 설정 | 온라인 확인 토글·결과 저장·요약·면책·마지막 온라인 확인(Expander `OptionsExpander`), 개발 도구 캐시 정리(보호 위치 도구가 있을 때만) |

## 구조 변경
- `MainViewModel.CurrentPage`(`MainPage` 열거형)와 `NavigateCommand`. 기존 `IsSpecVisible`·`ToggleSpecCommand`·`ShowAllResults`·`ShowAll/ShowRecommendations/ShowAppCaches`는 페이지 상태와 서로 맞춰 유지(기존 테스트 호환).
- `ActionCenterWindow`·`TroubleshootingWindow`·`DriverGuideWindow`의 본문을 `ActionCenterView`·`TroubleshootingView`·`DriverGuideView`(UserControl)로 옮기고 메인 창 페이지에 내장. 창 클래스는 얇은 래퍼로 남겨 기존 테스트·별도 창 열기를 유지.
- 카드의 "자동 실행 등록 해제 확인"·"Adobe 캐시 정리 대상 확인" 버튼은 창을 띄우지 않고 조치 페이지로 이동한 뒤 미리보기를 준비한다. 주사율 시험 창(`DisplayTrialWindow`)은 별도 창 유지.
- 문제 해결 뷰는 두 열에서 한 열(증상 칩 + 카드)로 바꿔 좁은 폭에서도 잘리지 않게 했다. UserControl 안 DataTemplate의 TextBlock에는 `TextWrapping="Wrap"`을 명시했다(암시적 스타일이 템플릿 안까지 적용되지 않던 문제).
- 실행 중(문제 해결 명령)에는 메인 창을 닫지 않고 문제 해결 페이지로 이동시킨다.

## 검증(구현자)
- `MainWindowLayoutTests`·`MainViewModelTests`·`ActionCenterLayoutTests`·`TroubleshootingLayoutTests`·`DriverGuideTests` 통과. 기존 단언 중 "머리글 전환 버튼 문구"만 내비게이션 항목 선택 상태 확인으로 바꿨다. 정리 창 버튼은 설정 페이지로 옮기고 표시 조건(보호 위치 도구)을 유지.
- 오프스크린 렌더(`artifacts/ui-nav/*.png`): 추천 조치(100/150/200%), 좁은 폭, 문제 해결(증상 칩·절차 펼침), 조치 화면을 눈으로 확인.
- 한계: 실제 창에서 키보드 탐색·DPI 전환·창 크기 최소값(760px)에서의 페이지별 배치는 실기 확인이 남는다. 조치 페이지가 메인 스크롤 안에 있어 확인 화면이 생기면 바깥 스크롤을 위로 올리는 처리를 추가했다.
