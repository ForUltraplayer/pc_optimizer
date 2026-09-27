# 진단 카드 → 주사율 시험 실행 연결

사용자가 preview.2의 화면을 제시하며 “바로 적용되는 건 없는데?”라고 지적했다. 실행기가 별도 인자에 숨겨지고 카드에는 설정 열기만 있어 사용자 흐름이 끊긴 것이 원인이다. 기능 추가 우선 지시를 반영해 **일반 실행에서 카드로 시험 기능에 접근**하도록 변경했다. 실제 화면 전환 검증을 통과했다는 의미는 아니며, 사용자 확인 뒤 시도하는 프리뷰 기능으로 제공한다.

## 변경

- App의 `--display-evaluation` 필수 조건 제거. Full 사용자 조건은 유지한다. 별도 명령행 인자 없이 실행한다.
- `DisplayFindingTarget`이 같은 대상의 모니터 경로·정수 최대 주사율·Finding ID를 확인한다. 제목이나 DISPLAY1 순번으로 다른 화면을 추측하지 않는다.
- `FindingCardViewModel`/`MainViewModel`/`MainWindow`가 후보 카드에 **NHz 시험 적용**을 표시하고 실행 창을 연다. 해당 경로/주사율을 새로 조회해 선택하며 사전 시험·미리보기까지만 수행한다. **확인한 내용 실행**을 따로 눌러야 실제 임시 적용이 시작된다. 유지/15초 원복·기록 복구 엔진은 변경하지 않았다.
- 후보가 사라졌거나 바뀌면 이전 미리보기를 지우고 거절한다. 첫 모니터/다른 주사율로 자동 대체하지 않는다. SystemOnly·사용자 식별 실패에서는 직접 시험 버튼을 제공하지 않는다.
- ‘바로 할 수 있는 것’ 건수에 연결된 주사율 후보를 포함한다. 일반 진입 버튼은 **주사율 변경 · 되돌리기**로 표시한다. 실제 사전 검사에서 실행이 거절될 수 있다.
- 카드의 ‘안전’ 배지를 ‘주의’로 바꾸고 무조건 15초 뒤 복원된다는 문구를 원복 시도·중단 시 직접 복구 가능성으로 바로잡았다. 시험 화면에도 실기 검증 미완료와 중단 한계를 유지한다.
- 버전 0.3.0-preview.3. 실행방법.txt에서 별도 인자 설명을 일반 카드 사용 흐름으로 교체했다.

## 자체 검증

- `dotnet test tests/PcOptimizer.Tests -c Release --no-restore --filter 'FullyQualifiedName~DisplayCardActionTests|FullyQualifiedName~DisplayTrialButtonIsVisibleOnlyForFullScope' --logger 'trx;LogFileName=sp1-display-card.trx'`: **9/9**. 실제 규칙 Finding 연결, 같은 모니터 필드 결합, 기본 주사율 거절, 카드→준비→명시 실행→유지 대역, 오래된 카드/다른 화면 거절, 실제 XAML Full/SystemOnly 버튼/건수 검증.
- `dotnet test tests/PcOptimizer.Tests -c Release --no-build --filter 'Category!=Smoke&Category!=Online&Category!=ToolSmoke' --logger 'trx;LogFileName=sp1-display-card-basic.trx'`: **1458/1458**. 기존 주사율 실행기·원복·취소 회귀 포함.
- `dotnet build PcOptimizer.sln -c Release --no-restore`: **경고 0 / 오류 0**.
- 오프스크린 `artifacts/sp1-display-card/display-card-action.png`에서 카드의 시험 적용 버튼과 바로 할 수 있는 것 1건을 확인했다. fixture의 120Hz는 사용자 PC 측정값이 아니다. 실제 키 입력·앱 버튼 클릭·화면 설정 변경은 하지 않았다.
- 첫 검증의 주의 문구 60자 상한 위반을 줄여 해결했다. XAML 검사는 저장소 확인 불가 카드에도 같은 템플릿 버튼이 생성돼 2개로 잡히던 선택 조건을 해당 디스플레이 카드로 좁혔다. 초기 실패 3건 이후 9건/전체 통과.
- 네이티브 실행기/수집기 변경이 없어 Smoke/Online/ToolSmoke는 이번 재실행하지 않았다. 직전 Smoke 42/42를 새 실행 결과로 쓰지 않는다. 실제 화면 전환·원복/가독성/재부팅은 여전히 미검증이다.

## 기록 원칙

배포: `tools/package.ps1 -OutputDirectory dist/sp1-evaluation` 성공. preview.3 ZIP 최상위4개/내부13파일, EXE 65,148,575 bytes. 규칙 원문·고지·SHA 검증 통과. `dist/sp1-evaluation/PcOptimizer-v0.3.0-preview.3-win-x64.zip`, SHA-256 `76364D4C5FF40A82A0D33B0660B220FCBA2B925FE9323A3AC606F25E14629FE8`. 기존 preview.2 ZIP과 사용자가 실행 중인 앱은 교체하지 않았다.

별도 평가 인자로 숨기던 이전 정책을 사용자 요청에 따라 일반 실행에서 명시적으로 시험하는 방식으로 바꿨다. Task 8 실기 평가 완료나 독립 승인으로 상태를 올리지 않는다. 기존 원장 이력은 보존한다. 엔진의 SID/모드 재식별·CDS_TEST·쓰기 전 관문·Pending·15초 기한·외부 변경 거절은 유지한다. 기본 실행 시 자동으로 화면을 바꾸지 않는다.
