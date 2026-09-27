# 고급 탭 첫 구현 묶음 (2026-09-28)

기준 `472865a`. 기존 조사 문서의 미커밋 변경을 보존한 상태에서 구현했다. 이 문서는 구현자 자체 확인이며 독립 리뷰가 아니다. 전체 고급 계획이나 GPU별 직접 제어 완료를 뜻하지 않는다.

## 구현

- Core `AdvancedOptions.cs`, `ActionModels.cs`: MPO/HAGS/GameMode와 기본값/켜기/끄기의 닫힌 대상, 재부팅 목적, 관측 상태. ActionId는 뒤에 추가하여 기존 직렬화 값 보존.
- Probes `Actions/Advanced/AdvancedRegistryAdapter.cs`, `AdvancedRegistryPlatform.cs`: 기존 RestoreCoordinator의 Pending 선행 저장·실행 직전 값 비교·원상 복구 재사용. 레지스트리 값의 부재도 복원. 모르는 값/타입은 변경하지 않음. 고정 키가 없으면 만들지 않고 사유 표시.
- MPO: 기본 동작은 OverlayTestMode 삭제, 사용 안 함은 DWORD 5. “Windows 기본값”과 앱 작업 전 값 복원은 별도 동작.
- HAGS: 기존 명시 DWORD 1/2가 관측된 경우만 변경. 값이 없으면 하드웨어 지원을 추정하지 않고 Windows 설정에서 먼저 확인하도록 안내. CurrentControlSet REG_LINK를 무조건 따라가지 않고 Select\Current의 1~999 번호로 ControlSetNNN 경로를 만들며 복구 대상 키에도 이 식별을 보존. 각 실제 쓰기는 링크 미추종 고정 핸들에서 재비교.
- GameMode: Full 사용자 범위만 HKCU 변경, SystemOnly/Unknown은 거절. 현재 게임의 실제 처리 효과가 아니라 저장 설정값으로 표시.
- `RestartActionAdapter.cs`: UEFI/고급 시작 확인 화면 → 앱 안 5초 취소 대기 → System32 shutdown.exe 고정 인자. `/r /fw /t 0` 또는 `/r /o /t 0`, `/f` 없음. 실제 프로세스 시작에만 Started, 종료까지 공통 관문 유지. 안전 모드 직행은 구현하지 않았으며 WinRE 활성 조회도 남아 있어 확인 화면에 명시.
- App `AdvancedViewModel`, `AdvancedView`, App/MainWindow 연결: 고급 페이지에서 비동기 설정 조회·효과 설명·선택·공통 확인 화면 이동. 적용/복원은 기존 조치 기록과 연결. 설정 저장과 재부팅 후 효과를 구분. 기존 탭 위치 유지/메인 스크롤을 공유하며 별도 내부 스크롤을 만들지 않음. 재부팅 요청 후 자동 전체 스캔은 생략.
- 카탈로그는 이번 3종에서 코드의 닫힌 목록을 사용했다. JSON 표시 메타데이터 분리는 아직 하지 않았고 임의 레지스트리/실행문 엔진도 없다.

## NVIDIA 공식 MPO 파일 원문 확인

공식 문서: https://nvidia.custhelp.com/app/answers/detail/a_id/5157

파일을 실행/등록하지 않고 HTTP 응답 바이트의 내용·해시만 확인했다. 웹 도구는 octet-stream을 읽지 못했고 샌드박스 셸의 네트워크 소켓은 거절되어 정상 권한 요청 후 읽었다.

| 파일 | 크기 | SHA-256 | 내용 |
|---|---:|---|---|
| mpo_disable.reg | 252 | `1F959DA931FF135B99FD0EEB4484BE701B9D5BEF5A2FF2D8028C140522D21CD1` | HKLM\SOFTWARE\Microsoft\Windows\Dwm, OverlayTestMode DWORD 5 |
| mpo_restore.reg | 226 | `AD792DA135888B522A097AC9639FBC8E5508CC12A797DA88D79A0D876D8385A8` | 동일 값 삭제 |

첨부: `/ci/fattach/get/824301808/0/filename/mpo_disable.reg`, `/ci/fattach/get/824301809/0/filename/mpo_restore.reg` (위 NVIDIA 호스트).

## 자체 검증과 실패 이력

- `dotnet build src/PcOptimizer.App/PcOptimizer.App.csproj -c Release --no-restore`: 경고 0, 오류 0.
- `dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter 'FullyQualifiedName~AdvancedActionTests|FullyQualifiedName~AdvancedViewTests'`: 25/25. 원본 부재 복원, Windows 기본값 후 원복, 외부 변경/ControlSet/세션 변경, SystemOnly, 미지원 형식·MPO 강제 On, 실패 Pending 보존, 고정 명령, 재부팅 전 취소/관문, UI 상태/선택/100·150·200% 렌더 포함.
- 기본 `--filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --no-build`: **1725/1725**. TRX: `tests/PcOptimizer.Tests/TestResults/advanced-basic.trx`.
- 신규 네이티브 `--filter 'FullyQualifiedName~AdvancedRegistrySmokeTests' --no-build`: **1/1**. 처음 샌드박스는 HKCU 임시 키 생성 접근 거절. 정상 권한 요청 후 `Software\PcOptimizer.Tests\<새 GUID>\Advanced` 소유 키만 생성·변경·제거하여 성공. 실제 MPO/HAGS/GameMode 키와 GPU, 재부팅은 변경하지 않음. `advanced-native-elevated.trx`.
- 첫 테스트 컴파일에서 System.IO using 및 인터페이스 대상 new() 2곳 오류를 수정했다. 최초 좁은 화면 렌더에서 설명 줄바꿈 누락을 발견하고 명시적 Wrap으로 고쳤으며 회귀 조건에 높이/Wrap을 추가했다. 이미지 `artifacts/advanced-ui/advanced-1.png` 등, 재렌더 후 설명 줄바꿈 확인.
- 전체 기존 Smoke/Online/ToolSmoke는 실행하지 않았다. 평가 장치에서 재부팅 도착·GPU 처리 효과·실제 HAGS 변경은 검증하지 않았다.

## 남은 범위

T4~T7 NVIDIA 프로필 ReBAR·NVIDIA 영상·AMD ADLX 직접 제어는 미구현. 최신 공식 NVAPI 헤더/함수 표를 확인했지만 Windows 기본 설정 조치와 같은 것으로 간주하지 않는다. T8 MPO 나머지 옵션 채택/안전 모드 직행, T10 추가 후보도 남는다. T9는 위 구현분 UI만 연결했다. T1의 전체 GPU 모델·JSON 분리와 T3의 WinRE 조회, 재부팅 전후 효과 추적은 잔여다. 통합 새 ZIP/버전·원격 배포 없음.

관련 REV-004/015/008/013/016/017/018의 기존 발견·독립 검증 상태는 유지한다. 새 코드의 자체 회귀 통과를 원장의 독립 검증 완료로 바꾸지 않는다.
