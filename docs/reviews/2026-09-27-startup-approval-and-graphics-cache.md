# T6 작업 관리자 시작 상태 토글 · T10 그래픽 셰이더 캐시 부분 정리 · Resolve 기본 위치 — 구현 기록 (2026-09-27, Claude)

기준 `3e74347`(Codex 인계 문서). 구현 커밋 **`e25aa43`**(T6), **`9bc264a`**(T10·Resolve). 사용자 지시: HANDOFF 5절 순서대로 구현 → UI → 검증(최하위). 독립 리뷰·서브에이전트 없음. 이 문서는 구현자 자체 기록이며 독립 승인이 아니다.

## 1. T6 — StartupApproved 토글 (`ActionId.StartupApproval` / `MachineStartupApproval`)

### 근거(이 PC, Windows 11 Pro 26200, 2026-09-27 읽기 전용 관측)

| 키 | 값 수 | 길이 | 형식 |
|---|---|---|---|
| HKCU `Explorer\StartupApproved\Run` | 26 | 전부 12바이트 REG_BINARY | 활성 `02 00 00 00` + 0×8, 비활성 `03 00 00 00` + FILETIME LE |
| HKLM `…\StartupApproved\Run` | 5 | 12 | 전부 활성(0x02) |
| HKLM `…\StartupApproved\Run32` | 2 | 12 | 전부 비활성(0x03 + FILETIME) |
| HKCU `…\Run32`, `…\StartupFolder` | 0 | — | — |

- 비활성 값의 8바이트를 FILETIME으로 해석하면 2024-03-15, 2025-08-20, 2026-08-05 등 실제 사용 시점의 날짜가 나온다(마지막 "사용 안 함" 전환 시각). 활성 값은 항상 0.
- Run에 없는 이름(예: 이전에 지운 앱)이 StartupApproved에 남아 있다. 값 존재는 등록 존재를 뜻하지 않는다.
- Run32 항목의 승인 값은 HKLM 64비트 보기의 `StartupApproved\Run32`에 있다(기존 프로브와 동일).

### 계약

- `Core/Actions/StartupApproval.cs`: `ValidValue`(REG_BINARY·12바이트·첫 DWORD 0x02(뒤 0) 또는 0x03), `StateOf`(값 없음 = 활성), `Enabled()`, `Disabled(now)`, `Toggled(current, now)`, `SourceFor(run source)`.
- 출처 `hkcu.approved`·`hklm64.approved`·`hklm32.approved`·`hkcu.approvedFolder`·`hklm.approvedFolder`(복구 키 접두 `*-approved-v1:`). 기존 Run 해제 출처·키와 구분된다.
- `StartupRunPlatform(source)`가 StartupApproved 경로를 열고(OPEN_LINK 유지, 링크 거절 유지), 승인 모드에서는 이진 값만 쓰기 허용. `StartupRunActionAdapter.ForApproval(session, machine)`이 현재 값의 반대 상태로 토글하고 원래 바이트(또는 값 없음)를 복구 기록에 저장한다. 복원은 현재 값이 앱 적용 값과 같을 때만.
- 선택 목록: 검사 스냅샷에서 StartupApproved 조회가 `found`+Binary 또는 `missing`인 Run·시작 폴더 항목마다 "작업 관리자 시작 상태 전환" 항목을 추가한다. `unreadable`·`notTracked`·문자열 형식은 제외.
- 조율기·코덱·복원 문구·화면 이름에 두 ActionId를 추가했다. 기존 복구 JSON은 ActionId를 숫자로 저장하므로 열거형 끝에 추가해 호환을 유지했다.

### 검증(구현자)

- `StartupApprovalTests` 13건: 형식 수용/거절, 상태·토글, 값 없음→사용 안 함→복원(값 삭제), 사용 안 함→사용→원래 바이트 복원(HKLM32·SystemOnly), 미지원 형식·출처·범위 거절, 미리보기 뒤 외부 변경 시 미기록, 선택 대상 7케이스.
- 시작 관련 필터 80/80 통과.

### 한계(미검증)

