# SP5 문제 해결 도구함 구현 계획 (2026-09-27)

> 스펙: `docs/superpowers/specs/2026-09-27-improvement-phase2-design.md` §5A. 사용자 지시: SP5 → SP3 순서로 구현 우선, 독립 리뷰·서브에이전트 없음, 검증은 최하위. 이 계획은 구현자(Claude)가 직접 수행한다.

**목표:** 증상으로 들어와 절차를 따라가는 "문제 해결" 창. Windows 내장 명령은 고정 인자로 직접 실행, 내장 GUI는 열기, 외부 도구는 공식 링크 + 한국어 절차.

**구조:**
- 데이터 `rules/troubleshooting-tools.json`(Probes 어셈블리 임베드, 파일 시스템에서 읽지 않음): `tools[]`(id·category·name·when/what/caution 한 줄·mode·safety·steps[]·warning·linkId|command|openTarget·rebootRequired), `symptoms[]`(id·title·summary·steps[{tool, note}]).
- 외부 도구 링크는 기존 `rules/vendor-links.json`에 `kind: tool` 항목으로 추가해 `LinkPolicy` 허용 목록(호스트+경로 접두)을 그대로 쓴다. 도구 JSON은 `linkId`로 참조만 한다.
- Core `Troubleshooting/`: 모델, 파서(오류 하나라도 있으면 전체 거부), `RepairCommandCatalog`(직접 실행 명령의 닫힌 목록: 실행 파일은 System32 아래 고정 이름, 인자 고정, 재부팅 의미, 시간 상한), `BuiltInToolCatalog`(내장 GUI: System32 실행 파일·.msc·ms-settings URI).
- Probes `Troubleshooting/`: 임베드 로더, `RepairCommandRunner`(셸 없음, 작업 폴더 System32, 환경 변수 정리, 출력 상한·진행 스트리밍, 취소 시 트리 종료, 종료 코드 해석), 시스템 복원 지점은 WMI `SystemRestore.CreateRestorePoint`.
- App: `TroubleshootingViewModel` + `TroubleshootingWindow`(좌: 증상 목록·모든 도구, 우: 순서별 도구 카드[언제/무엇/주의·안전 배지·절차 펼치기·실행/열기/공식 사이트]·출력 창·결과·재부팅 안내). 메인 화면 "문제 해결 도구" 버튼. 실행은 `OperationCoordinator` Apply 관문으로 검사·조치와 상호 배제.

**1차 도구:** DISM RestoreHealth → SFC, 시스템 복원 지점, chkdsk 온라인 검사·재부팅 예약(chkntfs /C), 디스크 관리, 메모리 진단(mdsched), ipconfig /flushdns, netsh winsock reset, 네트워크 문제 해결사(ms-settings:troubleshoot), 이벤트 뷰어, 신뢰성 모니터; 외부: CrystalDiskInfo/Mark, MemTest86, TestMem5, DDU, Windows 11 미디어 생성 도구/ISO, Rufus, HWiNFO64, CPU-Z, GPU-Z, Prime95, OCCT, Cinebench, FurMark, 3DMark.

**규칙(스펙):** 직접 실행은 절대 경로·고정 인자만, 출력 상한, 개인 경로 미노출. 외부 도구는 링크만(번들·자동 다운로드 없음, 버전 미안내). 스트레스·DDU·설치 USB는 '주의' 이상 + 백업·전원·온도 경고를 절차 첫 줄에. 절차 끝에 "도구 UI가 바뀌면 다를 수 있음" 명시.

**작업 순서:** (1) 데이터·Core 모델/파서/명령 목록 + 테스트 → (2) Probes 로더·실행기 → (3) App 창·연결 → (4) 문서·원장. 검증은 관련 필터 후 전체 1회.
