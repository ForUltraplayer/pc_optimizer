# 스크롤·탭 위치·시인성 개선 (2026-09-27, preview.13)

기준 `d6ea835`, 브랜치 `codex/sp1-actions`. 사용자 요청에 따른 구현자 자체 검증이다. 독립 리뷰/서브에이전트는 사용하지 않았다. 제품 변경은 이 문서와 같은 커밋에 포함된다.

## 변경과 재현 조건

- `Services/ScrollChain.cs`, 각 뷰 XAML: 안쪽 스크롤 영역에서 휠 입력을 소비하던 현상을 해결했다. 원래 입력 지점부터 올라가며 해당 방향으로 움직일 수 있는 가장 가까운 세로 영역 하나만 이동한다. 내부 끝/시작이면 부모로 연결하며, 무한 높이로 펼쳐진 내부 뷰도 바깥 본문을 움직인다. 포커스는 바꾸지 않는다. Ctrl/Shift 등의 조합 및 시스템 휠 비활성 설정은 가로채지 않는다.
- `MainWindow.xaml.cs`: 탭이 접히기 전 위치 저장 → 새 탭 배치 후 복원. 탭별 마지막 위치는 현재 앱 수명 동안 유지하며 재시작 영속 저장은 하지 않는다. 첫 방문은 0, 콘텐츠가 짧아졌으면 실제 스크롤 범위로 제한된다. 빠른 연속 이동 시 지난 복원 요청을 취소하고 전환 도중의 0으로 저장 위치를 덮지 않는다.
- `ActionCenterView.xaml.cs`, 메인 이벤트 연결: 새 대상 확인/실행 결과는 상단에 표시한다. 상태가 null로 지워지거나 탭만 바뀌면 무조건 상단으로 올리지 않는다.
- `Resources/VisibilityTheme.xaml`, 각 뷰: 짙은 남색 제목 표시줄·내비게이션, 청회색 본문, 흰 카드, 더 진한 경계와 보조 텍스트. 세로 스크롤바는 16px 폭에 진한 손잡이와 트랙을 쓴다. 경고·주의 배지 의미는 유지한다.
- `Views/ContrastTitleBar.cs`: WPF UI 기본 제목 표시줄 템플릿/명령을 사용한다. `ButtonsForeground`가 흰색이어도 초기 `RenderButtonsForeground`가 검정으로 남는 것을 실제 렌더에서 발견해 템플릿 적용 때 동기화했다. 버튼 높이도 제목 표시줄 42px에 맞춘다. hover/복귀 후 아이콘 색도 확인했다.
- 공통 사양 이미지 내보내기용 `PcSpecView`의 팔레트/익명화 및 실행기·보호 관문·링크 카탈로그는 변경하지 않았다.

## 자체 검증

1. `dotnet build PcOptimizer.sln -c Release --no-restore`: 경고 0 / 오류 0.
2. `dotnet test tests/PcOptimizer.Tests -c Release --no-build --no-restore --filter "Category!=Smoke&Category!=Online&Category!=ToolSmoke" --logger "trx;LogFileName=basic.trx" --results-directory artifacts/visibility-scroll`: **1700/1700**, 건너뜀 0. 신규 6개(휠 2, 페이지 1, 제목 표시줄 3배율).
3. `ScrollChainTests`: 실제 WPF routed PreviewMouseWheel를 텍스트 자식에서 발생시켜 3중 영역의 하향/상향 경계 전달, 내부 무한 높이, 반 칸 delta 두 번, 포커스 유지, 기능 해제를 확인.
4. `MainWindowLayoutTests.NavigationRestoresEachPageAndOnlyNewConfirmationReturnsToTop`: 전체 결과↔조치 탭 각각 120/300 위치, 연속 3번 전환, 새 미리보기 상단, 미리보기 해제 시 위치 유지.
5. `RealThemeRendersVisibleTitleBarButtons`: 실제 WPF UI Light/Controls 리소스로 창 버튼 3개가 배치되며 실제 Path.Fill이 흰색인지, hover/복귀 후에도 흰색인지 확인. 100/150/200% 렌더 생성. 기존 좁은 창/텍스트 레이아웃도 기본 테스트에 포함.
6. 화면 증거: `artifacts/visibility-scroll/ui/main-themed-{100,150,200}.png`, `action-choices.png`, `utilities.png` 등. 메인/조치/유틸리티 렌더를 시각 확인했다. 독립 창 렌더 헬퍼는 투명 본문 밑에 실제 Window.Background를 먼저 그려 검은 투명 배경 착시를 없앴다.
7. `tools/package.ps1 -Configuration Release -OutputDirectory dist/sp1-integrated`, `tools/verify-package.ps1 -ZipPath dist/sp1-integrated/PcOptimizer-v0.3.0-preview.13-win-x64.zip`: ZIP 13파일/최상위4, PE·고지·규칙 원본 및 SHA-256 검증 통과.

개발 중 신규 테스트의 네임스페이스 2개 컴파일 오류를 수정했고, 실제 아이콘 검정 렌더 재현 3건과 파생 컨트롤의 기본 스타일 미연결 실패 3건 후 수정해 최종 전체 기본 테스트가 통과했다. 테스트를 제외하거나 기대 색을 검정으로 완화하지 않았다.

## 배포와 남은 확인

- `dist/sp1-integrated/PcOptimizer-v0.3.0-preview.13-win-x64.zip` — 60,023,941 bytes.
- SHA-256 `3CA80153DDB0745C549525709BDEAD3578C0ABBCAC66B4CD015245638478AFD6`.
- 같은 이름의 폴더에 `PcOptimizer.exe`가 있다. preview.12와 실행 중 앱은 교체/종료하지 않았다.
- Smoke/Online/ToolSmoke는 UI 변경 범위에 해당하지 않아 재실행하지 않았다. 실제 마우스·터치패드·다중 모니터 DPI 변경·최대화/Snap·UAC의 실기 검증은 남는다. 오프스크린 배율 렌더와 routed event 재현은 실기 입력 시험과 구분한다.
- 실제 사용자 캐시/서비스/레지스트리/화면 모드 변경, master 병합, 원격 게시 없음.