- 실제 다음 로그인에서 Windows가 이 값을 그대로 존중하는지는 별도 평가 PC에서 확인해야 한다. 이 PC의 실제 항목은 바꾸지 않았다.
- 관측은 한 PC·한 빌드의 형식이다. 다른 빌드에서 길이나 표식이 다르면 `ValidValue`가 거절해 변경하지 않는다(조용히 통과하지 않음).
- 작업 관리자 UI가 같은 값을 즉시 다시 읽는지(캐시 여부)는 확인하지 않았다.
- 실제 레지스트리 Smoke(소유 GUID 키에 12바이트 값 CAS)는 추가하지 않았다. 기존 문자열 CAS Smoke가 같은 `RegSetValueExW` 경로를 쓴다.

## 2. T10 — 그래픽 셰이더 캐시 부분 정리 (`ActionId.GraphicsShaderCache`)

- `Probes/Actions/Files/GraphicsCacheTargets.cs`: 키 `nvidia-dxcache`·`nvidia-glcache`·`nvidia-nvcache`·`d3d-shadercache`, 위치는 관측 프로브(`ShaderCacheInspector`)와 동일한 `%LocalAppData%` 하위. LocalAppData가 프로필 기본 위치가 아니면 거절.
- 기존 `FileCleanupAdapter` 엔진 재사용: 30일 이상 만들어지거나 바뀌지 않은 파일만, 파일별 독점 핸들(사용 중이면 건너뜀), 링크·읽기 전용·다중 링크 제외, 보호 정책·다른 사용자 경계, 실행 직전 재검증. 폴더·드라이버 설정·NVIDIA 제어판은 건드리지 않는다.
- 이 조치는 NVIDIA 공식 전체 초기화 절차(캐시 끄기→재부팅→전체 삭제→복원)의 대체가 아니라 부분 정리다. 화면 문구·규칙 카드·수동 안내를 그렇게 바꿨다.
- 검증: `GraphicsCacheCleanupTests` 5건(키·기본 위치, 옮긴 LocalAppData 거절, 30일 기준, 사용 중 파일 건너뛰기 Partial, SystemOnly 거절). 관련 필터 183/183.
- 한계: 실제 게임/드라이버 실행 중 캐시 파일을 독점 열기했을 때의 거동은 이 PC에서 실기 시험하지 않았다. 사용 중 파일은 건너뛰는 설계이므로 최악의 경우 결과가 Partial이다.

## 3. T10 — Resolve 기본 CacheClip 위치 (실제 fixture)

- 이 PC의 DaVinci Resolve 20.1.00020 `config.dat`(2,397바이트)에는 `RenderCaching.CacheDir` 키가 없다. `Site.1.FS.Count = 2`이고 두 번째 저장소는 가상(`ResolveVirtual…`)이다. 첫 저장소 루트 아래 `CacheClip` 폴더가 실제로 존재한다.
- 기존 리더는 키가 없으면 `Invalid`를 돌려 기본 설치를 "설정 형식 미지원"으로 표시했다. 키가 없으면 `CacheClip`(첫 저장소 아래) 기본값으로 보도록 바꿨다. 테스트 1건 추가.
- 프로젝트별 캐시 위치 재정의는 프로젝트 데이터베이스(PostgreSQL/디스크 DB) 안에 있어 텍스트 설정으로는 읽을 수 없다. 자동 탐지 범위 밖으로 남긴다.

## 4. T10 — Adobe·CapCut 설정 자동 탐지 (조건 미충족)

- 이 PC에 Adobe Premiere Pro/Media Encoder/After Effects와 CapCut이 설치돼 있지 않다(`%AppData%\Adobe`에는 Flash Player만, `%LocalAppData%\CapCut` 없음, 제거 프로그램 목록에 없음). 버전별 설정 fixture를 확보할 수 없어 리더를 바꾸지 않았다. HANDOFF 5절 1항의 지시대로 조건만 기록한다.

## 5. 전체 검증

- Release 솔루션 빌드 경고 0/오류 0(각 커밋 시점).
- 기본 필터·Smoke 전체 실행 결과는 HANDOFF 4절과 원장 기록에 적는다.
